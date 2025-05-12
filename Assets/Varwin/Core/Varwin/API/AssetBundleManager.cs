using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using NLog;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Varwin.Data.ServerData;
using Varwin.ECS.Systems;
//using Logger = NLog.Logger;

namespace Varwin.WWW
{
    public class AssetBundleManager : MonoBehaviour
    {
        public static AssetBundleManager Instance;
        private static AssetBundle _currentBundle;

        private readonly Dictionary<string, AssetBundle> _loadedSceneAssetBundles = new();
        private readonly Dictionary<string, AssetBundle> _alwaysLoaded = new();

        private AssetBundle _previousScene;
        private string _previousSceneName;


        private static IEnumerator DownloadAssetBundle(string url, RequestAsset requestAsset)
        {
            while (!Caching.ready)
            {
                yield return null;
            }

            using UnityWebRequest webRequest = UnityWebRequestAssetBundle.GetAssetBundle(url, requestAsset.BundleCacheSettings);

            if (!string.IsNullOrEmpty(Request.AccessToken))
            {
                webRequest.SetRequestHeader("Authorization", $"Bearer {Request.AccessToken}");
            }
                
            var clientId = Request.SocketId?.ToString();
            if (!string.IsNullOrEmpty(clientId))
            {
                webRequest.SetRequestHeader("X-Socket-ID", clientId);
            }

            yield return webRequest.SendWebRequest();

            try
            {
                _currentBundle = DownloadHandlerAssetBundle.GetContent(webRequest);
            }
            catch
            {
                _currentBundle = null;
            }

            Debug.Log($"Loaded {requestAsset.AssetName} from {(webRequest.responseCode == 0 ? $"cache ({requestAsset.BundleCacheSettings.name})" : "server and cached")}. " +
                      $"Total cache size: {Caching.currentCacheForWriting.spaceOccupied / 1024 / 1024} Mb");
        }

        private static IEnumerator LoadAssetBundleFromMemory(byte[] bytes)
        {
            while (!Caching.ready)
            {
                yield return null;
            }

            AssetBundleCreateRequest createRequest = AssetBundle.LoadFromMemoryAsync(bytes);

            while (!createRequest.isDone)
            {
                yield return null;
            }

            _currentBundle = createRequest.assetBundle;
        }

        private static IEnumerator LoadAssetBundleFromFile(string path)
        {
            var cachingReadyWaiting = 0f;
            while (!Caching.ready)
            {
                yield return null;
                cachingReadyWaiting += Time.deltaTime;
                const float cachingReadyTimeout = 10f;

                if (cachingReadyWaiting > cachingReadyTimeout)
                {
                    var errorMessage = "LoadAssetBundleFromFile: Caching is not ready\n.";
                    var currentCache = Caching.currentCacheForWriting;

                    errorMessage += $"Current cache: {currentCache.path}. Free space: {currentCache.spaceFree}. Space occupied: {currentCache.spaceOccupied}";
                    errorMessage += "\nCache will be cleared\n";

                    Debug.LogError(errorMessage);
                    Caching.ClearCache();
                    break;
                }
            }

            var assetBundleCreateRequest = AssetBundle.LoadFromFileAsync(path);

            while (!assetBundleCreateRequest.isDone)
            {
                yield return null;
            }

            if (!assetBundleCreateRequest.assetBundle)
            {
                Debug.LogError("LoadAssetBundleFromFile: Bundle is not loaded\n" + path);
                yield break;    
            }

            _currentBundle = assetBundleCreateRequest.assetBundle;
        }

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;

                SceneManager.sceneUnloaded += ClearAllLoadedDataOnExit;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy() => ForceClear();

        private void ClearAllLoadedDataOnExit(UnityEngine.SceneManagement.Scene scene)
        {
            if (_alwaysLoaded.Any(x => x.Value.GetAllScenePaths()[0] == scene.path) && !LoaderAdapter.IsLoadingProcess)
            {
                return;
            }

            if (scene.buildIndex >= 0) // У загружаемых сцен из AssetBundle buildIndex всегда -1
            {
                return;
            }

            ForceClear();
        }

