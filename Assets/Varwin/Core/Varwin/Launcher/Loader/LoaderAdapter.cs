using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SmartLocalization;
using UnityEngine;
using Varwin.Data;
using Varwin.Data.ServerData;
using Varwin.PlatformAdapter;
using Varwin.Public;
using Varwin.PUN;
using Object = UnityEngine.Object;

namespace Varwin
{
    public static class LoaderAdapter
    {
        public static BaseLoader Loader { get; private set; }

        public delegate void ProgressUpdate(float val);

        public static ProgressUpdate OnLoadingUpdate;
        public static ProgressUpdate OnDownLoadUpdate;
        public static Type LoaderType => Loader?.GetType();
        public static string FeedBackText => Loader.FeedBackText;
        private static RequiredProjectArguments _requiredProjectArguments;

        private static List<PrefabObject> UsingObjects { get; set; }

        private static bool Initialized { get; set; }

        private const string AutoLanguage = "auto";

        public static bool IsLoadingProcess { get; private set; } = false;

        public static void Init(BaseLoader loader)
        {
            if (Initialized)
            {
                return;
            }

            Loader = loader;

            Initialized = true;

            Debug.Log($"<Color=Magenta>Loader adapter with {loader.GetType().Name} initialized...</Color>");
        }

        public static void Reset()
        {
            Loader = null;
            Initialized = false;
        }

        public static void LoaderFeedBackText(string message)
        {
            Loader.FeedBackText = message;
        }

        public static void LoadResources(ResourceDto resource)
        {
            LoadResources(new List<ResourceDto> {resource});
        }

        public static void LoadResources(IEnumerable<ResourceDto> resources)
        {
            List<ResourceDto> requiredResources = Loader.GetRequiredResources(resources);
            Debug.Log("<Color=Olive><b>Load Resources started...</b></Color>");
            Loader.LoadResources(requiredResources);
        }

        public static void LoadPrefabObjects(List<PrefabObject> objects)
        {
            List<PrefabObject> requiredObjects = Loader.GetRequiredObjects(objects);
            Debug.Log("<Color=Olive><b>Load SceneObjects started...</b></Color>");
            Loader.LoadObjects(requiredObjects);
        }

        public static void LoadPrefabObject(PrefabObject prefabObject, Action<PrefabObject> callback)
        {
            Loader.LoadObject(prefabObject, callback);
        }

        public static void LoadProjectStructure(int projectId, Action<ProjectStructure> onFinish)
        {
            Loader.LoadProjectStructure(projectId, onFinish);
        }

        private static void LoadSceneTemplate(int sceneTemplateId)
        {
            Debug.Log("<Color=Olive><b>Load Scene started...</b></Color>");
            Loader.LoadSceneTemplate(sceneTemplateId, false);
        }

        private static void CheckSceneId()
        {
            if (_requiredProjectArguments.SceneId != 0 || ProjectData.ProjectStructure == null)
            {
                return;
            }

            if (_requiredProjectArguments.ProjectConfigurationId == 0)
            {
                if (ProjectData.ProjectStructure.ProjectConfigurations.Count > 0)
                {
                    int sceneId = ProjectData.ProjectStructure.ProjectConfigurations[0].StartSceneId.HasValue
                        ? ProjectData.ProjectStructure.ProjectConfigurations[0].StartSceneId.Value
                        : 0;

                    if (sceneId == 0)
                    {
                        sceneId = ProjectData.ProjectStructure.Scenes[0].Id;
                    }

                    _requiredProjectArguments.SceneId = sceneId;
                    return;
                }
            }

            ProjectConfiguration configuration =
                ProjectData.ProjectStructure.ProjectConfigurations.GetProjectConfigurationByProjectScene(_requiredProjectArguments.ProjectConfigurationId);

            if (configuration == null)
            {
                return;
            }

            int startSceneId = configuration.StartSceneId ?? 0;
            _requiredProjectArguments.SceneId = startSceneId != 0 ? startSceneId : ProjectData.ProjectStructure.Scenes[0].Id;
        }

        private static bool _isOtherScene = true;

