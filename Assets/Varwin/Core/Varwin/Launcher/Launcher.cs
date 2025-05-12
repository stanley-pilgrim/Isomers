using System;
using System.IO;
using Core.Varwin;
using Varwin.Log;
using SmartLocalization;
using UnityEngine;
using Varwin.Data;
using Varwin.Data.ServerData;
using Varwin.ECS.Systems.Loader;
using Varwin.GraphQL;
using Varwin.UI;
using Varwin.WWW;
using Debug = UnityEngine.Debug;
using ErrorCode = Varwin.Log.ErrorCode;

namespace Varwin.PUN
{
    public class Launcher : MonoBehaviour
    {
        #region PUBLIC VARS

        public static bool Launch { get; private set; }

        public static Launcher Instance;

        public bool LoadProjectFromStorage;
        public string StorageProjectFolderPath;

        #endregion

        #region PRIVATE VARS

        private LoaderSystems _loaderSystems;

        #endregion

        private void OnApplicationQuit()
        {
            Debug.Log($"Application ending after {Time.time} seconds");

            Subscriptions.LibraryChanged?.Unsubscribe();
            Subscriptions.ProjectsChanged?.Unsubscribe();
            Subscriptions.ProjectsLogicChanged?.Unsubscribe();
        }

        private void Awake()
        {
            Debug.Log("Launcher started");

            Instance = this;

            Texture.allowThreadedTextureCreation = true;
        }

        private void Start()
        {
            Init();
        }

        public void LaunchFromApi(int workspaceId, string apiBaseUrl, string accessToken, string refreshToken)
        {
            Launch = true;

            Request.WorkspaceId = workspaceId;
            Request.SocketId = Guid.NewGuid();

            API.UpdateTokens(accessToken, refreshToken);

            LoaderAdapter.Init(new ApiLoader());
            RequestServerConfig(apiBaseUrl);
            StartLoaderSystems();
        }

        private void LaunchFromStorage(string projectPath)
        {
            Settings.CreateStorageSettings(projectPath);
            StartLoadFile();
        }

        public void Init()
        {
            LanguageManager.SetDontDestroyOnLoad();
            Contexts.sharedInstance.game.DestroyAllEntities();

            string[] args = Environment.GetCommandLineArgs();
            string projectPath = args.Length < 2 ? string.Empty : args[1];

#if UNITY_EDITOR
            if (LoadProjectFromStorage)
            {
                Settings.ReadTestSettings();
                projectPath = StorageProjectFolderPath;
            }
#endif
            var localProjectPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "project");
            
            if (Directory.Exists(projectPath))
            {
                LaunchFromStorage(projectPath);
            }
            else if (Directory.Exists(localProjectPath))
            {
                LaunchFromStorage(localProjectPath);
            }
            else if (CommandPipe.Instance)
            {
                CommandPipe.Instance.RunPipeHandling();
            }
        }

        private void StartLoaderSystems()
        {
            if (_loaderSystems != null)
            {
                return;
            }

            _loaderSystems = new LoaderSystems(Contexts.sharedInstance);
            _loaderSystems.Initialize();
            ProjectDataListener.OnUpdate = () => _loaderSystems.Execute();
        }

