using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using Varwin;
using Varwin.Data.ServerData;
using Varwin.GraphQL;
using Varwin.PUN;

namespace Core.Varwin
{
    public class CommandPipe : MonoBehaviour
    {
        public static CommandPipe Instance;
        
        private const string DebugPipeName = "varwin-client-debug";
        private const string PipeNameArg = "-pipeName";

        private readonly Queue<string> _receiveQueue = new();
        private readonly Queue<string> _sendQueue = new();

        private NamedPipeClientStream _pipeClientStream;

        private string _pipeName = "debug-pipe";
        private string _logFilePath;

        private bool _firstExecuteLaunchCommand = true;
        private bool _sceneInitialized = false;

        private CommandPipeHandler _pipeHandler;

        public enum PipeCommandType
        {
            [EnumMember(Value = "launch")] Launch,
            [EnumMember(Value = "terminate")] Terminate,
            [EnumMember(Value = "runtime_blocks")] RunningBlocks,
            [EnumMember(Value = "runtime_error")] RuntimeError,
            [EnumMember(Value = "restart")] Restart
        }

        public class PipeCommand
        {
            public class PipeLaunchCommandData
            {
                public string Lang;
                public int ProjectId;
                public int SceneId;
                public int ProjectConfigurationId;

                public MultiplayerMode? MultiplayerMode;
                public PlatformMode PlatformMode;
                public GameMode LaunchMode;

                public string AccessToken;
                public string RefreshToken;
                public string ApiBaseUrl;
                public int WorkspaceId;
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public PipeCommandType Command;

            public PipeLaunchCommandData LaunchParams;
        }

        public void RunPipeHandling()
        {
#if UNITY_EDITOR
            _pipeName = DebugPipeName;
#else
            string[] args = Environment.GetCommandLineArgs();
            try
            {
                for (var i = 0; i < args.Length; i++)
                {
                    if (args[i].Contains(PipeNameArg))
                    {
                        _pipeName = args[i + 1];
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: RunPipeHandling. Args: {string.Join(", ", args)}\n{e}");
                throw;
            }
#endif
            _pipeHandler = new CommandPipeHandler(_receiveQueue, _sendQueue, _pipeName);
            _pipeHandler.PipeStart(this);

            Debug.Log("Pipe handling start");
        }

        public void SendPipeCommand(object command, bool verbose = false) => _pipeHandler?.SendPipeCommand(command, verbose);

        private static bool CanExecuteLaunchCommand()
        {
            if (Instance._firstExecuteLaunchCommand)
            {
                Instance._firstExecuteLaunchCommand = false;
                return true;
            }
            
            return Instance._sceneInitialized;
        }
        
        private static void HandleCommand(PipeCommand command)
        {
            if (command == null)
            {
                return;
            }

            switch (command.Command)
            {
                case PipeCommandType.Launch:
                    if (command.LaunchParams != null && CanExecuteLaunchCommand())
                    {
                        if (!Launcher.Launch)
                        {
                            Launcher.Instance.LaunchFromApi(
                                command.LaunchParams.WorkspaceId,
                                command.LaunchParams.ApiBaseUrl,
                                command.LaunchParams.AccessToken,
                                command.LaunchParams.RefreshToken);
                        }

                        var launchArguments = BuildLaunchArguments(command);
                        
                        if (Subscriptions.ProjectsChanged != null)
                        {
                            Subscriptions.ProjectsChanged.Unsubscribe();
                        }
                        
                        Subscriptions.ProjectsChanged = new Subscriptions.ProjectsChangedSubscription(new[] {launchArguments.projectId});
                        Subscriptions.ProjectsChanged.Subscribe();

                        if (Subscriptions.ProjectsLogicChanged != null)
                        {
                            Subscriptions.ProjectsLogicChanged.Unsubscribe();
                        }

                        Subscriptions.ProjectsLogicChanged = new Subscriptions.ProjectsLogicChangedSubscription(new[] {launchArguments.projectId});
                        Subscriptions.ProjectsLogicChanged.Subscribe();

                        if (Subscriptions.LibraryChanged != null)
                        {
                            Subscriptions.LibraryChanged.Unsubscribe();
                        }
                        
                        Subscriptions.LibraryChanged = new Subscriptions.LibraryChangedSubscription();
                        Subscriptions.LibraryChanged.Subscribe();

                        Subscriptions.ProjectBackup = new Subscriptions.RestoreSceneBackupSubscription();
                        Subscriptions.ProjectBackup.Subscribe();

                        ProjectData.OnLaunchSignal();
                        ProjectDataListener.Instance.LaunchWithArguments(launchArguments);
                    }

                    break;
                case PipeCommandType.Terminate:
                    ProjectData.OnTerminateSignal();
                    break;
                case PipeCommandType.RunningBlocks:
                    break;
                case PipeCommandType.RuntimeError:
                    break;
                case PipeCommandType.Restart:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static LaunchArguments BuildLaunchArguments(PipeCommand command)
        {
            var launchArguments = new LaunchArguments()
            {
                multiplayerMode = command.LaunchParams.MultiplayerMode,
                gm = command.LaunchParams.LaunchMode,
                lang = command.LaunchParams.Lang,
                projectId = command.LaunchParams.ProjectId,
                platformMode = command.LaunchParams.PlatformMode,
                projectConfigurationId = command.LaunchParams.ProjectConfigurationId,
                sceneId = command.LaunchParams.SceneId
            };

#if UNITY_EDITOR
            if (launchArguments.multiplayerMode == MultiplayerMode.Undefined)
            {
                var multiplayerMode = UnityEditor.SessionState.GetInt("MultiplayerMode", 0);
                switch (multiplayerMode)
                {
                    case 0:
                        launchArguments.multiplayerMode = MultiplayerMode.Single;
                        break;
                    case 1:
                        launchArguments.multiplayerMode = MultiplayerMode.Host;
                        launchArguments.platformMode = PlatformMode.Desktop;
                        break;
                    case 2:
                        launchArguments.multiplayerMode = MultiplayerMode.Client;
                        launchArguments.gm = GameMode.Undefined;
                        launchArguments.platformMode = PlatformMode.Desktop;
                        launchArguments.projectId = -1;
                        launchArguments.projectConfigurationId = -1;
                        break;
                }
            }
#endif
            
            return launchArguments;
        }

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ProjectData.SceneCleared += OnSceneCleared;
        }

        private void OnSceneCleared()
        {
            StartCoroutine(AwaitSceneInitialization());
        }

        private IEnumerator AwaitSceneInitialization()
        {
            _sceneInitialized = false;
            
            ProjectData.ObjectInitialSpawnProcessCompleted += OnInitialSpawnProcessComplete;
            var spawnPointInitialized = false;

            yield return new WaitWhile(() => !spawnPointInitialized);
            
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForEndOfFrame();
            }

            ProjectData.ObjectInitialSpawnProcessCompleted -= OnInitialSpawnProcessComplete;
            
            _sceneInitialized = true;

            void OnInitialSpawnProcessComplete()
            {
                spawnPointInitialized = true;
            }
        }

        private void LateUpdate()
        {
            while (_receiveQueue.TryDequeue(out string data))
            {
                try
                {
                    Debug.Log($"Pipe receive: {data}");
                    var command = JsonConvert.DeserializeObject<PipeCommand>(data);
                    HandleCommand(command);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }

        private void OnDestroy()
        {
            _pipeHandler?.Dispose();

            ProjectData.SceneCleared -= OnSceneCleared;
            Debug.Log("Pipe handling end");
        }
    }
}