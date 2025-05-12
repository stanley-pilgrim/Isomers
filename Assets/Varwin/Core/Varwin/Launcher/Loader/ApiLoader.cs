using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartLocalization;
using UnityEngine;
using Varwin.Data.AssetBundle;
using Varwin.Data.ServerData;
using Varwin.Log;
using Varwin.UI;
using Varwin.WWW;

namespace Varwin.Data
{
    public class ApiLoader : BaseLoader
    {
        private const string KtxFormat = ".ktx";
        
        public delegate void ProgressUpdate(float val);

        public ProgressUpdate OnLoadingUpdate;

        public override void LoadObjects(List<PrefabObject> objects)
        {
            Debug.Log("Scene objects list loaded (API). Number of objects: " + objects.Count);
            ResetObjectsCounter(objects.Count);

            foreach (PrefabObject prefabObject in objects)
            {
                LoadPrefabObject(prefabObject);
            }

            Debug.Log("Required objects list has been loaded (API)");
        }

        public override void LoadObject(PrefabObject prefabObject, Action<PrefabObject> callback) => LoadPrefabObject(prefabObject, callback);

        public override void LoadResources(List<ResourceDto> resources)
        {
            Debug.Log("Resources list loaded (API). Number of resources: " + resources.Count);
            ResetResourcesCounter(resources.Count);

            foreach (ResourceDto resource in resources)
            {
                LoadResource(resource);
            }

            Debug.Log("Required resources list has been loaded (API)");
        }

        protected override void LoadMeshResource(ResourceDto resource)
        {
            var path = Settings.Instance.ApiHost + resource.Assets;
            var request = new Request3dModel(path);

            request.OnFinish += response =>
            {
                var response3DModel = (Response3dModel) response;
                UpdateResourceEntity(resource, response3DModel.GameObject);
            };

            request.OnError += message => LoadResourceDefaultErrorHandler(resource, message);
        }

        protected override void LoadTextureResource(ResourceDto resource)
        {
            var url = Settings.Instance.ApiHost + resource.Path + KtxFormat;

            var requestTexture = new RequestKtxTexture(url, true)
            {
                ErrorLogging = false
            };

            requestTexture.OnFinish += response =>
            {
                var responseTexture = (ResponseTexture) response;
                UpdateResourceEntity(resource, responseTexture.Texture);
            };

            requestTexture.OnError += _ => LoadTextureResourceFallback(resource);
        }

        private void LoadTextureResourceFallback(ResourceDto resource)
        {
            var url = Settings.Instance.ApiHost + resource.Path;

            var requestTexture = new RequestTexture(url, true);

            requestTexture.OnFinish += response =>
            {
                var responseTexture = (ResponseTexture) response;
                UpdateResourceEntity(resource, responseTexture.Texture);
            };

            requestTexture.OnError += message => LoadResourceDefaultErrorHandler(resource, message);
        }

        protected override void LoadTextResource(ResourceDto resource)
        {
            var requestApi = new RequestUri(resource.Path, null, true);

            requestApi.OnFinish += response =>
            {
                var responseUri = (ResponseUri) response;
                UpdateResourceEntity(resource, new TextAsset(responseUri.TextData));
            };

            requestApi.OnError += message => LoadResourceDefaultErrorHandler(resource, message);
        }

        protected override void LoadAudioResource(ResourceDto resource)
        {
            var url = Settings.Instance.ApiHost + resource.Path;
            var request = new RequestAudio(url, resource.StreamAudio);

            request.OnFinish += response =>
            {
                var responseAudio = (ResponseAudio) response;
                UpdateResourceEntity(resource, responseAudio.AudioClip);
            };

            request.OnError += message => LoadResourceDefaultErrorHandler(resource, message);
        }

        protected override void LoadVideoResource(ResourceDto resource)
        {
            var url = Settings.Instance.ApiHost + resource.Path;
            var request = new RequestVideo(url);

            request.OnFinish += response =>
            {
                var responseVideo = (ResponseVideo) response;
                UpdateResourceEntity(resource, responseVideo.VideoUrl);
            };

            request.OnError += message => LoadResourceDefaultErrorHandler(resource, message);
        }

