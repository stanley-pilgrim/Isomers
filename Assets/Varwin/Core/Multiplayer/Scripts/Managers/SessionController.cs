using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Varwin.Data;
using Varwin.Data.ServerData;
using Varwin.UI;
using Varwin.WWW;

namespace Varwin.Multiplayer
{
    /// <summary>
    /// Контроллер игровой сессии.
    /// </summary>
    public class SessionController : MonoBehaviour
    {
        /// <summary>
        /// Максимальное количество игроков в сетевой сессии.
        /// </summary>
        public const int MaxPlayersCount = 8;

        /// <summary>
        /// При подключении/отключении игрока.
        /// </summary>
        /// <param name="wrapper">Враппер.</param>
        public delegate void ClientEventHandler(NetworkPlayerWrapper wrapper);

        /// <summary>
        /// Событие, вызываемое при подключении игрока.
        /// </summary>
        public event ClientEventHandler ClientConnected;
        
        /// <summary>
        /// Событие, вызываемое при отключении игрока.
        /// </summary>
        public event ClientEventHandler ClientDisconnected;
        
        /// <summary>
        /// Синглтон менеджера сессии.
        /// </summary>
        public static SessionController Instance { get; private set; }
        
        /// <summary>
        /// Префаб аватара.
        /// </summary>
        public NetworkObject PlayerAvatar;
        
        /// <summary>
        /// Созданные аватары.
        /// </summary>
        private Dictionary<ulong, NetworkObject> _avatarInstances = new();

        /// <summary>
        /// Очередь подключенных клиентов, ожидающих спавна аватара
        /// </summary>
        private Queue<ulong> _waitForSpawnClients = new();

        /// <summary>
        /// Текущий режим платформы.
        /// </summary>
        private PlatformMode _platformMode = PlatformMode.Desktop;
        
        /// <summary>
        /// Идентификатор для инициализации игроков в ECS.
        /// </summary>
        private static int _id = 10000;

        /// <summary>
        /// Подключенные игроки.
        /// </summary>
        public Dictionary<ulong, NetworkPlayer> NetworkPlayers { get; private set; }

        /// <summary>
        /// Загружена ли сцена сессии
        /// </summary>
        private bool _sessionSceneLoaded;
        
        /// <summary>
        /// Инициализация.
        /// </summary>
        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
        
        /// <summary>
        /// Подписка на старт проекта.
        /// </summary>
        private void Start()
        {
            ((VarwinNetworkManager) NetworkManager.Singleton).Disconnected += OnDisconnected;
            ((VarwinNetworkManager) NetworkManager.Singleton).StartProjectReceived += OnSessionStarted;
            ((VarwinNetworkManager) NetworkManager.Singleton).OnClientDisconnectCallback += OnClientDisconnected;
        }

        /// <summary>
        /// При запуске сессии запуск проекта.
        /// </summary>
        /// <param name="address">Адрес RMS.</param>
        /// <param name="accessToken">Токен доступа.</param>
        /// <param name="refreshToken">Токен обновления.</param>
        /// <param name="projectId">ИД проекта.</param>
        /// <param name="sceneId">ИД сцены.</param>
        /// <param name="configurationId">ИД конфигурации.</param>
        /// <param name="licenseinfo"></param>
        private void OnSessionStarted(string address, string accessToken, string refreshToken, int projectId, int sceneId, int configurationId, string licenseinfo)
        {
            Settings.Instance.ApiHost = address;
            Request.AccessToken = accessToken;
            Request.RefreshToken = refreshToken;

            var launchArguments = new LaunchArguments
            {
                gm = GameMode.View,
                multiplayerMode = MultiplayerMode.Client,
                platformMode = _platformMode,
                projectId = projectId,
                sceneId = sceneId,
                projectConfigurationId = configurationId
            };

            StartCoroutine(StartProject(launchArguments));
        }

        /// <summary>
        /// При отключении игрока.
        /// </summary>
        private void OnDisconnected()
        {
            DisconnectFromHost();
        }

