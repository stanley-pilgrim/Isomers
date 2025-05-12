using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SmartLocalization;
using UnityEngine;
using Varwin.Core;
using Varwin.Data.ServerData;
using Varwin.Log;
using Varwin.Public;
using Varwin.UI;
using Varwin.UI.VRErrorManager;
using Varwin.WWW;
using Object = UnityEngine.Object;

namespace Varwin.Data
{
    public abstract class BaseLoader
    {
        #region ABSTRACT METHODS

        /// <summary>
        /// Load list of objects
        /// </summary>
        /// <param name="objects">Prefab object data with resources links</param>
        public abstract void LoadObjects(List<PrefabObject> objects);

        /// <summary>
        /// Load a prefab object
        /// </summary>
        /// <param name="prefabObject"></param>
        /// <param name="callback"></param>
        public abstract void LoadObject(PrefabObject prefabObject, Action<PrefabObject> callback);

        /// <summary>
        /// Load list of objects
        /// </summary>
        /// <param name="resources">Resources data</param>
        public abstract void LoadResources(List<ResourceDto> resources);

        protected abstract void LoadTextureResource(ResourceDto resource);
        protected abstract void LoadMeshResource(ResourceDto resource);
        protected abstract void LoadTextResource(ResourceDto resource);
        protected abstract void LoadAudioResource(ResourceDto resource);
        protected abstract void LoadVideoResource(ResourceDto resource);

        protected abstract void LoadAssetBundleParts(PrefabObject o);

        /// <summary>
        /// Load scene asset
        /// </summary>
        /// <param name="sceneTemplateId">Scene template Id (Scene template prefab)</param>
        /// <param name="isLoadingScene">Load scene as loading scene</param>
        public abstract void LoadSceneTemplate(int sceneTemplateId, bool isLoadingScene);

        /// <summary>
        /// Load/Get world structure data
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="onFinish"></param>
        public abstract void LoadProjectStructure(int projectId, Action<ProjectStructure> onFinish);

        #endregion

        #region PROTECTED FIELDS

        /// <summary>
        /// Count of current loaded (spawn ready) prefabs (LoadObjectsCounter.cs)
        /// </summary>
        protected static GameEntity LoadObjectsCounter { get; private set; }

        /// <summary>
        /// Count of current loaded (spawn ready) prefabs (LoadObjectsCounter.cs)
        /// </summary>
        protected static GameEntity LoadResourcesCounter { get; private set; }

        /// <summary>
        /// Count of current loaded assets 
        /// </summary>
        protected int CountLoadedObjects;

        /// <summary>
        /// Count of current loaded assets 
        /// </summary>
        protected int CountLoadedResources;

        /// <summary>
        /// Current loading project structure
        /// </summary>
        protected ProjectStructure ProjectStructure { get; set; }

        #endregion

        #region PUPLIC PROPERIES

        /// <summary>
        /// Current loading required project arguments
        /// </summary>
        public RequiredProjectArguments RequiredProjectArguments { get; set; }

        #endregion


        /// <summary>
        /// Loader processes realtime feedback
        /// </summary>
        public string FeedBackText { get; set; }

        /// <summary>
        /// Write assembly to disk and load
        /// </summary>
        /// <param name="dllPath">Path to write dll assembly</param>
        /// <param name="dllName">Dll file name</param>
        /// <param name="byteData">Assembly bytes</param>
        /// <param name="exception">Exception</param>
        /// <returns>Success</returns>
        protected bool AddAssembly(string dllPath, string dllName, ref byte[] byteData, out Exception exception)
        {
            exception = null;
            
            dllPath = dllPath.ClearString(Path.GetInvalidPathChars());
            var drive = dllPath.Substring(0, 3);
            var pathWithoutDrive = dllPath.Substring(3, dllPath.Length - 3).Replace(":", "");
            dllPath = drive + pathWithoutDrive;
            
            dllName = dllName.ClearString(Path.GetInvalidFileNameChars());

            if (!Directory.Exists(dllPath))
            {
                Directory.CreateDirectory(dllPath);
            }

            string dllFileName = $"{dllPath}/{dllName}";

            try
            {
                if (dllFileName.Length > byte.MaxValue && (Application.isEditor || Application.platform == RuntimePlatform.WindowsPlayer))
                {
                    const string extendedLengthPathPrefix = @"\\?\";
                    dllFileName = extendedLengthPathPrefix + dllFileName;
                }
                
                File.WriteAllBytes(dllFileName, byteData);
            }
            catch (Exception e)
            {
                exception = e;
                Debug.LogError($"Can not write assembly \"{dllName}\" to cache\n{e}");
                FeedBackText = e.ToString();
                FeedBackText = e.Message;
                return false;
            }

            try
            {
                Debug.Log($"<Color=Cyan>Loading assembly \"{dllName}\"</Color>");
                Assembly assembly = Assembly.Load(byteData);
                GameStateData.AddAssembly(dllName, assembly);

                Debug.Log($"<Color=Cyan>Assembly \"{dllName}\" is loaded</Color>");
                return true;
            }
            catch (Exception e)
            {
                exception = e;
                Debug.LogError($"Can not load assembly \"{dllName}\"\n{e}");
                FeedBackText = e.ToString();
                FeedBackText = e.Message;
                return false;
            }
        }