        protected override void LoadAssetBundleParts(PrefabObject prefabObject)
        {
            var assetInfo = prefabObject.AssetInfo;
            
            if (assetInfo?.AssetBundleParts == null || assetInfo.AssetBundleParts.Count == 0)
            {
                return;
            }

            foreach (var pathToPart in assetInfo.AssetBundleParts)
            {
                var standaloneManifest = prefabObject.LinuxReady && ProjectData.IsLinux()
                    ? Path.Combine(prefabObject.Assets, $"linux_{pathToPart}.manifest")
                    : Path.Combine(prefabObject.Assets, $"{pathToPart}.manifest");
                
                var requestManifest = new RequestUri(ProjectData.IsMobileClient
                    ? Path.Combine(prefabObject.Assets, $"android_{pathToPart}.manifest")
                    : standaloneManifest);

                requestManifest.OnFinish += response =>
                {
                    var responseManifest = (ResponseUri) response;

                    var bundleHash = Hash128.Compute(responseManifest.TextData);

                    if (GameStateData.LoadedAssetBundleParts.Contains(bundleHash))
                    {
                        return;
                    }

                    GameStateData.LoadedAssetBundleParts.Add(bundleHash);

                    var assetBundleCacheSettings = new CachedAssetBundle()
                    {
                        name = $"AssetBundlePartManifest_{prefabObject.Guid}_{pathToPart}",
                        hash = bundleHash
                    };

                    var standaloneBundleUri = prefabObject.LinuxReady && ProjectData.IsLinux()
                        ? Path.Combine(prefabObject.Assets, $"linux_{pathToPart}")
                        : Path.Combine(prefabObject.Assets, $"{pathToPart}");
                    
                    var assetBundleUri = ProjectData.IsMobileClient
                        ? Path.Combine(prefabObject.Assets, $"android_{pathToPart}")
                        : standaloneBundleUri;

                    var requestAsset = new RequestAsset(assetInfo.AssetName, assetBundleUri, assetBundleCacheSettings, new object[] {prefabObject, assetInfo});

                    requestAsset.OnFinish += responseAsset => { Debug.Log($"Resources loaded for {assetInfo.AssetName}"); };
                };
            }
        }

        private void LoadPrefabObject(PrefabObject prefabObject, Action<PrefabObject> callback = null)
        {
            var request = new RequestUri(prefabObject.ConfigResource, null, true);

            request.OnFinish += response =>
            {
                string json = ((ResponseUri) response).TextData;

                Debug.Log($"<Color=Yellow>Loading prefab object \"{prefabObject.Name.en}\" (build at {prefabObject.BuiltAt})</Color>");
                if (!LoadAssetInfo(json, prefabObject))
                {
                    return;
                }

                LoadAssetBundleParts(prefabObject);

                if (prefabObject.AssetInfo.Assembly != null)
                {
                    string mainAssembly = FindMainAssembly(prefabObject);

                    foreach (string dllName in prefabObject.AssetInfo.Assembly.Where(dllName => dllName != mainAssembly))
                    {
                        LoadDll(prefabObject, dllName);
                    }

                    if (!string.IsNullOrEmpty(mainAssembly))
                    {
                        LoadDll(prefabObject, mainAssembly);
                    }
                }

                LoadCustomAssetApi(prefabObject, callback);
            };

            request.OnError += s =>
            {
                FeedBackText = $"Load prefab object \"{prefabObject.Name.en}\" error:\n{s}";
                Helper.ShowFatalErrorLoadObject(prefabObject, s);
            };
        }