        /// <summary>
        /// Задать режим предпросмотра.
        /// </summary>
        /// <param name="platformMode"></param>
        public void SetPlatformMode(PlatformMode platformMode)
        {
            _platformMode = platformMode;
        }
        
        /// <summary>
        /// При отключении игрока.
        /// </summary>
        /// <param name="clientId">Идентификатор клиента.</param>
        private void OnClientDisconnected(ulong clientId)
        {
            if (clientId == NetworkManager.ServerClientId && SceneManager.GetActiveScene().buildIndex != -1)
            {
                DisconnectFromHost();
            }
            
            if (SceneManager.GetActiveScene().buildIndex != -1)
            {
                return;
            }
            
            if (NetworkManager.Singleton.IsServer)
            {
                if (!_avatarInstances.ContainsKey(clientId))
                {
                    return;
                }

                _avatarInstances[clientId].Despawn();
                _avatarInstances.Remove(clientId);
                ClientDisconnected?.Invoke((NetworkPlayerWrapper) NetworkPlayers[clientId].Wrapper());
                NetworkPlayers.Remove(clientId);

                return;
            }

            if (!NetworkManager.Singleton.IsServer && clientId == NetworkManager.ServerClientId)
            {
#if VARWINCLIENT
                var messageText = "MULTIPLAYER_HOST_DISCONNECTED_MESSAGE".Localize();
                var buttonText = "MULTIPLAYER_HOST_DISCONNECTED_BUTTON".Localize();
#else
                var messageText = "Host has been disconnected";
                var buttonText = "Quit";
#endif
                PopupWindowManager.ShowPopup(
                        messageText: messageText,
                        enableButton: true,
                        buttonText: buttonText,
                        onClose: DisconnectFromHost
                    );
            }
        }

        /// <summary>
        /// Метод исключения игрока из сессии.
        /// </summary>
        private void DisconnectFromHost()
        {
            return;
#if UNITY_ANDROID
            GameStateData.ClearObjects();
            GameStateData.ClearLogic();

            ProjectData.ProjectStructure = null;

            try
            {
                ProjectData.UpdateSubscribes(new RequiredProjectArguments() {GameMode = GameMode.View, PlatformMode = PlatformMode.Vr});
            }
            catch
            {
            }
            finally
            {
                ProjectDataListener.Instance.LoadScene("MobileLauncher");
            }

            NetworkManager.Singleton.Shutdown();
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// При подключении игрока.
        /// </summary>
        /// <param name="clientId">Идентификатор клиента.</param>
        private void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton.IsServer && !_waitForSpawnClients.Contains(clientId))
            {
                _waitForSpawnClients.Enqueue(clientId);
            }

            if (_sessionSceneLoaded)
            {
                SpawnConnectedClients();
            }
        }

        /// <summary>
        /// Спаун аватара для заданного идентификатора клиента.
        /// </summary>
        /// <param name="clientId">Идентификатор клиента.</param>
        private void SpawnAvatar(ulong clientId)
        {
            if (_avatarInstances.ContainsKey(clientId))
            {
                return;
            }

            var playerInstance = Instantiate(PlayerAvatar);
            playerInstance.SpawnAsPlayerObject(clientId);
            playerInstance.ChangeOwnership(clientId);
            _avatarInstances.Add(clientId, playerInstance);
            InitObjectController(playerInstance);
                        
            NetworkPlayers ??= new Dictionary<ulong, NetworkPlayer>();
            var networkPlayer = playerInstance.gameObject.GetComponent<NetworkPlayer>();
            NetworkPlayers.Add(clientId, networkPlayer);
        }

        /// <summary>
        /// Инициализация внутренней конфигурации объекта-аватара.
        /// </summary>
        /// <param name="playerInstance">Экземпляр игрового аватара.</param>
        private void InitObjectController(NetworkObject playerInstance)
        {
            var initObjectParams = new InitObjectParams();
            initObjectParams.Id = _id++;
            initObjectParams.Index = initObjectParams.Id;
            initObjectParams.Name = "NetworkPlayer";
            initObjectParams.IdServer = initObjectParams.Id;
            initObjectParams.IdScene = initObjectParams.Id;
            initObjectParams.RootGameObject = playerInstance.gameObject;
            initObjectParams.Asset = playerInstance.gameObject;
            initObjectParams.WrappersCollection = GameStateData.GetWrapperCollection();
            var objectController = new ObjectController(initObjectParams);
        }
        