        public static void LoadProjectScene(bool force = false)
        {
            IsLoadingProcess = true;
            if (ProjectData.ProjectStructure != null && _requiredProjectArguments.SceneId == 0)
            {
                CheckSceneId();
            }

            _isOtherScene = ProjectData.ProjectStructure == null
                            || ProjectData.ProjectId != _requiredProjectArguments.ProjectId
                            || ProjectData.SceneId != _requiredProjectArguments.SceneId
                            || (ProjectData.SceneId == _requiredProjectArguments.SceneId && ProjectData.IsRichSceneTemplate);

            _requiredProjectArguments.ForceReloadProject = force;
            _isOtherScene |= force;

            bool isOtherMode = ProjectData.ProjectStructure != null && (ProjectData.GameMode != _requiredProjectArguments.GameMode);

            if (isOtherMode && !_isOtherScene)
            {
                if (ProjectData.GameMode != GameMode.Edit && _requiredProjectArguments.GameMode == GameMode.Edit)
                {
                    Helper.ReloadSceneObjects();
                }

                ProjectData.GameMode = _requiredProjectArguments.GameMode;
                ProjectDataListener.ReadyToGetNewMessages();
                IsLoadingProcess = false;
                ProjectData.PlatformMode = _requiredProjectArguments.PlatformMode;
                return;
            }

            if (!_isOtherScene)
            {
                CheckSceneId();
            }

            Debug.Log($"<Color=Olive><b>Other scene: {_isOtherScene}</b></Color>");

            ProjectData.SceneCleared += OnSceneCleared;
            ProjectDataListener.Instance.BeforeSceneLoaded(_isOtherScene);
            ProjectData.SceneLoaded += DisableAudioListenersOnScene;

            void OnSceneCleared()
            {
                ProjectData.SceneCleared -= OnSceneCleared;
                ProjectData.ObjectsLoaded += PrefabObjectsLoaded;

                if (_requiredProjectArguments.ForceReloadProject || ProjectData.ProjectId != _requiredProjectArguments.ProjectId || ProjectData.IsRichSceneTemplate)
                {
                    LoadProjectStructure(_requiredProjectArguments.ProjectId, OnLoadProjectStructure);

                    void OnLoadProjectStructure(ProjectStructure projectStructure)
                    {
                        if (projectStructure == null)
                        {
                            return;
                        }

                        if (ProjectData.GameMode == GameMode.View)
                        {
                            ProjectConfiguration configuration = null;
                            
                            if (_requiredProjectArguments.ProjectConfigurationId > 0)
                            {
                                configuration = projectStructure.ProjectConfigurations.GetProjectConfigurationById(_requiredProjectArguments.ProjectConfigurationId);

                                if (configuration == null)
                                {
                                    Debug.LogError($"No project configuration found with ID {_requiredProjectArguments.ProjectConfigurationId}");
                                }
                            }
                            else
                            {
                                try
                                {
                                    configuration = projectStructure.ProjectConfigurations.First();
                                }
                                catch (Exception e)
                                {
                                    Debug.LogError("No project configuration found");
                                }
                            }

                            if (configuration != null)
                            {
                                var launchLanguage = configuration.Lang.Equals(AutoLanguage) ? CultureInfo.CurrentCulture.TwoLetterISOLanguageName : configuration.Lang;
                                LanguageManager.Instance.ChangeLanguage(launchLanguage);
                            }
                        }

                        LoadLoadingScenes(projectStructure);

                        _requiredProjectArguments.Guid = projectStructure.Guid;

                        Debug.Log("Checking license info...");

                        if (!LicenseValidator.LicenseKeyProvided(projectStructure) || !LicenseValidator.FillLicenseInfo(projectStructure.LicenseKey))
                        {
                            return;
                        }
                        
                        LicenseFeatureManager.ActivateLicenseFeatures(LicenseInfo.Value.Code ?? Edition.Starter);
                        
                        AuthorAttribution.ProjectAttribution = AuthorAttribution.ParseProjectStructure(projectStructure);
                        ProjectData.PlatformMode = _requiredProjectArguments.PlatformMode;
                        ProjectData.ProjectStructure = projectStructure;
                        CheckSceneId();
                        LoadResourcesAndObjects(_requiredProjectArguments.SceneId);
                    }
                }
                else
                {
                    CheckSceneId();
                    LoadResourcesAndObjects(_requiredProjectArguments.SceneId);
                    ProjectData.PlatformMode = _requiredProjectArguments.PlatformMode;
                }
            }
        }

        private static void LoadLoadingScenes(ProjectStructure projectStructure)
        {
            foreach (ProjectConfiguration projectConfiguration in projectStructure.ProjectConfigurations)
            {
                int loadingSceneTemplateId = projectConfiguration.LoadingSceneTemplateId ?? 0;
                if (loadingSceneTemplateId == 0)
                {
                    continue;
                }

                if (!ProjectData.LoadingScenePaths.ContainsKey(projectConfiguration.Id))
                {
                    Loader.LoadSceneTemplate(loadingSceneTemplateId, true);
                }
            }
        }