        private void LoadDll(PrefabObject o, string dllName)
        {
            var dllCachePath = FileSystemUtils.GetFilesPath(ProjectData.IsMobileClient, "cache/dll/");
            var dllPath = $"{o.Assets}/{dllName}";

            var request = new RequestUri(dllPath);
            
            request.OnFinish += response1 =>
            {
                var byteData = ((ResponseUri)response1).ByteData;

                if (AddAssembly(dllCachePath, dllName, ref byteData, out Exception e))
                {
                    return;
                }

                var message = $"{ErrorHelper.GetErrorDescByCode(ErrorCode.LoadObjectError)}: {o.Name} ({o.GetLocalizedName()})\n";
                ShowError(message);
            };

            request.OnError += errorMessage =>
            {
                var message = $"{ErrorHelper.GetErrorDescByCode(ErrorCode.LoadObjectError)}: {o.Name} ({o.GetLocalizedName()})\n{errorMessage}";
                ShowError(message);
            };
        }

        private void LoadDll(SceneTemplatePrefab s, string dllName)
        {
            var dllCachePath = FileSystemUtils.GetFilesPath(ProjectData.IsMobileClient, "cache/dll/");
            var dllPath = $"{s.Assets}/{dllName}";
            
            var request = new RequestUri(dllPath);

            request.OnFinish += response =>
            {
                byte[] byteData = ((ResponseUri) response).ByteData;

                if (AddAssembly(dllCachePath, dllName, ref byteData, out Exception e))
                {
                    return;
                }

                var message = $"{ErrorHelper.GetErrorDescByCode(ErrorCode.LoadSceneError)}: {s.Name} ({s.GetLocalizedName()})\n{e}";
                ShowError(message);
            };

            request.OnError += errorMessage =>
            {
                var message = $"{ErrorHelper.GetErrorDescByCode(ErrorCode.LoadSceneError)}: {s.Name} ({s.GetLocalizedName()})\n{errorMessage}";
                ShowError(message);
            };
        }

        public override void LoadSceneTemplate(int sceneTemplateId, bool isLoadingScene)
        {
            SceneTemplatePrefab sceneTemplatePrefab = GetSceneTemplatePrefab(sceneTemplateId);

            if (!sceneTemplatePrefab.LinuxReady && ProjectData.IsLinux())
            {
                InvokeWindowsOnLinuxBundleLoaded(sceneTemplatePrefab);
            }
            
            var standaloneSceneTemplateManifestUri = sceneTemplatePrefab.LinuxReady && ProjectData.IsLinux()
                ? sceneTemplatePrefab.LinuxManifestResource
                : sceneTemplatePrefab.ManifestResource; 
            
            var requestManifest = new RequestUri(ProjectData.IsMobileClient
                ? sceneTemplatePrefab.AndroidManifestResource
                : standaloneSceneTemplateManifestUri);

            requestManifest.OnFinish += response =>
            {
                var responseManifest = (ResponseUri) response;
                try
                {
                    DownloadSceneTemplateFiles(sceneTemplatePrefab, responseManifest.ByteData, isLoadingScene);
                }
                catch (Exception error)
                {
                    FeedBackText = ErrorHelper.GetErrorDescByCode(ErrorCode.LoadSceneError);
                    Helper.ShowErrorLoadScene(error.ToString());
                }
            };

            requestManifest.OnError += (errorMessage) =>
            {
                FeedBackText = ErrorHelper.GetErrorDescByCode(ErrorCode.LoadSceneError);
                Helper.ShowErrorLoadScene(errorMessage);
            };
        }

        public override void LoadProjectStructure(int projectId, Action<ProjectStructure> onFinish)
        {
            API.GetProjectMeta(projectId, ps =>
            {
                ProjectStructure = ps;

                foreach (ServerData.Scene scene in ProjectStructure.Scenes)
                {
                    var logicRequest = new RequestUri(scene.LogicResource)
                    {
                        ErrorLogging = false
                    };

                    logicRequest.OnFinish += response =>
                    {
                        var logicResponse = (ResponseUri) response;
                        scene.AssemblyBytes = logicResponse.ByteData;
                    };
                    logicRequest.OnError += error =>
                    {
                        scene.AssemblyBytes = null;
                    };
                }

                onFinish?.Invoke(ps);
            });
        }

