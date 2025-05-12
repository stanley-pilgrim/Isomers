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
    public class StorageLoader : BaseLoader
    {
        private const string KtxFormat = ".ktx";
        
        public override void LoadObjects(List<PrefabObject> objects)
        {
            ResetObjectsCounter(objects.Count);

            foreach (PrefabObject prefabObject in objects)
            {
                LoadPrefabObject(prefabObject);
            }
        }

        public override void LoadResources(List<ResourceDto> resources)
        {
            ResetResourcesCounter(resources.Count);
            
            foreach (ResourceDto resourceDto in resources)
            {
                LoadResource(resourceDto);
            }
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
                var standaloneManifest = ProjectData.IsLinux()
                    ? Path.Combine(prefabObject.Assets, $"linux_{pathToPart}.manifest")
                    : Path.Combine(prefabObject.Assets, $"{pathToPart}.manifest");
                
                var requestManifest = new RequestUri(ProjectData.IsMobileClient
                    ? Path.Combine(prefabObject.Assets, $"android_{pathToPart}.manifest")
                    : standaloneManifest);
                
                requestManifest.OnFinish += response =>
                {
                    var responseManifest = (ResponseUri) response;
                    
                    Hash128 bundleHash = Hash128.Compute(responseManifest.TextData);

                    if (GameStateData.LoadedAssetBundleParts.Contains(bundleHash))
                    {
                        return;
                    }

                    GameStateData.LoadedAssetBundleParts.Add(bundleHash);

                    var standaloneUri = ProjectData.IsLinux()
                        ? Path.Combine(prefabObject.Assets, $"linux_{pathToPart}")
                        : Path.Combine(prefabObject.Assets, $"{pathToPart}");
                    
                    var assetBundleUri = ProjectData.IsMobileClient
                        ? Path.Combine(prefabObject.Assets, $"android_{pathToPart}")
                        : standaloneUri;

                    var requestAsset = new RequestLoadAssetFromFile(assetInfo.AssetName, assetBundleUri, new object[] {prefabObject, assetInfo});

                    requestAsset.OnFinish += responseAsset =>
                    {
                        Debug.Log($"Resources loaded for {assetInfo.AssetName}");
                    };
                };
            }
        }

        public override void LoadSceneTemplate(int sceneTemplateId, bool isLoadingScene)
        {
            SceneTemplatePrefab sceneTemplate = ProjectData.ProjectStructure.SceneTemplates.GetProjectScene(sceneTemplateId);
            Debug.Log($"Loading scene template \"{sceneTemplate.GetLocalizedName()}\" from tar file");
            if (!sceneTemplate.LinuxReady && ProjectData.IsLinux())
            {
                InvokeWindowsOnLinuxBundleLoaded(sceneTemplate);
            }
            
            var requestConfig = new RequestUri(sceneTemplate.ConfigResource);
            requestConfig.OnFinish += responseConfig =>
            {
                var sceneData = ((ResponseUri) responseConfig).TextData.JsonDeserialize<SceneData>();
                
                var standaloneSceneTemplateManifestUri = sceneTemplate.LinuxReady && ProjectData.IsLinux()
                    ? sceneTemplate.LinuxBundleResource
                    : sceneTemplate.BundleResource; 
                
                var sceneUri = Settings.Instance.StoragePath + (ProjectData.IsMobileClient
                    ? sceneTemplate.AndroidBundleResource
                    : standaloneSceneTemplateManifestUri);
                
                var dllNames = sceneData.DllNames.Select(x => (string) x).ToArray();
                sceneTemplate.HasScripts = dllNames.Any();
                foreach (string dllName in dllNames)
                {
                    LoadDll(sceneTemplate, dllName);
                }

                var request = new RequestLoadSceneFromFile(sceneData.Name, sceneUri, null, isLoadingScene);
                request.OnFinish += response1 =>
                {
                    var responseAsset = (ResponseAsset) response1;
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

                request.OnError += Helper.ShowErrorLoadScene;
            };
        }

        public override void LoadProjectStructure(int projectId, Action<ProjectStructure> onFinish)
        {
            var request = new RequestUri("/index.json");

            request.OnFinish += response =>
            {
                string jsonWorldStructure = ((ResponseUri) response).TextData;
                ProjectStructure = jsonWorldStructure.JsonDeserialize<ProjectStructure>();

                foreach (var scene in ProjectStructure.Scenes)
                {
                    string logicFilePath = Settings.Instance.StoragePath + scene.LogicResource;

                    if (!File.Exists(logicFilePath))
                    {
                        continue;
                    }
                    
                    var logicRequest = new RequestUri(scene.LogicResource);

                    logicRequest.OnFinish += response1 =>
                    {
                        ResponseUri logicResponse = (ResponseUri) response1;
                        scene.AssemblyBytes = logicResponse.ByteData;
                    };
                }

                onFinish.Invoke(ProjectStructure);
            };

            request.OnError += s =>
            {
                LauncherErrorManager.Instance.ShowFatal(ErrorHelper.GetErrorDescByCode(ErrorCode.LoadSceneError), null);
                FeedBackText = $"Can't find project folder";
            };
        }

        public override void LoadObject(PrefabObject prefabObject, Action<PrefabObject> callback) => LoadPrefabObject(prefabObject, callback);

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
                    string mainAssembly = FindMainAssembly( prefabObject);

                    foreach (string dllName in prefabObject.AssetInfo.Assembly.Where(dllName => dllName != mainAssembly))
                    {
                        LoadDll(prefabObject, dllName);
                    }

                    if (!string.IsNullOrEmpty(mainAssembly))
                    {
                        LoadDll(prefabObject, mainAssembly);
                    }
                }

                LoadAssetFromStorage(prefabObject, callback);
            };

            request.OnError += s =>
            {
                FeedBackText = $"Load prefab object \"{prefabObject.Name.en}\" error:\n{s}";
                Helper.ShowFatalErrorLoadObject(prefabObject, s);
            };
        }

        protected override void LoadMeshResource(ResourceDto resource)
        {
            var path = $"file:///{Settings.Instance.StoragePath}{resource.Assets}";
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
            var url = $"file:///{Settings.Instance.StoragePath}{resource.Path}{KtxFormat}";
            
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
            var url = $"file:///{Settings.Instance.StoragePath}{resource.Path}";

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
            var requestFileRead = new RequestUri(resource.Path, null, true);

            requestFileRead.OnFinish += response =>
            {
                var responseFileRead = (ResponseUri) response;
                UpdateResourceEntity(resource, new TextAsset(responseFileRead.TextData));
            };
                
            requestFileRead.OnError += message => LoadResourceDefaultErrorHandler(resource, message);
        }

        protected override void LoadAudioResource(ResourceDto resource)
        {
            var path = $"file:///{Settings.Instance.StoragePath}{resource.Path}";
            var request = new RequestAudio(path, resource.StreamAudio);
            
            request.OnFinish += response =>
            {
                var responseAudio = (ResponseAudio) response;
                UpdateResourceEntity(resource, responseAudio.AudioClip);
            };

            request.OnError += message => LoadResourceDefaultErrorHandler(resource, message);
        }
        
        protected override void LoadVideoResource(ResourceDto resource)
        {
            var path = $"file:///{Settings.Instance.StoragePath}{resource.Path}";
            var request = new RequestVideo(path);
            
            request.OnFinish += response =>
            {
                var responseVideo = (ResponseVideo) response;
                UpdateResourceEntity(resource, responseVideo.VideoUrl);
            };

            request.OnError += message => LoadResourceDefaultErrorHandler(resource, message);
        }
        
        private void LoadDll(PrefabObject o, string dllName)
        {
            string dllCachePath = FileSystemUtils.GetFilesPath(ProjectData.IsMobileClient, "cache/dll/")
                                  + o.Name.en
                                  + o.Guid;

            string dllPath = o.Assets + "/" + dllName;

            new RequestUri(dllPath).OnFinish += response1 =>
            {
                var byteData = ((ResponseUri) response1).ByteData;

                if (AddAssembly(dllCachePath, dllName, ref byteData, out Exception e))
                {
                    return;
                }

                var message = $"{ErrorHelper.GetErrorDescByCode(ErrorCode.LoadObjectError)}: {o.Name} ({o.GetLocalizedName()})\n{e}";
                
                FeedBackText = message;
                Debug.LogError(message);
                FeedBackText = message;
                RequestManager.Instance.StopRequestsWithError(message);
            };
        }

        private void LoadDll(SceneTemplatePrefab s, string dllName)
        {
            string dllCachePath = $"{FileSystemUtils.GetFilesPath(ProjectData.IsMobileClient, "cache/dll/")}{s.Guid}";
            
            string dllPath = $"{s.Assets}/{dllName}";

            new RequestUri(dllPath).OnFinish += response1 =>
            {
                var byteData = ((ResponseUri) response1).ByteData;

                if (AddAssembly(dllCachePath, dllName, ref byteData, out Exception e))
                {
                    return;
                }

                var message = $"{ErrorHelper.GetErrorDescByCode(ErrorCode.LoadSceneError)}: {s.Name} ({s.GetLocalizedName()})\n{e}";
                
                FeedBackText = message;
                Debug.LogError(message);
                FeedBackText = message;
                RequestManager.Instance.StopRequestsWithError(message);
            };
        }

        private void LoadAssetFromStorage(PrefabObject prefabObject, Action<PrefabObject> callback = null)
        {
            var standaloneBundleUriManifest = prefabObject.LinuxReady && ProjectData.IsLinux() 
                ? prefabObject.LinuxBundleManifest 
                : prefabObject.BundleManifest;
            
            var requestManifest = new RequestUri(ProjectData.IsMobileClient
                ? prefabObject.AndroidBundleManifest
                : standaloneBundleUriManifest);

            requestManifest.OnFinish += responseManifest =>
            {
                var assetName = prefabObject.AssetInfo.AssetName;
                if (!prefabObject.LinuxReady && ProjectData.IsLinux())
                {
                    InvokeWindowsOnLinuxBundleLoaded(prefabObject);
                }
            
                var standaloneBundleUri = prefabObject.LinuxReady && ProjectData.IsLinux() 
                    ? prefabObject.LinuxBundleResource 
                    : prefabObject.BundleResource;
            
                var assetBundleUri = ProjectData.IsMobileClient 
                    ? prefabObject.AndroidBundleResource 
                    : standaloneBundleUri;
            
                var requestAsset = new RequestLoadAssetFromFile(assetName, assetBundleUri, new object[] {prefabObject, prefabObject.AssetInfo});

                requestAsset.OnFinish += response =>
                {
                    FeedBackText = LanguageManager.Instance.GetTextValue("LOADING")
                                   + " "
                                   + prefabObject.GetLocalizedName()
                                   + "...";
                    CreatePrefabEntity(response, prefabObject, callback: callback);
                };
            };
        }
    }
}