        private static void LoadResourcesAndObjects(int sceneId)
        {
            HashSet<ResourceDto> usingResources = GetUsingResources(sceneId);
            LoadResources(usingResources);

            ProjectData.ResourcesLoaded += LoadObjects;

            void LoadObjects()
            {
                UsingObjects = GetUsingObjects(sceneId);
                LoadPrefabObjects(UsingObjects.OrderByDescending(x => x.BuiltAt).ToList());
                ProjectData.ResourcesLoaded -= LoadObjects;
            }
        }

        private static void DisableAudioListenersOnScene()
        {
            GameObject playerHead = InputAdapter.Instance.PlayerController.Nodes.Head.GameObject;
            var playerAudioListeners = playerHead.GetComponentsInChildren<AudioListener>();
            var audioListeners = Object.FindObjectsOfType<AudioListener>().Except(playerAudioListeners);
            foreach (AudioListener audioListener in audioListeners)
            {
                if (audioListener.GetComponentInParent<IPlayerController>() == null)
                {
                    audioListener.enabled = false;
                }
            }
        }

        private static void InitializeSceneTemplateObjects()
        {
            var sceneObjects = GetSceneTemplateObjects();
            SceneTemplatePrefab sceneTemplatePrefab = ProjectData.ProjectStructure.SceneTemplates.GetProjectScene(ProjectData.SceneTemplateId);
            ProjectData.IsRichSceneTemplate = sceneObjects.Count > 0 || sceneTemplatePrefab.HasScripts;

            foreach (var sceneObject in sceneObjects)
            {
                var spawn = new SpawnInitParams
                {
                    Name = sceneObject.Value,
                    IdScene = ProjectData.SceneId,
                    IdObject = 0,
                    SceneTemplateObject = true,
                    InternalSpawn = true
                };

                Helper.InitObject(0, spawn, sceneObject.Key, null);
            }
        }

        private static Dictionary<GameObject, string> GetSceneTemplateObjects()
        {
            var sceneObjects = new Dictionary<GameObject, string>();

            var descriptors = Object.FindObjectsOfType<VarwinObjectDescriptor>();
            foreach (VarwinObjectDescriptor descriptor in descriptors)
            {
                if (!sceneObjects.ContainsKey(descriptor.gameObject))
                {
                    sceneObjects.Add(descriptor.gameObject, descriptor.Name);
                }
            }

            var monoBehaviours = Object.FindObjectsOfType<MonoBehaviour>().Where(x => x is IVarwinInputAware);
            foreach (MonoBehaviour monoBehaviour in monoBehaviours)
            {
                if (monoBehaviour.GetComponentInParent<VarwinObjectDescriptor>())
                {
                    continue;
                }

                if (!sceneObjects.ContainsKey(monoBehaviour.gameObject))
                {
                    sceneObjects.Add(monoBehaviour.gameObject, monoBehaviour.name);
                }
            }

            return sceneObjects;
        }

        private static List<PrefabObject> GetUsingObjects(int sceneId)
        {
            var projectObjects = ProjectData.ProjectStructure.Objects;
            var sceneObjects = GetSceneObjects(sceneId);
            var usingObjects = new List<PrefabObject>();

            foreach (PrefabObject prefabObject in projectObjects)
            {
                var usingSceneObjects = sceneObjects.Where(objectDto => objectDto.ObjectId == prefabObject.Id && !usingObjects.Contains(prefabObject));
                usingObjects.AddRange(usingSceneObjects.Select(_ => prefabObject));
            }

            return usingObjects;
        }

        private static HashSet<ResourceDto> GetUsingResources(int sceneId)
        {
            Data.ServerData.Scene currentScene = ProjectData.ProjectStructure.Scenes.FirstOrDefault(x => x.Id == sceneId);
            var onDemandedResourceGuids = currentScene?.Data?.OnDemandedResourceGuids;

            var usingResources = new HashSet<ResourceDto>();

            if (ProjectData.ProjectStructure.Resources == null)
            {
                return usingResources;
            }

            List<SceneObjectDto> sceneObjects = GetSceneObjects(sceneId);

            foreach (ResourceDto projectResource in ProjectData.ProjectStructure.Resources)
            {
                foreach (SceneObjectDto sceneObjectDto in sceneObjects)
                {
                    if (sceneObjectDto.ResourceIds == null)
                    {
                        continue;
                    }

                    foreach (int sceneObjectResourceId in sceneObjectDto.ResourceIds)
                    {
                        if (sceneObjectResourceId != projectResource.Id)
                        {
                            continue;
                        }

                        if (onDemandedResourceGuids != null && onDemandedResourceGuids.Contains(projectResource.Guid))
                        {
                            projectResource.OnDemand = true;
                        }

                        usingResources.Add(projectResource);
                    }
                }
            }

            return usingResources;
        }