        private void StartLoadFile()
        {
            Debug.Log($"File name = {Settings.Instance.StoragePath}");

            ProjectData.ExecutionMode = ExecutionMode.EXE;
            ProjectData.PlatformMode = PlatformMode.Desktop;
            ProjectData.GameMode = GameMode.View;

            LoaderAdapter.LoadProjectStructure(0, OnProjectStructureRead);

            void OnProjectStructureRead(ProjectStructure projectStructure)
            {
                ProjectData.ProjectStructure = projectStructure;

                Debug.Log("Checking license info...");

                if (!LicenseValidator.LicenseKeyProvided(projectStructure) || !LicenseValidator.FillLicenseInfo(projectStructure.LicenseKey))
                {
                    return;
                }
                
                LicenseFeatureManager.ActivateLicenseFeatures(LicenseInfo.Value.Code ?? Edition.Starter);
                AuthorAttribution.ProjectAttribution = AuthorAttribution.ParseProjectStructure(projectStructure);

                if (ProjectData.ProjectStructure.ProjectConfigurations.Count == 0)
                {
                    string errorMsg = ErrorHelper.GetErrorDescByCode(ErrorCode.ProjectConfigNullError);
                    CoreErrorManager.Error(new Exception(errorMsg));
                    LauncherErrorManager.Instance.Show(errorMsg);

                    return;
                }

                if (ProjectData.ProjectStructure.ProjectConfigurations.Count == 1)
                {
                    int projectConfigurationId = ProjectData.ProjectStructure.ProjectConfigurations[0].Id;
                    LoadConfigurationFromStorage(projectConfigurationId);
                }
                else
                {
                    UIChooseProjectConfig.Instance.Show();
                    UIChooseProjectConfig.Instance.UpdateDropDown(
                        ProjectData.ProjectStructure.ProjectConfigurations.GetNames(),
                        LoadConfigurationFromStorage);
                }
            }
        }

        private void LoadConfigurationFromStorage(int projectConfigurationId)
        {
            var loaderSystems = new LoaderSystems(Contexts.sharedInstance);
            loaderSystems.Initialize();
            ProjectDataListener.OnUpdate = () => loaderSystems.Execute();
            LoaderAdapter.LoadProjectConfiguration(projectConfigurationId);
        }

        private void RequestServerConfig(string apiBaseUrl)
        {
            Debug.Log($"Server address: {apiBaseUrl}");

            Settings.SetApiUrl(apiBaseUrl);

            ProjectData.ExecutionMode = ExecutionMode.RMS;

            API.GetServerConfig(serverConfig =>
                {
                    Debug.Log("Checking API version...");

                    if (Version.TryParse(serverConfig.appVersion, out var serverVersion) 
                        && Version.TryParse(VarwinVersionInfoContainer.VersionNumber, out var clientVersion)
                        && serverVersion.Major != clientVersion.Major 
                        && serverVersion.Minor != clientVersion.Minor)
                    {
                        Debug.Log($"VarwinVersionInfo.VersionNumber = `{VarwinVersionInfo.VersionNumber}`; serverConfig.AppVersion = `{serverConfig.appVersion}`");
                        LauncherErrorManager.Instance.ShowFatalErrorKey(ErrorHelper.GetErrorKeyByCode(ErrorCode.ApiAndClientVersionMismatchError), Environment.StackTrace);
                        Debug.LogError("Api and client version mismatch");

                        return;
                    }

                    Settings.ReadServerConfig(serverConfig);
                },
                onError: (error, responseCode) =>
                {
                    var errorDetails = $"Can't get server config:\n{error}\n\n RESPONSE CODE: {responseCode} \n\n API URL: {apiBaseUrl}\n\n{Environment.StackTrace}";
                    LauncherErrorManager.Instance.ShowFatalErrorKey(ErrorHelper.GetErrorKeyByCode(ErrorCode.ServerNoConnectionError), $"{errorDetails}");
                    Debug.LogError(errorDetails);
                },
                onConnectionError: (connectionError, responseCode) =>
                {
                    string localizedError = ErrorHelper.TryGetServerConnectionError(apiBaseUrl, out var documentationLink, out bool isConnectionExists);
                    var errorDetails = $"Can't get server config:\n{connectionError}\n\n RESPONSE CODE: {responseCode}\n\nAPI URL: {apiBaseUrl}\n\n{Environment.StackTrace}";
                    LauncherErrorManager.Instance.ShowDocumentedError(localizedError, errorDetails, documentationLink);

                    Debug.LogError(errorDetails);
                },
                onParseServerInfoError: parseError =>
                {
                    var errorDetails = $"Can't parse server config:\n{parseError}\n\nAPI URL: {apiBaseUrl}\n\n{Environment.StackTrace}";
                    var localizedError = ErrorHelper.GetParseServerConfigError(apiBaseUrl, out var documentationLink);

                    LauncherErrorManager.Instance.ShowDocumentedError(localizedError, errorDetails, documentationLink);
                });
        }
    }
}