        /// <summary>
        /// Sort objects if already loaded
        /// </summary>
        /// <param name="objects"></param>
        /// <returns></returns>
        public List<PrefabObject> GetRequiredObjects(List<PrefabObject> objects)
        {
            var result = new List<PrefabObject>(objects);
            var alreadyLoaded = GameStateData.GetPrefabsData();
            var resultRemove = new List<PrefabObject>();

            foreach (PrefabObject o in result)
            {
                PrefabObject found = alreadyLoaded.Find(prefabObject => prefabObject.Id == o.Id);

                if (found != null)
                {
                    PrefabObject loaded = result.Find(prefabObject => prefabObject.Id == o.Id);
                    resultRemove.Add(loaded);
                    Debug.Log(o.Name.en + " is already loaded");
                }
                else
                {
                    Debug.Log(o.Name.en + " is required");
                }

                Debug.Log(o.Name.en + " was added to ProjectStructure.SceneObjects");
            }

            foreach (PrefabObject o in resultRemove)
            {
                result.Remove(o);
            }

            return result;
        }

        public List<ResourceDto> GetRequiredResources(IEnumerable<ResourceDto> resources)
        {
            var result = new List<ResourceDto>(resources);
            var alreadyLoaded = GameStateData.GetResourcesData();
            var resultRemove = new List<ResourceDto>();

            foreach (ResourceDto o in result)
            {
                ResourceDto found = alreadyLoaded.Find(resourceDto => resourceDto.Id == o.Id);

                if (found != null)
                {
                    var loaded = result.Find(resourceDto => resourceDto.Id == o.Id);
                    resultRemove.Add(loaded);
                    Debug.Log(o.Name + " is already loaded");
                }
                else
                {
                    Debug.Log(o.Name + " is required");
                }

                Debug.Log(o.Name + " was added to ProjectStructure.Resources");
            }

            foreach (ResourceDto o in resultRemove)
            {
                result.Remove(o);
            }

            return result;
        }

        protected static string FindMainAssembly(PrefabObject prefabObject)
        {
            try
            {
                string pattern = (prefabObject?.RootGuid ?? prefabObject.AssetInfo.AssetName).Replace("-", string.Empty);
                return prefabObject.AssetInfo.Assembly.FirstOrDefault(match => Regex.Match(match, pattern).Success) ?? prefabObject.AssetInfo.Assembly.Last();
            }
            catch (Exception e)
            {
                Debug.LogError($"Can not find main assembly for object \"{prefabObject.Name}\"\n{e}");
                return null;
            }
        }

        protected void ResetObjectsCounter(int count)
        {
            ProjectData.ObjectsAreLoaded = false;
            FeedBackText = LanguageManager.Instance.GetTextValue("LOADING") + " " + LanguageManager.Instance.GetTextValue("SCENE_OBJECTS");
            CountLoadedObjects = 0;

            if (LoadObjectsCounter != null && LoadObjectsCounter.hasLoadObjectsCounter)
            {
                LoadObjectsCounter.ReplaceLoadObjectsCounter(count, 0, false);
            }
            else
            {
                LoadObjectsCounter = Contexts.sharedInstance.game.CreateEntity();
                LoadObjectsCounter.AddLoadObjectsCounter(count, 0, false);
            }
        }

        protected void ResetResourcesCounter(int count)
        {
            ProjectData.ResourcesAreLoaded = false;
            FeedBackText = LanguageManager.Instance.GetTextValue("LOADING") + " " + LanguageManager.Instance.GetTextValue("SCENE_OBJECTS");
            CountLoadedResources = 0;

            if (LoadResourcesCounter != null && LoadResourcesCounter.hasLoadResourcesCounter)
            {
                LoadResourcesCounter.ReplaceLoadResourcesCounter(count, 0, false);
            }
            else
            {
                LoadResourcesCounter = Contexts.sharedInstance.game.CreateEntity();
                LoadResourcesCounter.AddLoadResourcesCounter(count, 0, false);
            }
        }