        private void LoadCustomAssetApi(PrefabObject prefabObject, Action<PrefabObject> callback = null)
        {
            if (!prefabObject.LinuxReady && ProjectData.IsLinux())
            {
                InvokeWindowsOnLinuxBundleLoaded(prefabObject);
            }
            
            var standaloneManifestUri = prefabObject.LinuxReady && ProjectData.IsLinux()
                ? prefabObject.LinuxBundleManifest
                : prefabObject.BundleManifest;
            
            var requestManifest = new RequestUri(ProjectData.IsMobileClient
                ? prefabObject.AndroidBundleManifest
                : standaloneManifestUri);
            
            string checkMobileReady = ProjectData.IsMobileClient
                ? "\nCheck if the object is mobile ready."
                : string.Empty;

            requestManifest.OnFinish += response =>
            {
                var responseManifest = (ResponseUri) response;

                Hash128 bundleHash = Hash128.Compute(responseManifest.TextData);

                var assetBundleCacheSettings = new CachedAssetBundle()
                {
                    name = $"Object_{prefabObject.Guid}",
                    hash = bundleHash
                };

                string assetName = prefabObject.AssetInfo.AssetName;
                string standaloneAssetBundleUri = prefabObject.LinuxReady && ProjectData.IsLinux()
                    ? prefabObject.LinuxBundleResource
                    : prefabObject.BundleResource;
                
                string assetBundleUri = ProjectData.IsMobileClient 
                    ? prefabObject.AndroidBundleResource 
                    : standaloneAssetBundleUri;

                var requestAsset = new RequestAsset(assetName, assetBundleUri, assetBundleCacheSettings, new object[] {prefabObject, prefabObject.AssetInfo});

                requestAsset.OnFinish += responseAsset =>
                {
                    FeedBackText = $"{LanguageManager.Instance.GetTextValue("LOADING")} {prefabObject.GetLocalizedName()}...";
                    CountLoadedObjects++;

                    if (LoaderAdapter.OnDownLoadUpdate != null)
                    {
                        LoaderAdapter.OnDownLoadUpdate(CountLoadedObjects / (float) LoadObjectsCounter.loadObjectsCounter.PrefabsCount);
                    }

                    if (ProjectData.GameMode == GameMode.View)
                    {
                        CreatePrefabEntity(responseAsset, prefabObject, callback: callback);
                    }
                    else
                    {
                        var requestDownLoad = new RequestTexture(Settings.Instance.ApiHost + prefabObject.IconResource, true);

                        requestDownLoad.OnFinish += responseUri =>
                        {
                            var responseTexture = (ResponseTexture) responseUri;
                            Texture2D texture2D = responseTexture.Texture;

                            var sprite = Sprite.Create(texture2D, new Rect(0, 0, texture2D.width, texture2D.height), Vector2.zero);
                            CreatePrefabEntity(responseAsset, prefabObject, sprite, callback);
                        };

                        requestDownLoad.OnError += s => { CreatePrefabEntity(responseAsset, prefabObject, callback: callback); };
                    }
                };

                requestAsset.OnError += s =>
                {
                    var errorMessage = $"Load object {prefabObject.Name.en} error!{checkMobileReady}\n{s}.";
                    FeedBackText = errorMessage;
                    Helper.ShowFatalErrorLoadObject(prefabObject, errorMessage);
                };
            };

            requestManifest.OnError += s =>
            {
                var errorMessage = $"Load object {prefabObject.Name.en} error! Manifest not found.{checkMobileReady}\n{s}.";
                FeedBackText = errorMessage;
                Helper.ShowFatalErrorLoadObject(prefabObject, errorMessage);
            };
        }