        private static List<SceneObjectDto> GetSceneObjects(int sceneId)
        {
            Data.ServerData.Scene currentScene = ProjectData.ProjectStructure.Scenes.Find(scene => scene.Id == sceneId);
            var sceneObjects = new List<SceneObjectDto>();
            sceneObjects.AddRange(currentScene.SceneObjects);

            foreach (SceneObjectDto objectDto in currentScene.SceneObjects)
            {
                GetSceneObjectsHierarchy(objectDto, sceneObjects);
            }

            return sceneObjects;
        }

        private static void GetSceneObjectsHierarchy(SceneObjectDto objectDto, List<SceneObjectDto> result)
        {
            if (objectDto?.SceneObjects == null)
            {
                return;
            }

            foreach (SceneObjectDto child in objectDto.SceneObjects)
            {
                result.Add(child);
                GetSceneObjectsHierarchy(child, result);
            }
        }

        private static void PrefabObjectsLoaded()
        {
            if (_isOtherScene)
            {
                if (ProjectData.ProjectStructure == null)
                {
                    return;
                }

                Data.ServerData.Scene sceneTemplate = ProjectData.ProjectStructure.Scenes.GetProjectScene(_requiredProjectArguments.SceneId);

                if (sceneTemplate == null)
                {
                    return;
                }

                int sceneTemplateId = sceneTemplate.SceneTemplateId;
                
                ProjectData.SceneLoaded += OnLoadScene;
                ProjectData.SceneLoaded += InitializeSceneTemplateObjects;
                ProjectData.SceneLoaded += PlayerAnchorManager.StoreSettingsOnSceneLoad;
                
                LoadSceneTemplate(sceneTemplateId);

                void OnLoadScene()
                {
                    ProjectData.SceneLoaded -= InitializeSceneTemplateObjects;

                    Helper.SpawnSceneObjects(_requiredProjectArguments.SceneId, ProjectData.SceneId, _requiredProjectArguments.ForceReloadProject);
                    ProjectData.UpdateSubscribes(_requiredProjectArguments);
                    ProjectData.SceneLoaded -= OnLoadScene;
                }
            }
            else
            {
                ProjectData.SceneWasLoaded();
                Helper.SpawnSceneObjects(_requiredProjectArguments.SceneId, ProjectData.SceneId);
                ProjectData.UpdateSubscribes(_requiredProjectArguments);
            }

            IsLoadingProcess = false;
            ProjectData.ObjectsLoaded -= PrefabObjectsLoaded;
        }

        public static void LoadProject(LaunchArguments launchArguments)
        {
            _requiredProjectArguments.ProjectId = launchArguments.projectId;
            _requiredProjectArguments.SceneId = launchArguments.sceneId;
            _requiredProjectArguments.ProjectConfigurationId = launchArguments.projectConfigurationId;
            _requiredProjectArguments.GameMode = launchArguments.gm;
            _requiredProjectArguments.PlatformMode = launchArguments.platformMode;
            //Force it to be VR or Desktop from the beginning
            ProjectData.ProjectConfigurationId = launchArguments.projectConfigurationId;
            Loader.RequiredProjectArguments = _requiredProjectArguments;
            LoadProjectScene();
        }

        public static void LoadProject(int projectId, int sceneId, int projectConfigurationId, bool forceReloadScene = false)
        {
            LoadProject(
                new ProjectConfiguration 
                { 
                    ProjectId = projectId,
                    StartSceneId = sceneId,
                    Id = projectConfigurationId,
                    PlatformMode = ProjectData.PlatformMode,
                    
                }, 
                projectId,
                forceReloadScene
            );
        }

        private static void LoadProject(ProjectConfiguration projectConfiguration, int projectId, bool forceReloadScene = false)
        {
            _requiredProjectArguments.ProjectId = projectId;
            _requiredProjectArguments.SceneId = projectConfiguration.StartSceneId ?? 0;
            _requiredProjectArguments.ProjectConfigurationId = projectConfiguration.Id;
            _requiredProjectArguments.GameMode = ProjectData.GameMode;
            _requiredProjectArguments.PlatformMode = projectConfiguration.PlatformMode;
            Loader.RequiredProjectArguments = _requiredProjectArguments;

            LoadProjectScene(forceReloadScene);
        }

        public static void LoadProjectConfiguration(int projectConfigurationId)
        {
            ProjectConfiguration projectConfiguration = ProjectData.ProjectStructure.ProjectConfigurations.Find(configuration => configuration.Id == projectConfigurationId);
            int projectId = ProjectData.ProjectStructure.ProjectId;
            ProjectData.ProjectConfigurationId = projectConfigurationId;
            LoadProject(projectConfiguration, projectId);
        }
    }
}