        protected bool LoadAssetInfo(string json, PrefabObject prefabObject)
        {
            var launcherErrorManager = LauncherErrorManager.Instance;
            var vrErrorManager = VRErrorManager.Instance;

            try
            {
                prefabObject.AssetInfo = json.JsonDeserialize<AssetInfo>();

                if (!string.IsNullOrEmpty(prefabObject.AssetInfo.AssetName))
                {
                    return true;
                }

                string message = $"Asset name can not be null. {prefabObject.Name} Bundle.json is not actual version!";
                FeedBackText = message;
                Debug.Log(message);
                RequestManager.Instance.StopRequestsWithError(message);
                string errorMsg = $"{prefabObject.Name} not actual!";

                if (launcherErrorManager)
                {
                    launcherErrorManager.ShowFatal(errorMsg, "null asset");
                }

                if (vrErrorManager)
                {
                    vrErrorManager.ShowFatal(errorMsg, "null asset");
                }

                return false;
            }
            catch (Exception e)
            {
                string message = $"AssetInfo can not be loaded. {prefabObject.Name.en} Bundle.json is not actual version! Bundle.json = {json}";
                FeedBackText = message;
                Debug.Log(message);
                RequestManager.Instance.StopRequestsWithError(message);

                if (launcherErrorManager)
                {
                    launcherErrorManager.ShowFatal($"{prefabObject.Name.en} not actual!", e.ToString());
                }

                if (vrErrorManager)
                {
                    vrErrorManager.ShowFatal($"{prefabObject.Name.en} not actual!", e.StackTrace);
                }

                return false;
            }
        }

        private void CreateResourceEntity(ResourceDto resource, object resourceValue)
        {
            if (GameStateData.ResourceDataIsLoaded(resource.Guid))
            {
                LoadResourcesCounter.loadResourcesCounter.ResoursesLoaded++;
                Debug.Log($"{resource.Name} was ignored, because it already loaded");
                return;
            }

            var resourceObject = new ResourceObject
            {
                Data = resource,
                Value = resourceValue
            };

            GameEntity entity = Contexts.sharedInstance.game.CreateEntity();
            entity.AddResource(resourceObject);

            GameStateData.AddResourceObject(resourceObject, resource, entity);

            LoadResourcesCounter.loadResourcesCounter.ResoursesLoaded++;
            Debug.Log($"Resourse {resource.Name} is loaded");

            GCManager.Collect();

            if (resourceValue != null)
            {
                ResourceLoaded?.Invoke(resource, resourceValue);
            }
            else
            {
                ResourceUnloaded?.Invoke(resource);
            }
        }

        protected void UpdateResourceEntity(ResourceDto resource, object resourceValue)
        {
            ResourceObject resourceObject = GameStateData.GetResource(resource.Guid);

            if (resourceObject == null)
            {
                CreateResourceEntity(resource, resourceValue);
                return;
            }

            object oldValue = resourceObject.Value;
            resourceObject.Value = resourceValue;

            if (resourceValue != null)
            {
                try
                {
                    ResourceLoaded?.Invoke(resource, resourceValue);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Can't invoke ResourceLoaded event for {resource.Name}\n{e}");
                }
            }
            else if (oldValue != null)
            {
                try
                {
                    ResourceUnloaded?.Invoke(resource);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Can't invoke ResourceUnloaded event for {resource.Name}\n{e}");
                }

                if (oldValue is Object oldValueObject)
                {
                    Object.Destroy(oldValueObject);
                }
            }

            GCManager.Collect();
        }

