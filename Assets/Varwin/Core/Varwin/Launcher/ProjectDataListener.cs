using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Assimp;
using Varwin.Log;
using SmartLocalization;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Varwin.Data;
using Varwin.Data.ServerData;
using Varwin.GraphQL;
using Varwin.Multiplayer;
using Varwin.Public;
using Varwin.UI;
using Varwin.UI.VRErrorManager;
using Varwin.PlatformAdapter;
using Varwin.WWW;

namespace Varwin
{
    public class ProjectDataListener : MonoBehaviour
    {
        #region PUBLIC VARS

        public const int WaitForInputAdapterInitializeFrame = 10;
        public static ProjectDataListener Instance;
        public static Action OnUpdate { get; set; }

        #endregion

        #region PRIVATE VARS

        private int _lastSceneTemplateId;
        private LaunchArguments _launchArguments;
        private ProjectSceneArguments _projectSceneArguments;
        private ProjectConfigurationArguments _projectConfigurationArguments;
        private ProjectSceneArguments _storedSceneArguments;
        private LaunchArguments _storedLaunchArguments;
        private ProjectConfigurationArguments _storedProjectConfigurationArguments;
        private bool _activeSceneChanged;

        #endregion

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }
        
        private void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene prevScene, UnityEngine.SceneManagement.Scene currentScene)
        {
            if (!prevScene.IsValid())
            {
                return;
            }
            
            _activeSceneChanged = true;
        }

#if !UNITY_EDITOR
        private void Start()
        {
           Application.logMessageReceived += ErrorHelper.ErrorHandler;
        }
