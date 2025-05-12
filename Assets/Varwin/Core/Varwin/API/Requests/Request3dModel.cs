using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assimp;
using ICSharpCode.SharpZipLib.Zip;
using Varwin.Data;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Varwin.WWW
{
    public class Request3dModel : Request
    {
        public Request3dModel(string uri)
        {
            Uri = uri;
            RunInParallel = false;
            RequestManager.AddRequest(this);
        }

        protected override IEnumerator SendRequest()
        {
            if (Cache.ContainsKey(Uri))
            {
                var response = new Response3dModel {GameObject = Cache[Uri]};
                (this as IRequest).OnResponseDone(response);
            }
            else
            {
                string tempFolder = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString());

                string contentZipSourcePath = Path.Combine(Uri, "content.zip");
                string contentZipDestPath = Path.Combine(tempFolder, "content.zip");
                
                using (UnityWebRequest webRequest = UnityWebRequest.Get(contentZipSourcePath))
                using (var downloadHandler = new DownloadHandlerFile(contentZipDestPath))
                {
                    downloadHandler.removeFileOnAbort = true;
                    

                    if (!string.IsNullOrEmpty(AccessToken))
                    {
                        webRequest.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
                    }
                    
                    var clientId = SocketId?.ToString();
                    if (!string.IsNullOrEmpty(clientId))
                    {
                        webRequest.SetRequestHeader("X-Socket-ID", clientId);
                    }

                    webRequest.downloadHandler = downloadHandler;
                    yield return webRequest.SendWebRequest();

                    if (webRequest.HasError())
                    {
                        ((IRequest) this).OnResponseError($"3D Model can not be loaded due to web request error: {webRequest.error}\n{Uri}");
                        yield break;
                    }
                }

                var fastZip = new FastZip();
                fastZip.ExtractZip(contentZipDestPath, tempFolder, null);

                string installDataSourcePath = Path.Combine(Uri, "install.json");
                string installDataDestPath = Path.Combine(tempFolder, "install.json");
                using (UnityWebRequest webRequest = UnityWebRequest.Get(installDataSourcePath))
                using (var downloadHandler = new DownloadHandlerFile(installDataDestPath))
                {
                    downloadHandler.removeFileOnAbort = true;
                    
                    if (!string.IsNullOrEmpty(AccessToken))
                    {
                        webRequest.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
                    }
                    
                    var clientId = SocketId?.ToString();
                    if (!string.IsNullOrEmpty(clientId))
                    {
                        webRequest.SetRequestHeader("X-Socket-ID", clientId);
                    }

                    webRequest.downloadHandler = downloadHandler;
                    yield return webRequest.SendWebRequest();

                    if (webRequest.HasError())
                    {
                        (this as IRequest).OnResponseError($"3D Model can not be loaded due to web request error: {webRequest.error}\n{Uri}");
                        yield break;
                    }
                }

                string installDataJson = File.ReadAllText(installDataDestPath);
                var installData = JsonUtility.FromJson<InstallData>(installDataJson);

                string modelPath = Path.Combine(tempFolder, $"model.{installData.Format}");

                try
                {
                    AssimpModelContainer assetLoaderContext = AssimpImporter.Load(modelPath);
                    GameObject gameObject = assetLoaderContext.gameObject;
                    gameObject.transform.parent = CacheRoot.transform;
                    Cache.Add(Uri, gameObject);

                    var response = new Response3dModel {GameObject = gameObject};
                    (this as IRequest).OnResponseDone(response);
                }
                catch (Exception e)
                {
                    (this as IRequest).OnResponseError($"3d Model can not be loaded {e}");
                }

                yield return true;
            }
        }

        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        private static GameObject _cacheRoot;

        private static GameObject CacheRoot
        {
            get
            {
                if (!_cacheRoot)
                {
                    _cacheRoot = new GameObject("3d_models_cache");
                    _cacheRoot.SetActive(false);
                    Object.DontDestroyOnLoad(_cacheRoot);
                }

                return _cacheRoot;
            }
        }

        public static void ClearCache()
        {
            if (Cache == null)
            {
                return;
            }
            
            foreach (var cacheItem in Cache.Where(cacheItem => cacheItem.Value))
            {
                Object.Destroy(cacheItem.Value);
            }
                
            Cache.Clear();
        }

        [Serializable]
        private class InstallData
        {
            public string Name;

            public string Guid;

            public string Format;
        }
    }
}