        protected void CreatePrefabEntity(IResponse response, PrefabObject prefabObject, Sprite icon = null, Action<PrefabObject> callback = null)
        {
            Debug.Log($"CreatePrefabEntity: \"{prefabObject.Name}\"; {LoadObjectsCounter.loadObjectsCounter.PrefabsLoaded}/{LoadObjectsCounter.loadObjectsCounter.PrefabsCount}");
            
            var alreadyLoaded = GameStateData.GetPrefabData(prefabObject.Id);

            if (alreadyLoaded != null)
            {
                LoadObjectsCounter.loadObjectsCounter.PrefabsLoaded++;
                Debug.Log($"CreatePrefabEntity: Object \"{prefabObject.Name}\" was ignored, because it already loaded; {LoadObjectsCounter.loadObjectsCounter.PrefabsLoaded}/{LoadObjectsCounter.loadObjectsCounter.PrefabsCount}");
                return;
            }

            var responseAsset = (ResponseAsset) response;
            var unityObject = responseAsset.Asset;
            var serverObject = (PrefabObject) responseAsset.UserData[0];

            GameEntity entity = Contexts.sharedInstance.game.CreateEntity();
            entity.AddServerObject(serverObject);

            GameStateData.AddPrefabGameObject(responseAsset.Asset, prefabObject);

            if (icon)
            {
                GameStateData.AddObjectIcon(serverObject.Id, icon);
                entity.AddIcon(icon);
            }

            if (prefabObject.Embedded)
            {
                GameStateData.AddToEmbeddedList(serverObject.Id);
            }

            var gameObject = unityObject as GameObject;
            if (gameObject)
            {
                entity.AddGameObject(gameObject);
            }
            else
            {
                var message = $"CreatePrefabEntity: Game object is null in asset \"{prefabObject.Name}\"; {LoadObjectsCounter.loadObjectsCounter.PrefabsLoaded}/{LoadObjectsCounter.loadObjectsCounter.PrefabsCount}";
                FeedBackText = message;
                Debug.LogError(message);
                NotificationWindowManager.Show(message, 60f);
                RequestManager.Instance.StopRequestsWithError(message);
                return;
            }

            LoadObjectsCounter.loadObjectsCounter.PrefabsLoaded++;
            callback?.Invoke(prefabObject);
            Debug.Log($"CreatePrefabEntity: \"{prefabObject.Name}\" is loaded; {LoadObjectsCounter.loadObjectsCounter.PrefabsLoaded}/{LoadObjectsCounter.loadObjectsCounter.PrefabsCount}");

            GCManager.Collect();
        }

        protected SceneTemplatePrefab GetSceneTemplatePrefab(int sceneTemplateId)
        {
            SceneTemplatePrefab sceneTemplate = ProjectStructure.SceneTemplates.GetProjectScene(sceneTemplateId);

            if (sceneTemplate == null)
            {
                string message = I18next.Format(ErrorHelper.GetErrorDescByCode(ErrorCode.EnvironmentNotFoundError), new KeyValuePair<string, object>("error", "???"));
                FeedBackText = message;
                Debug.LogError(message);
                RequestManager.Instance.StopRequestsWithError(message);

                return null;
            }
            else
            {
                string message = I18next.Format(LanguageManager.Instance.GetTextValue("LOADING_SCENE_TEMPLATE"), new KeyValuePair<string, object>("name", sceneTemplate.GetLocalizedName()));
                Debug.Log(message);
                FeedBackText = message;
            }

            return sceneTemplate;
        }

        protected void ShowErrorLoadScene()
        {
            string message = ErrorHelper.GetErrorDescByCode(ErrorCode.LoadSceneError);

            FeedBackText = message;
            Debug.LogError("Scene template is not loaded");
            RequestManager.Instance.StopRequestsWithError(message);
        }

        public void LoadResource(ResourceDto resource)
        {
            var resourceOnDemandUnloadingRequired = resource.OnDemand && !resource.ForceLoad;
            if (resourceOnDemandUnloadingRequired)
            {
                GameStateData.LoadedResources.Remove(resource.Guid);
                UpdateResourceEntity(resource, null); 
                return;
            }

            if (!GameStateData.LoadedResources.Add(resource.Guid))
            {
                return;
            }
            
            var resourceObject = GameStateData.GetResource(resource.Guid);
            if (resourceObject?.Value != null)
            {
                return;
            }

            if (resource.IsModel())
            {
                LoadMeshResource(resource);
            }
            else if (resource.IsPicture())
            {
                LoadTextureResource(resource);
            }
            else if (resource.IsText())
            {
                LoadTextResource(resource);
            }
            else if (resource.IsAudio())
            {
                LoadAudioResource(resource);
            }
            else if (resource.IsVideo())
            {
                LoadVideoResource(resource);
            }
        }

        protected void LoadResourceDefaultErrorHandler(ResourceDto resource, string message)
        {
            string feedBack = LanguageManager.Instance.GetTextValue("LOAD_RESOURCE_ERROR") + " " + resource.Name + "\n" + message;
            Debug.LogError(feedBack);
            FeedBackText = feedBack;
            RequestManager.Instance.StopRequestsWithError(feedBack);
        }

        public event Action<ResourceDto, object> ResourceLoaded;
        public event Action<ResourceDto> ResourceUnloaded;
        public event Action<object> WindowsOnLinuxBundleLoaded;

        protected void InvokeWindowsOnLinuxBundleLoaded(object resource)
        {
            WindowsOnLinuxBundleLoaded?.Invoke(resource);
        }
    }
}