#endif

        public void ForceReload()
        {
            _launchArguments = _storedLaunchArguments;
            _projectSceneArguments = _storedSceneArguments;
            _projectConfigurationArguments = _storedProjectConfigurationArguments;
        }

        public void LaunchWithArguments(LaunchArguments launchArguments)
        {
            if (_storedLaunchArguments?.multiplayerMode == MultiplayerMode.Client && launchArguments?.multiplayerMode == MultiplayerMode.Client)
            {
                return;
            }
            
            _launchArguments = launchArguments;
            _storedLaunchArguments = launchArguments;

            if (ProjectData.GameMode == GameMode.Edit && ProjectData.ObjectsAreChanged)
            {
                AskUserToSave();
            }
            else
            {
                ApplyConfig();
            }
        }

        private void Update()
        {
            DoOnUpdate();

            if (LoaderAdapter.LoaderType == null || LoaderAdapter.LoaderType == typeof(StorageLoader))
            {
                return;
            }

            if (_projectSceneArguments != null)
            {
                switch (_projectSceneArguments.State)
                {
                    case ProjectSceneArguments.StateProjectScene.Added:
                        SceneTemplateAdded(_projectSceneArguments);

                        break;

                    case ProjectSceneArguments.StateProjectScene.Deleted:
                        SceneTemplateDeleted(_projectSceneArguments);

                        break;

                    case ProjectSceneArguments.StateProjectScene.Changed:
                        SceneTemplateChanged(_projectSceneArguments);

                        break;
                }

                _storedSceneArguments = _projectSceneArguments;
                _projectSceneArguments = null;
            }

            if (_projectConfigurationArguments != null)
            {
                switch (_projectConfigurationArguments.State)
                {
                    case ProjectConfigurationArguments.StateConfiguration.Added:
                        ConfigurationAdded(_projectConfigurationArguments.ProjectConfiguration);

                        break;

                    case ProjectConfigurationArguments.StateConfiguration.Deleted:
                        ConfigurationDeleted(_projectConfigurationArguments.ProjectConfiguration);

                        break;

                    case ProjectConfigurationArguments.StateConfiguration.Changed:
                        ConfigurationChanged(_projectConfigurationArguments.ProjectConfiguration);

                        break;
                }

                _storedProjectConfigurationArguments = _projectConfigurationArguments;
                _projectConfigurationArguments = null;
            }
        }

        private void OnDestroy()
        {
            if (Directory.Exists(RequestVideo.VideosDirectory))
            {
                Directory.Delete(RequestVideo.VideosDirectory, true);
            }

            Subscriptions.LibraryChanged?.Unsubscribe();
            Subscriptions.ProjectsChanged?.Unsubscribe();
            Subscriptions.ProjectsLogicChanged?.Unsubscribe();
            Subscriptions.ProjectsLogicChanged?.Unsubscribe();
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private static void AskUserToSave()
        {
            ProjectData.OnProjectReload();
        }

        private static void DoOnUpdate()
        {
            try
            {
                OnUpdate?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError("Error Invoke OnUpdate! " + e.Message + " " + e.StackTrace);
            }
        }

        public void ApplyConfig()
        {
            if (_launchArguments == null)
            {
                return;
            }

            if (_launchArguments.multiplayerMode == null || _launchArguments.multiplayerMode == MultiplayerMode.Single || _launchArguments.multiplayerMode == MultiplayerMode.Undefined)
            {
                LoaderAdapter.LoadProject(_launchArguments);
            }
            else if (_launchArguments.multiplayerMode == MultiplayerMode.Host)
            {
                var projectInfo = new ProjectInfo
                {
                    ProjectId = _launchArguments.projectId,
                    SceneId = _launchArguments.sceneId,
                    ProjectConfigurationId = _launchArguments.projectConfigurationId,
                    VersionNumber = VarwinVersionInfoContainer.VersionNumber,
                };
                
                ((VarwinNetworkManager) NetworkManager.Singleton).ProjectInfo = projectInfo;
                ((VarwinNetworkManager) NetworkManager.Singleton).StartHost();
                SceneManager.LoadScene(2);
                UIFadeInOutController.Instance.InstantFadeOut();
            }
            else if (_launchArguments.multiplayerMode == MultiplayerMode.Client)
            {
                var isMobileVr = ProjectData.IsMobileVr();
                if (_launchArguments.platformMode == 0 && !isMobileVr)
                {
                    _launchArguments.platformMode = PlatformMode.Desktop;
                    ProjectData.PlatformMode = PlatformMode.Desktop;
                }

                if (!isMobileVr)
                {
                    SceneManager.LoadScene(3);
                }

                UIFadeInOutController.Instance.InstantFadeOut();
            }

            UpdateSettings(_launchArguments);
            _launchArguments = null;
        }

        public static void ReadyToGetNewMessages()
        {
            Debug.Log("<Color=Lime><b></b>Listener ready to listen!</Color>");
            Debug.Log("Listener ready to listen!");
        }

        public static void UpdateSettings(LaunchArguments launchArguments)
        {
            Debug.Log($"New launch arguments: {launchArguments.ToString()}");
            Settings.SetupLanguageFromLaunchArguments(launchArguments);
        }

        public void BeforeSceneLoaded(bool isOtherScene)
        {
            HidePopUpAndToolTips();
            StartCoroutine(BeforeLoadSceneCoroutine(isOtherScene));
        }
        
        // ReSharper disable once UnusedMember.Global
        public void BackToMainMenu()
        {
#if !UNITY_ANDROID
            Debug.LogError("BackToMainMenu is not supported on this platform!");
            return;
#endif
            
            GameStateData.ClearObjects();
            GameStateData.ClearLogic();
            
            if (NetworkManager.Singleton)
            {
                NetworkManager.Singleton.Shutdown();
            }
            
            try
            {
                if (Subscriptions.ProjectsChanged != null)
                {
                    Subscriptions.ProjectsChanged.Unsubscribe();
                }
            
                if (Subscriptions.ProjectsLogicChanged != null)
                {
                    Subscriptions.ProjectsLogicChanged.Unsubscribe();
                }
                
                ProjectData.UpdateSubscribes(new RequiredProjectArguments() {GameMode = GameMode.View, PlatformMode = PlatformMode.Vr});
            }
            catch
            {
                // ignored
            }
            finally
            {
                LoadScene("MobileLauncher");
                InputAdapter.Instance.PointerController.IsMenuOpened = false;
                PopupWindowManager.ClosePopup();
            }
            
            ProjectData.ProjectStructure = null;
        }

        private IEnumerator BeforeLoadSceneCoroutine(bool isOtherScene)
        {
            if (isOtherScene)
            {
                if (UIFadeInOutController.Instance)
                {
                    yield return FadeIn();
                }

                GameStateData.ClearLogic();
                GameStateData.ClearObjects();
                GameStateData.ClearAllData();

                yield return WaitForInitializeInputAdapter();


                yield return StartCoroutine(LoadLoaderScene());
                yield return StartCoroutine(UnloadSceneAndResources());
            }
            else
            {
                Helper.ReloadSceneObjects();
            }
            
            UIFadeInOutController.IsBlocked = true;

            ResetPlayerRigPosition();
            
            var isFaded = UIFadeInOutController.Instance
                && (UIFadeInOutController.Instance.FadeStatus == FadeInOutStatus.FadingIn
                || UIFadeInOutController.Instance.FadeStatus == FadeInOutStatus.FadingInComplete);

            if (isOtherScene && isFaded)
            {
                yield return FadeOut();
            }

            ProjectData.OnSceneCleared();
            yield return new WaitWhile(() => !_activeSceneChanged);
            _activeSceneChanged = false;
        }

        private void ResetPlayerRigPosition()
        {
            if (!GameObjects.Instance)
            {
                return;
            }

            WorldDescriptor worldDescriptor = FindObjectOfType<WorldDescriptor>();

            if (!worldDescriptor)
            {
                ReturnLoadError("WorldDescriptor not found!");
                return;
            }

            PlayerAnchorManager playerAnchorManager = FindObjectOfType<PlayerAnchorManager>();

            if (!playerAnchorManager)
            {
                playerAnchorManager = worldDescriptor.PlayerSpawnPoint.gameObject.AddComponent<PlayerAnchorManager>();
            }

            playerAnchorManager.SetPlayerPosition();
        }

        public static void AddLoadingScene(int id, string scenePath)
        {
            if (!ProjectData.LoadingScenePaths.ContainsKey(id))
            {
                ProjectData.LoadingScenePaths.Add(id, scenePath);
            }
        }

        public void LoadScene(string scenePath)
        {
            StartCoroutine(LoadSceneBeautifulCoroutine(scenePath));
        }

        private IEnumerator LoadSceneBeautifulCoroutine(string scenePath)
        {
            if (UIFadeInOutController.Instance && UIFadeInOutController.Instance.FadeStatus != FadeInOutStatus.FadingIn)
            {
                yield return StartCoroutine(FadeIn());
            }

            yield return StartCoroutine(LoadSceneCoroutine(scenePath));

            while (!InputAdapter.Instance.PlayerController.Nodes.Head.GameObject)
            {
                yield return null;
            }

            ProjectData.SceneWasLoaded();

            if (ProjectData.GameMode == GameMode.Edit)
            {
                UIFadeInOutController.Instance.InstantFadeOut();
                yield return null;
            }
            else
            {
                yield return StartCoroutine(FadeOut());
            }
        }

        private void LoadWorldDescriptor()
        {
            Debug.Log("Waiting to load WorldDescriptor...");
            WorldDescriptor worldDescriptor = FindObjectOfType<WorldDescriptor>();

            if (!worldDescriptor)
            {
                ReturnLoadError("WorldDescriptor not found!");
                return;
            }

            if (worldDescriptor.PlayerSpawnPoint)
            {
                PlayerAnchorManager playerAnchorManager = FindObjectOfType<PlayerAnchorManager>();

                if (!playerAnchorManager)
                {
                    playerAnchorManager = worldDescriptor.PlayerSpawnPoint.gameObject.AddComponent<PlayerAnchorManager>();
                }

                playerAnchorManager.RestartPlayer();
            }
            else
            {
                Debug.LogError("Player Spawn Point not found in Scene Template Config");
                ReturnLoadError("Player Spawn Point not found in Scene Template Config");
            }
        }

        private void ReturnLoadError(string message)
        {
            Debug.LogError($"Error to load scene. {message}");
            var go = new GameObject("Default player rig");
            go.transform.position = new Vector3(0, 1.8f, 0);
            go.transform.rotation = Quaternion.identity;
            go.AddComponent<PlayerAnchorManager>();
            StartCoroutine(ShowSpawnError());
        }

        private IEnumerator ShowSpawnError()
        {
            while (!VRErrorManager.Instance)
            {
                yield return null;
            }

            string errorMsg = ErrorHelper.GetErrorDescByCode(ErrorCode.SpawnPointNotFoundError);
            CoreErrorManager.Error(new Exception(errorMsg));
            VRErrorManager.Instance.Show(errorMsg);
            yield return true;
        }

        private IEnumerator LoadSceneCoroutine(string scenePath)
        {
            Debug.Log("Loading new Scene: " + Time.time);

            var activeScene = SceneManager.GetActiveScene();

            if (activeScene.path == scenePath)
            {
                Debug.Log("Scene already loaded");
                yield break;
            }

            AsyncOperation loadSceneOperation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);

            while (!loadSceneOperation.isDone)
            {
                yield return null;
            }
            
            LightProbes.TetrahedralizeAsync();

            Debug.Log("Unloading old Scene: " + Time.time);

            AsyncOperation unloadSceneOperation = SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene(), UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);

            while (!unloadSceneOperation.isDone)
            {
                yield return null;
            }

            LoadWorldDescriptor();
            Debug.Log($"Scene Template is loaded: {Time.time}");

            yield return true;
        }

        private static IEnumerator FadeIn()
        {
            while (!UIFadeInOutController.Instance)
            {
                yield return null;
            }
            
            UIFadeInOutController.Instance.FadeIn();

            while (UIFadeInOutController.Instance && !UIFadeInOutController.Instance.IsComplete)
            {
                yield return null;
            }

            yield return true;
        }

        public void StartFadeOut()
        {
            StartCoroutine(FadeOut());
        }

        private static IEnumerator FadeOut()
        {
            while (!UIFadeInOutController.Instance)
            {
                yield return null;
            }

            UIFadeInOutController.Instance.InstantFadeIn();

            const float freezeCompensation = 0.33f;
            yield return new WaitForSecondsRealtime(freezeCompensation);

            while (SceneLogic.Instance && UIFadeInOutController.IsBlocked)
            {
                yield return null;
            }

            UIFadeInOutController.IsBlocked = false;
            UIFadeInOutController.Instance.FadeOut();

            while (!UIFadeInOutController.Instance.IsComplete)
            {
                yield return null;
            }

            yield return true;
        }

        private static IEnumerator UnloadSceneAndResources()
        {
            Debug.Log("Start unload current scene");

            AsyncOperation unloadSceneOperation = SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene());

            if (unloadSceneOperation == null)
            {
                yield break;
            }

            while (!unloadSceneOperation.isDone)
            {
                yield return null;
            }

            AssimpModelContainer.UnloadAll();
            
            Resources.UnloadUnusedAssets();

            yield return true;
        }

        private static string TryGetLoadingScene()
        {
            if (ProjectData.ProjectStructure == null)
            {
                return string.Empty;
            }

            ProjectConfiguration configuration = ProjectData.ProjectStructure.ProjectConfigurations.Find(p => p.Id == ProjectData.ProjectConfigurationId);

            if (configuration == null)
            {
                return string.Empty;
            }

            int loadingSceneTemplateId = configuration.LoadingSceneTemplateId ?? 0;
            if (loadingSceneTemplateId != 0 && ProjectData.LoadingScenePaths.ContainsKey(configuration.Id))
            {
                return ProjectData.LoadingScenePaths[ProjectData.ProjectConfigurationId];
            }

            return string.Empty;
        }

        private static IEnumerator LoadLoaderScene()
        {
            if (InputAdapter.Instance == null|| !InputAdapter.Instance.PlayerController.Nodes.Head.GameObject)
            {
                yield break;
            }

            Debug.Log("Load loader scene");

            string customLoadingScene = TryGetLoadingScene();

            if (!string.IsNullOrEmpty(customLoadingScene))
            {
                AsyncOperation loadSceneOperation = SceneManager.LoadSceneAsync(customLoadingScene, LoadSceneMode.Additive);

                while (!loadSceneOperation.isDone)
                {
                    yield return null;
                }
                
                LightProbes.TetrahedralizeAsync();

                TurnOnFeedBackScripts();
            }
            else
            {
                AsyncOperation loadLoading = SceneManager.LoadSceneAsync("Loading", LoadSceneMode.Additive);

                while (!loadLoading.isDone)
                {
                    yield return null;
                }
                
                LightProbes.TetrahedralizeAsync();

                yield return true;
            }
        }

        private static IEnumerator WaitForInitializeInputAdapter()
        {
            int frameCounter = 0;
            while (InputAdapter.Instance == null && frameCounter < WaitForInputAdapterInitializeFrame)
            {
                yield return null;
                frameCounter++;
            }
        }

        private static void TurnOnFeedBackScripts()
        {
            var feedBackText = FindObjectOfType<LoaderFeedBackText>();

            if (!feedBackText)
            {
                return;
            }

            var text = feedBackText.GetComponent<Text>();
            var tmpText = feedBackText.GetComponent<TMP_Text>();

            if (text)
            {
                text.enabled = true;
            }

            if (tmpText)
            {
                tmpText.enabled = true;
            }

            feedBackText.enabled = true;
        }

        public void RestoreJoints(Dictionary<int, JointData> joints)
        {
            if (joints == null)
            {
                return;
            }

            StartCoroutine(RestoreJointsOnNextFrame(joints));
        }

        private IEnumerator RestoreJointsOnNextFrame(Dictionary<int, JointData> joints)
        {
            JointBehaviour.IsTempConnectionCreated = true;

            yield return null;

            Debug.Log("<Color=Olive>Restore joints started!</Color>");

            var jointsScene = FindObjectsOfType<JointBehaviour>();

            foreach (JointBehaviour joint in jointsScene)
            {
                joint.UnLockAndDisconnectPoints();
            }

            var behaviours = new List<JointBehaviour>();

            foreach (var joint in joints)
            {
                int instanceId = joint.Key;
                JointData jointData = joint.Value;
                ObjectController objectController = GameStateData.GetObjectControllerInSceneById(instanceId);

                if (objectController == null)
                {
                    continue;
                }

                JointBehaviour jointBehaviour = objectController.RootGameObject.GetComponent<JointBehaviour>();

                if (!jointBehaviour)
                {
                    continue;
                }

                behaviours.Add(jointBehaviour);

                var jointPoints = Helper.GetJointPoints(objectController.RootGameObject);

                foreach (var jointConnectionsData in jointData.JointConnectionsData)
                {
                    int pointId = jointConnectionsData.Key;
                    if (!jointPoints.ContainsKey(pointId))
                    {
                        Debug.LogError(
                            $"Cannot find joint point for id {pointId} (ConnectedObjectInstanceId: {jointConnectionsData.Value.ConnectedObjectInstanceId}; ConnectedObjectJointPointId: {jointConnectionsData.Value.ConnectedObjectJointPointId})");
                        continue;
                    }

                    JointPoint myJointPoint = jointPoints[pointId];
                    JointConnectionsData connectionData = jointConnectionsData.Value;

                    ObjectController otherObjectController =
                        GameStateData.GetObjectControllerInSceneById(connectionData.ConnectedObjectInstanceId);
                    var otherJointPoints = Helper.GetJointPoints(otherObjectController.RootGameObject);

                    if (!otherJointPoints.ContainsKey(connectionData.ConnectedObjectJointPointId))
                    {
                        Debug.LogError(
                            $"Cannot find object id {connectionData.ConnectedObjectJointPointId} for joint {myJointPoint.gameObject.name} in object {myJointPoint.GetComponentInParent<VarwinObjectDescriptor>()?.Name}",
                            myJointPoint);
                        continue;
                    }

                    JointPoint otherJointPoint = otherJointPoints[connectionData.ConnectedObjectJointPointId];
                    jointBehaviour.ConnectToJointPoint(myJointPoint, otherJointPoint);
                    myJointPoint.CanBeDisconnected = !connectionData.ForceLocked;
                    otherJointPoint.CanBeDisconnected = !connectionData.ForceLocked;
                }
            }

            yield return null;

            JointBehaviour.IsTempConnectionCreated = false;
            yield return true;
        }

        public class UpdatedLibraryObject
        {
            public int Id;
            public string Name;
        }

        public event Action LibraryItemCreated;
        public event Action<int[]> LibraryResourcesRemoved;
        public event Action<List<UpdatedLibraryObject>> LibraryObjectsUpdated;
        public event Action<int[]> LibraryObjectsRemoved;
        public event Action<int[]> LibraryPackagesRemoved;

        public void OnLibraryItemCreated()
        {
            LibraryItemCreated?.Invoke();
        }

        public void OnLibraryResourcesRemoved(int[] ids)
        {
            LibraryResourcesRemoved?.Invoke(ids);
        }

        public void OnLibraryObjectsUpdated(List<UpdatedLibraryObject> updatedObjects)
        {
            LibraryObjectsUpdated?.Invoke(updatedObjects);
        }

        public void OnLibraryObjectsRemoved(int[] ids)
        {
            LibraryObjectsRemoved?.Invoke(ids);
        }
        
        public void OnLibraryPackagesRemoved(int[] ids)
        {
            LibraryPackagesRemoved?.Invoke(ids);
        }

        private static void SceneTemplateAdded(ProjectSceneArguments newSceneTemplate)
        {
            ProjectData.ProjectStructure.Scenes.Add(newSceneTemplate.Scene);
            ProjectData.ProjectStructure.UpdateOrAddSceneTemplatePrefab(newSceneTemplate.SceneTemplate);
        }

        private static void SceneTemplateChanged(ProjectSceneArguments changedSceneTemplate)
        {
            ProjectData.ProjectStructure.UpdateProjectScene(changedSceneTemplate.Scene);
            ProjectData.ProjectStructure.UpdateOrAddSceneTemplatePrefab(changedSceneTemplate.SceneTemplate);

            if (ProjectData.SceneId == changedSceneTemplate.Scene.Id && ProjectData.SceneTemplateId != changedSceneTemplate.Scene.SceneTemplateId)
            {
                LoaderAdapter.LoadProject(ProjectData.ProjectId, changedSceneTemplate.Scene.Id, ProjectData.ProjectConfigurationId);
            }
        }

        public static void SceneTemplateDeleted(ProjectSceneArguments deletedSceneTemplate)
        {
            if (ProjectData.SceneId == deletedSceneTemplate.Scene.Id)
            {
                Debug.Log("Current scene template was deleted");
                string message = LanguageManager.Instance.GetTextValue("CURRENT_SCENE_TEMPLATE_DELETED");
                if (VRErrorManager.Instance)
                {
                    VRErrorManager.Instance.ShowFatal(message);
                }
            }
            else
            {
                ProjectData.ProjectStructure.RemoveProjectScene(deletedSceneTemplate.Scene);
            }
        }

        public static void ConfigurationAdded(ProjectConfiguration projectConfiguration)
        {
            ProjectData.ProjectStructure.ProjectConfigurations.Add(projectConfiguration);
        }

        public static void ConfigurationDeleted(ProjectConfiguration projectConfiguration)
        {
            ProjectData.ProjectStructure.RemoveProjectConfiguration(projectConfiguration);
        }

        public static void ConfigurationChanged(ProjectConfiguration projectConfiguration)
        {
            ProjectData.ProjectStructure.UpdateProjectConfiguration(projectConfiguration);
        }

        private static void HidePopUpAndToolTips()
        {
            PopupWindowManager.ClosePopup();
            TooltipManager.HideControllerTooltip(ControllerTooltipManager.TooltipControllers.Both, ControllerTooltipManager.TooltipButtons.Grip);
            TooltipManager.HideControllerTooltip(ControllerTooltipManager.TooltipControllers.Both, ControllerTooltipManager.TooltipButtons.Trigger);
            TooltipManager.HideControllerTooltip(ControllerTooltipManager.TooltipControllers.Both, ControllerTooltipManager.TooltipButtons.Touchpad);
            TooltipManager.HideControllerTooltip(ControllerTooltipManager.TooltipControllers.Both, ControllerTooltipManager.TooltipButtons.ButtonOne);
            TooltipManager.HideControllerTooltip(ControllerTooltipManager.TooltipControllers.Both, ControllerTooltipManager.TooltipButtons.ButtonTwo);
            TooltipManager.HideControllerTooltip(ControllerTooltipManager.TooltipControllers.Both, ControllerTooltipManager.TooltipButtons.StartMenu);
        }
    }
}