        /// <summary>
        /// Запуск проекта с учетом параметров запуска.
        /// </summary>
        private IEnumerator StartProject(LaunchArguments launchArguments)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                yield return StartProjectHost(launchArguments);
            }
            else
            {
                yield return StartProjectClient(launchArguments);
            }
        }

        /// <summary>
        /// Запустить проект на стороне хоста
        /// </summary>
        /// <returns></returns>
        private IEnumerator StartProjectHost(LaunchArguments launchArguments)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                yield break;
            }

            var sceneLoaded = false;
            ProjectData.GameMode = launchArguments.gm;
            ProjectData.PlatformMode = launchArguments.platformMode;
            LoaderAdapter.LoadProject(launchArguments.projectId, launchArguments.sceneId, launchArguments.projectConfigurationId, true);
            ProjectData.SceneLoaded += OnSceneLoaded;

            void OnSceneLoaded()
            {
                sceneLoaded = true;
                _sessionSceneLoaded = true;
                ProjectData.SceneLoaded -= OnSceneLoaded;
            }

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            yield return new WaitWhile(() => !sceneLoaded);
            yield return new WaitForEndOfFrame();

            ProjectData.PlatformMode = _platformMode;
            SpawnConnectedClients();

            MultiplayerHelper.AddSyncComponentsToVarwinObjects();

            yield return new WaitForEndOfFrame();
            
            foreach (var networkObject in FindObjectsOfType<NetworkObject>(true))
            {
                if (!networkObject.IsSpawned)
                {
                    networkObject.Spawn();
                }
            }

            SpawnAvatar(NetworkManager.Singleton.LocalClient.ClientId);
        }

        /// <summary>
        /// Спавн аватаров для клиентов из очереди.
        /// </summary>
        private void SpawnConnectedClients()
        {
            if (!NetworkManager.Singleton.IsServer || _waitForSpawnClients.Count == 0)
            {
                return;
            }

            while (_waitForSpawnClients.Count > 0)
            {
                SpawnAvatar(_waitForSpawnClients.Dequeue());
            }
        }

        /// <summary>
        /// Запустить проект на стороне клиента
        /// </summary>
        /// <returns></returns>
        private IEnumerator StartProjectClient(LaunchArguments launchArguments)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                yield break;
            }
            
            NetworkManager.Singleton.Shutdown();
            bool sceneLoaded = false;
            LoaderAdapter.LoadProject(launchArguments);
            ProjectData.SceneLoaded += OnSceneLoaded;

            void OnSceneLoaded()
            {
                sceneLoaded = true;
                ProjectData.SceneLoaded -= OnSceneLoaded;
            }
            yield return new WaitWhile(() => !sceneLoaded);
            StartCoroutine(WaitAndDisableClientLogic());

            yield return new WaitForEndOfFrame();
            ProjectData.PlatformMode = _platformMode;

            yield return new WaitWhile(() => NetworkManager.Singleton.LocalClient != null);

            MultiplayerHelper.AddSyncComponentsToVarwinObjects();
            
            yield return new WaitForEndOfFrame();
            NetworkManager.Singleton.StartClient();
        }

        /// <summary>
        /// Удаление логики на клиенте.
        /// </summary>
        private IEnumerator WaitAndDisableClientLogic()
        {
            yield return new WaitWhile(() => !SceneLogic.Instance);
            Destroy(SceneLogic.Instance.gameObject);
            UIFadeInOutController.IsBlocked = false;
        }

        /// <summary>
        /// Отписка.
        /// </summary>
        private void OnDestroy()
        {
            if (!NetworkManager.Singleton)
            {
                return;
            }

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            ((VarwinNetworkManager) NetworkManager.Singleton).Disconnected -= OnDisconnected;
        }
    }
}