        public void ForceClear()
        {
            if (Contexts.sharedInstance != null)
            {
                new DestroyAssetSystem(Contexts.sharedInstance).Execute();
            }

            GameStateData.ClearAllData();
            Resources.UnloadUnusedAssets();
            UnloadAssetBundles(AssetBundle.GetAllLoadedAssetBundles());
            LogicUtils.RemoveAllEventHandlers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private void UnloadAssetBundles(IEnumerable<AssetBundle> bundles)
        {
            foreach (AssetBundle assetBundle in bundles)
            {
                if (!assetBundle)
                {
                    continue;
                }

                if (!_alwaysLoaded.ContainsValue(assetBundle))
                {
                    assetBundle.Unload(true);
                }
            }
        }

        public IEnumerator InstantiateGameObjectAsync(RequestAsset requestAsset)
        {
            yield return StartCoroutine(DownloadAssetBundle(requestAsset.Uri, requestAsset));
            AssetBundle bundle = _currentBundle;

            if (bundle)
            {
                AssetBundleRequest loadAssetAsync = bundle.LoadAssetAsync(requestAsset.AssetName);
                yield return loadAssetAsync;
                var response = new ResponseAsset {Asset = loadAssetAsync.asset, UserData = requestAsset.UserData};
                ((IRequest) requestAsset).OnResponseDone(response);
            }
            else
            {
                string message = "Can not load asset " + requestAsset.AssetName;
                ((IRequest) requestAsset).OnResponseError(message);
            }

            yield return true;
        }

        public IEnumerator InstantiateAssetFromMemoryAsync(RequestLoadAssetFromMemory requestAsset)
        {
            yield return StartCoroutine(LoadAssetBundleFromMemory(requestAsset.Bytes));
            AssetBundle bundle = _currentBundle;

            if (bundle)
            {
                AssetBundleRequest loadAssetAsync = bundle.LoadAssetAsync(requestAsset.AssetName);
                yield return loadAssetAsync;
                var response = new ResponseAsset {Asset = loadAssetAsync.asset, UserData = requestAsset.UserData};
                ((IRequest) requestAsset).OnResponseDone(response);
            }
            else
            {
                string message = "Can not load asset " + requestAsset.AssetName;
                ((IRequest) requestAsset).OnResponseError(message);
            }
        }

        public IEnumerator InstantiateAssetFromFileAsync(RequestLoadAssetFromFile requestAsset)
        {
            Debug.Log($"InstantiateAssetFromFileAsync: Start for \"{requestAsset.AssetName}\""); 
            yield return StartCoroutine(LoadAssetBundleFromFile(requestAsset.Uri));
            Debug.Log($"InstantiateAssetFromFileAsync: LoadAssetBundleFromFile finished for \"{requestAsset.AssetName}\""); 
            AssetBundle bundle = _currentBundle;

            if (bundle)
            {
                Debug.Log($"InstantiateAssetFromFileAsync: Bundle is loaded for \"{requestAsset.AssetName}\"");
                
                AssetBundleRequest loadAssetAsync = bundle.LoadAssetAsync(requestAsset.AssetName);
                yield return loadAssetAsync;
                
                Debug.Log($"InstantiateAssetFromFileAsync: LoadAssetAsync finished for \"{requestAsset.AssetName}\"");
                
                var response = new ResponseAsset {Asset = loadAssetAsync.asset, UserData = requestAsset.UserData};
                ((IRequest) requestAsset).OnResponseDone(response);
            }
            else
            {
                string message = $"Can not load asset \"{requestAsset.AssetName}\"";
                Debug.Log($"InstantiateAssetFromFileAsync: {message}");
                ((IRequest) requestAsset).OnResponseError(message);
            }
        }

        public IEnumerator InstantiateSceneFromMemoryAsync(RequestLoadSceneFromMemory requestScene)
        {
            if (IsPreviousScene(requestScene.AssetName))
            {
                ((IRequest) requestScene).OnResponseDone(ResponseAsset.Get(_previousScene, _previousScene.GetAllScenePaths()[0], requestScene.UserData));
                yield break;
            }

            if (TryGetAlwaysLoadedSceneName(requestScene.AssetName, out string alwaysLoadedSceneName))
            {
                var alwaysLoadedScene = _alwaysLoaded[alwaysLoadedSceneName];
                string path = alwaysLoadedScene.GetAllScenePaths()[0];

                ((IRequest) requestScene).OnResponseDone(ResponseAsset.Get(alwaysLoadedScene, path, requestScene.UserData));
                yield break;
            }

            yield return StartCoroutine(LoadAssetBundleFromMemory(requestScene.Bytes));
            AssetBundle bundle = _currentBundle;

            if (requestScene.AlwaysLoaded)
            {
                if (!_alwaysLoaded.ContainsValue(bundle))
                {
                    _alwaysLoaded.Add(requestScene.AssetName, bundle);
                }
            }

            if (bundle)
            {
                string path = bundle.GetAllScenePaths()[0];
                var response = new ResponseAsset {Asset = bundle, Path = path, UserData = requestScene.UserData};
                ((IRequest) requestScene).OnResponseDone(response);

                if (_previousScene)
                {
                    if (!_alwaysLoaded.ContainsValue(_previousScene))
                    {
                        _previousScene.Unload(false);
                    }
                }

                GCManager.Collect();
                _previousScene = bundle;
                _previousSceneName = requestScene.AssetName;
            }
            else
            {
                string message = "Can not load asset " + requestScene.AssetName;
                ((IRequest) requestScene).OnResponseError(message);
            }
        }

        private bool IsPreviousScene(string assetName)
        {
            return _previousScene && string.Equals(assetName, _previousSceneName, StringComparison.CurrentCultureIgnoreCase);
        }

        private bool TryGetAlwaysLoadedSceneName(string assetName, out string sceneName)
        {
            sceneName = _alwaysLoaded.Keys.FirstOrDefault(a => string.Equals(assetName, a, StringComparison.CurrentCultureIgnoreCase));
            return !string.IsNullOrEmpty(sceneName);
        }

        public IEnumerator InstantiateSceneFromFileAsync(RequestLoadSceneFromFile requestScene)
        {
            if (IsPreviousScene(requestScene.AssetName))
            {
                string path = _previousScene.GetAllScenePaths()[0];
                var response = new ResponseAsset {Asset = _previousScene, Path = path, UserData = requestScene.UserData};
                ((IRequest) requestScene).OnResponseDone(response);
                yield break;
            }

            if (TryGetAlwaysLoadedSceneName(requestScene.AssetName, out var alwaysLoadedSceneAssetName))
            {
                var alwaysLoadedScene = _alwaysLoaded[alwaysLoadedSceneAssetName];
                string path = alwaysLoadedScene.GetAllScenePaths()[0];

                ((IRequest) requestScene).OnResponseDone(ResponseAsset.Get(alwaysLoadedScene, path, requestScene.UserData));
                yield break;
            }

            foreach (var loadedSceneBundle in _loadedSceneAssetBundles.Values) 
                loadedSceneBundle.Unload(false);

            _loadedSceneAssetBundles.Clear();

            yield return StartCoroutine(LoadAssetBundleFromFile(requestScene.Uri));;
            AssetBundle bundle = _currentBundle;

            if (requestScene.AlwaysLoaded)
            {
                _alwaysLoaded.TryAdd(requestScene.AssetName, bundle);
            }
            else
            {
                _loadedSceneAssetBundles.Add(requestScene.Uri, bundle);
            }

            if (bundle)
            {
                ((IRequest) requestScene).OnResponseDone(ResponseAsset.Get(bundle, bundle.GetAllScenePaths()[0], requestScene.UserData));

                if (_previousScene && !_alwaysLoaded.ContainsValue(_previousScene))
                {
                    _previousScene.Unload(false);
                }

                GCManager.Collect();

                _previousScene = bundle;
                _previousSceneName = requestScene.AssetName;
            }
            else
            {
                string message = "Can not load asset " + requestScene.AssetName;
                ((IRequest) requestScene).OnResponseError(message);
            }
        }

        public void UnloadPreviousScene()
        {
            if (_previousScene)
            {
                _previousScene.Unload(true);
            }
        }
    }
}