        private void DownloadSceneTemplateFiles(SceneTemplatePrefab sceneTemplatePrefab, byte[] manifestOnServer, bool isLoadingScene)
        {
            Debug.Log($"Starting download scene template <{sceneTemplatePrefab.GetLocalizedName()}> assets");

            string pathToEnvironmentsStorage = FileSystemUtils.GetFilesPath(ProjectData.IsMobileClient, "cache/sceneTemplates/");

            string environmentDirectory = Path.Combine(pathToEnvironmentsStorage, sceneTemplatePrefab.Guid);

            FileSystemUtils.CreateDirectory(environmentDirectory);

            string manifestStorageFile = Path.Combine(environmentDirectory, ProjectData.IsMobileClient ? "android_bundle.manifest" : "bundle.manifest");


            if (File.Exists(manifestStorageFile))
            {
                var manifestOnStorage = File.ReadAllBytes(manifestStorageFile);

                //TODO: Turn back caching
                if (Equal(manifestOnServer, manifestOnStorage))
                {
                    LoadSceneTemplateFromStorage(sceneTemplatePrefab, environmentDirectory, isLoadingScene);
                }
                else
                {
                    LoadSceneTemplateFromWeb(sceneTemplatePrefab, environmentDirectory, isLoadingScene);
                }
            }
            else
            {
                LoadSceneTemplateFromWeb(sceneTemplatePrefab, environmentDirectory, isLoadingScene);
            }
        }

        private static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            return !a.Where((t, i) => t != b[i]).Any();
        }

        private void LoadSceneTemplateFromWeb(SceneTemplatePrefab sceneTemplatePrefab, string environmentDirectory, bool isLoadingScene)
        {
            DateTime startLoadingTime = DateTime.Now;
            Debug.Log($"Loading scene template \"{sceneTemplatePrefab.GetLocalizedName()}\" from web");

            var sceneDataRequest = new RequestUri(sceneTemplatePrefab.ConfigResource);
            sceneDataRequest.OnFinish += response =>
            {
                string json = ((ResponseUri) response).TextData;
                var sceneData = json.JsonDeserialize<SceneData>();
                var standaloneBundleUri = sceneTemplatePrefab.LinuxReady && ProjectData.IsLinux()
                    ? sceneTemplatePrefab.LinuxBundleResource
                    : sceneTemplatePrefab.BundleResource;
                
                var standaloneBundleManifestUri = sceneTemplatePrefab.LinuxReady && ProjectData.IsLinux()
                    ? sceneTemplatePrefab.LinuxManifestResource
                    : sceneTemplatePrefab.ManifestResource;
                
                var environmentFiles = new List<string>()
                {
                    sceneTemplatePrefab.ConfigResource,
                    ProjectData.IsMobileClient ? sceneTemplatePrefab.AndroidBundleResource : standaloneBundleUri,
                    sceneTemplatePrefab.IconResource,
                    ProjectData.IsMobileClient ? sceneTemplatePrefab.AndroidManifestResource : standaloneBundleManifestUri,
                };

                environmentFiles.AddRange(sceneData.DllNames.Select(x => $"{sceneTemplatePrefab.Assets}/{x}"));

                var requestDownloads = new RequestDownLoad(environmentFiles,
                    environmentDirectory,
                    LoaderAdapter.OnLoadingUpdate,
                    this,
                    LanguageManager.Instance.GetTextValue("LOADING_FILE"));

                requestDownloads.OnFinish += response1 =>
                {
                    TimeSpan span = DateTime.Now - startLoadingTime;
                    Debug.Log($"Scene template \"{sceneTemplatePrefab.GetLocalizedName()}\" is loaded. Time = {span.Seconds} sec.");

                    var responseDownload = (ResponseDownLoad) response1;

                    string sceneName = sceneData.Name;
                    foreach (string filename in responseDownload.Filenames)
                    {
                        Debug.Log($"Downloaded for scene \"{sceneName}\": {filename}");
                    }

                    var dllNames = sceneData.DllNames.Select(x => (string) x).ToArray();
                    sceneTemplatePrefab.HasScripts = dllNames.Any();
                    foreach (string dllName in dllNames)
                    {
                        LoadDll(sceneTemplatePrefab, dllName);
                    }

                    string assetStandaloneName = sceneTemplatePrefab.LinuxReady && ProjectData.IsLinux()
                        ? $"linux_{sceneData.AssetBundleLabel}"
                        : sceneData.AssetBundleLabel;
                    
                    string assetName = ProjectData.IsMobileClient ? $"android_{sceneData.AssetBundleLabel}" : assetStandaloneName;
                    var bundlePath = $"{environmentDirectory}/{assetName}";

                    Debug.Log($"Loading scene template \"{sceneName}\"...");

                    var requestLoadSceneFromFile = new RequestLoadSceneFromFile(sceneName, bundlePath, null, isLoadingScene);
                    requestLoadSceneFromFile.OnFinish += response2 =>
                    {
                        var responseAsset = (ResponseAsset) response2;
                        string scenePath = Path.GetFileNameWithoutExtension(responseAsset.Path);

                        if (!isLoadingScene)
                        {
                            ProjectDataListener.Instance.LoadScene(scenePath);
                        }
                        else
                        {
                            ProjectDataListener.AddLoadingScene(RequiredProjectArguments.ProjectConfigurationId, scenePath);
                        }
                    };

                    requestLoadSceneFromFile.OnError += s => { ShowErrorLoadScene(); };
                };

                requestDownloads.OnError += s => { ShowErrorLoadScene(); };
            };

            sceneDataRequest.OnError += s => { ShowErrorLoadScene(); };
        }

        private void LoadSceneTemplateFromStorage(SceneTemplatePrefab sceneTemplatePrefab, string environmentDirectory, bool isLoadingScene)
        {
            string sceneName;
            Debug.Log($"Loading scene template \"{sceneTemplatePrefab.GetLocalizedName()}\" from storage: " + environmentDirectory);

            var sceneDataPath = $"{environmentDirectory}/bundle.json";

            var requestJson = new RequestFileRead(sceneDataPath, null, false, true);

            requestJson.OnFinish += responseJson =>
            {
                string json = ((ResponseFileRead) responseJson).TextData;
                var sceneData = json.JsonDeserialize<SceneData>();
                sceneName = sceneData.Name;

                var dllNames = sceneData.DllNames.Select(x => (string) x).ToArray();
                sceneTemplatePrefab.HasScripts = dllNames.Any();
                foreach (string dllName in dllNames)
                {
                    LoadDll(sceneTemplatePrefab, dllName);
                }

                var sceneBundlePath = $"{environmentDirectory}/{(ProjectData.IsMobileClient ? "android_bundle" : "bundle")}";
                LoadSceneBundle(sceneBundlePath);
            };

            requestJson.OnError += responseJson => { ShowErrorLoadScene(); };

            void LoadSceneBundle(string bundlePath)
            {
                var request = new RequestLoadSceneFromFile(sceneName, bundlePath, null, isLoadingScene);
                request.OnFinish += response1 =>
                {
                    var response = (ResponseAsset) response1;
                    string scenePath = Path.GetFileNameWithoutExtension(response.Path);

                    if (!isLoadingScene)
                    {
                        ProjectDataListener.Instance.LoadScene(scenePath);
                    }
                    else
                    {
                        ProjectDataListener.AddLoadingScene(RequiredProjectArguments.ProjectConfigurationId, scenePath);
                    }
                };

                request.OnError += s =>
                {
                    string message = ErrorHelper.GetErrorDescByCode(ErrorCode.EnvironmentNotFoundError);
                    string details = "Scene template is not loaded from storage! " + s;
                    CoreErrorManager.Error(new Exception(message));
                    Debug.LogError(details);
                    LauncherErrorManager.Instance.ShowFatal(message, details + Environment.NewLine + Environment.StackTrace);
                };
            }
        }

        private void ShowError(string message)
        {
            FeedBackText = message;
            Debug.LogError(message);
            FeedBackText = message;
            RequestManager.Instance.StopRequestsWithError(message);
            NotificationWindowManager.Show(message, 60f);
        }
    }
}