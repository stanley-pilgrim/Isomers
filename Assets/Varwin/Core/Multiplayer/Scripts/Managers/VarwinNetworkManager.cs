using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Varwin.Data.ServerData;
using Varwin.WWW;
using SystemInfo = UnityEngine.Device.SystemInfo;

namespace Varwin.Multiplayer
{
    /// <summary>
    /// Класс для управления соединением.
    /// Стартует клиента/хоста.
    /// Обменивается сетевыми сообщениями.
    /// </summary>
    public class VarwinNetworkManager : NetworkManager
    {
        /// <summary>
        /// Делегат для события получения сетевых данных о проекте.
        /// </summary>
        /// <param name="address">Адрес RMS.</param>
        /// <param name="accessToken">Access Token RMS хоста.</param>
        /// <param name="refreshToken">Refresh Token RMS хоста.</param>
        /// <param name="projectId">Id скачиваемого проекта.</param>
        /// <param name="sceneId">Id скачиваемой сцены.</param>
        /// <param name="configurationId">Id конфигурации сетевого проекта.</param>
        /// <param name="versionInfo">Информация о версии.</param>
        public delegate void LoadProjectDelegate(string address, string accessToken, string refreshToken, int projectId, int sceneId, int configurationId, string versionInfo);

        public const string PlayerConnectedMessage = "PlayerConnected";
        public const string StartProjectMessage = "StartProject";
        public const string LoadedProjectMessage = "LoadedProject";
        public const string DisconnectPlayerMessage = "DisconnectPlayer";

        /// <summary>
        /// Словарь подключенных игроков.
        /// </summary>
        public Dictionary<ulong, NetworkPlayerData> ConnectedPlayers = new();

        /// <summary>
        /// Информация о всех когда-либо подключенных клиентах.
        /// </summary>
        private List<NetworkPlayerData> _allConnectedPlayersData = new();

        /// <summary>
        /// Информация о локальном игроке.
        /// </summary>
        private NetworkPlayerData _localPlayerData;

        /// <summary>
        /// Событие изменения состояния подключенного игрока.
        /// </summary>
        public event Action ConnectedPlayersChanged;

        /// <summary>
        /// Событие получения данных о загрузке проекта.
        /// </summary>
        public event LoadProjectDelegate StartProjectReceived;

        /// <summary>
        /// Событие отключения игрока.
        /// </summary>
        public event Action Disconnected;

        /// <summary>
        /// Флаг подписки на события подключения и отключения игроков.
        /// </summary>
        private bool _isSubscribed;
        
        /// <summary>
        /// Количество игроков в лобби.
        /// </summary>
        public int StartProjectPlayerCount { get; private set; }

        /// <summary>
        /// Информация о запускаемом проекте.
        /// </summary>
        public ProjectInfo ProjectInfo;

        /// <summary>
        /// Генерация данных о локальном игроке.
        /// </summary>
        protected override void OnAwake()
        {
            if (Singleton)
            {
                Destroy(gameObject);
            }
            
            _localPlayerData = new()
            {
                Nickname = $"Player {SystemInfo.deviceName}",
                LocalId = Guid.NewGuid().ToString(),
                IsMobileVr = ProjectData.IsMobileClient
            };
        }

        /// <summary>
        /// Функция, вызываемая при попытке подключения к хосту.
        /// Получает данные о подключаемых клиентах.
        /// Подтверждает или отклоняет входящие подключения.
        /// Работает только с включенным флагом \"approval check\" в инспекторе Network Manager'а.
        /// </summary>
        /// <param name="request">Входящий запрос на подключение.</param>
        /// <param name="response">Ответ на подключение.</param>
        private void ApprovalCheck(ConnectionApprovalRequest request, ConnectionApprovalResponse response)
        {
            var approved = ProjectData.IsMultiplayerSceneActive;
            var clientId = request.ClientNetworkId;
            var playerData = JsonConvert.DeserializeObject<NetworkPlayerData>(System.Text.Encoding.ASCII.GetString(request.Payload));

            approved |= _allConnectedPlayersData.FirstOrDefault(x => x.LocalId == playerData.LocalId) != null;
            approved &= !ProjectInfo.IsMobileReady && playerData.IsMobileVr ? false : true;
            approved &= Singleton.ConnectedClients.Count + 1 < SessionController.MaxPlayersCount;

            response.Approved = approved;
            playerData.NetworkId = clientId;

            if (ConnectedPlayers.ContainsKey(clientId) || !approved)
            {
                return;
            }

            ConnectedPlayers.Add(clientId, playerData);
            _allConnectedPlayersData.Add(playerData);
            ConnectedPlayersChanged?.Invoke();
        }

        /// <summary>
        /// <inheritdoc cref="NetworkManager.StartClient"/>
        /// Старт сетевого клиента.
        /// В случае успешного подключения регистрация обработчиков сетевых сообщений.
        /// </summary>
        /// <returns>true - если клиент успешно запущен. false - если клиент не смог запуститься.</returns>
        public override bool StartClient()
        {
            var result = base.StartClient();
            NetworkConfig.ConnectionData = System.Text.Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(_localPlayerData));
            if (result)
            {
                CustomMessagingManager.RegisterNamedMessageHandler(PlayerConnectedMessage, OnPlayersInfoReceived);
                CustomMessagingManager.RegisterNamedMessageHandler(DisconnectPlayerMessage, OnDisconnectMessageReceived);
                CustomMessagingManager.RegisterNamedMessageHandler(StartProjectMessage, OnStartProjectMessageReceived);
            }

            return result;
        }

        /// <summary>
        /// При получении сигнала о старте проекта.
        /// </summary>
        /// <param name="senderClientId">Id отправителя сообщения.</param>
        /// <param name="messagePayload">Сетевые данные сообщения.</param>
        private void OnStartProjectMessageReceived(ulong senderClientId, FastBufferReader messagePayload)
        {
            messagePayload.ReadValueSafe(out string apiHost);
            messagePayload.ReadValueSafe(out string accessToken);
            messagePayload.ReadValueSafe(out string refreshToken);
            messagePayload.ReadValueSafe(out int projectId);
            messagePayload.ReadValueSafe(out int sceneId);
            messagePayload.ReadValueSafe(out int projectConfigurationId);
            messagePayload.ReadValueSafe(out string versionNumber);

            StartProjectReceived?.Invoke(apiHost, accessToken, refreshToken, projectId, sceneId, projectConfigurationId, versionNumber);
        }

        /// <summary>
        /// Обработчик сетевого сообщения об отключении игрока.
        /// </summary>
        /// <param name="senderClientId">Id отправителя сообщения.</param>
        /// <param name="messagePayload">Сетевые данные сообщения.</param>
        private void OnDisconnectMessageReceived(ulong senderClientId, FastBufferReader messagePayload)
        {
            Shutdown();
            Disconnected?.Invoke();
        }

        /// <summary>
        /// Обработчик сообщения об изменении данных клиента.
        /// </summary>
        /// <param name="senderClientId">Id отправителя сообщения.</param>
        /// <param name="messagePayload">Сетевые данные сообщения.</param>
        private void OnPlayersInfoReceived(ulong senderClientId, FastBufferReader messagePayload)
        {
            messagePayload.ReadValueSafe(out string playersJson, true);
            ConnectedPlayers = JsonConvert.DeserializeObject<Dictionary<ulong, NetworkPlayerData>>(playersJson);
            ConnectedPlayersChanged?.Invoke();
        }

        /// <summary>
        /// Метод для старта подключения к хосту.
        /// </summary>
        /// <param name="address">Адрес хоста.</param>
        public void ConnectToHost(string address)
        {
            var transport = GetComponent<UnityTransport>();
            transport.ConnectionData.Address = address;
            StartClient();
        }

        /// <summary>
        /// Старт хоста с задержкой на получение сетевого адреса.
        /// </summary>
        /// <returns>Всегда возвращает true.</returns>
        public override bool StartHost()
        {
            ConnectedPlayers.Clear();
            StartCoroutine(GetLaunchInfo((address) =>
            {
                ((UnityTransport)NetworkConfig.NetworkTransport).ConnectionData.Address = address;
                var result = base.StartHost();
                ConnectionApprovalCallback = ApprovalCheck;
                NetworkConfig.ConnectionData = System.Text.Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(_localPlayerData));
                ConnectedPlayers.Add(ServerClientId, _localPlayerData);
                if (result)
                {
                    CustomMessagingManager.RegisterNamedMessageHandler(StartProjectMessage, OnStartProjectMessageReceived);
                }
                else
                {
                    Debug.LogError("Can't start host");
                }

                if (!_isSubscribed)
                {
                    OnClientConnectedCallback += OnClientConnected;
                    OnClientDisconnectCallback += OnClientDisconnected;
                    _isSubscribed = true;
                }
            }));

            return true;
        }

        /// <summary>
        /// При получении данных для хоста.
        /// </summary>
        /// <param name="onIPReceived">Событие, вызываемое после загрузки IP адреса.</param>
        private IEnumerator GetLaunchInfo(Action<string> onIPReceived)
        {
            ProjectStructure projectStructure = null;
            API.GetProjectMeta(ProjectInfo.ProjectId, (result) => projectStructure = result);
            yield return new WaitWhile(() => projectStructure == null);
            ProjectInfo.IsMobileReady = projectStructure.MobileReady;
            yield return GetApiAddress(onIPReceived);
        }
        
        /// <summary>
        /// Получение сетевого адреса.
        /// </summary>
        /// <param name="onComplete"></param>
        /// <returns></returns>
        private IEnumerator GetApiAddress(Action<string> onComplete)
        {
            const float GetApiAddressTimeout = 5f;
            var endTime = Time.unscaledTime + GetApiAddressTimeout;

            while (string.IsNullOrEmpty(Settings.Instance.RemoteAddress))
            {
                if (Time.unscaledTime > endTime)
                {
                    Debug.LogWarning("Fail to start host. Can't get API url");
                    yield break;                    
                }

                yield return new WaitForEndOfFrame();
            }

            var regexIp = Regex.Match(Settings.Instance.RemoteAddress, @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");
            var ip = regexIp.Groups[0].Value;

            if (IPAddress.TryParse(ip, out var address))
            {
                onComplete?.Invoke(address.ToString());
            }
        }

        /// <summary>
        /// Обработчик события отключения клиента.
        /// </summary>
        /// <param name="clientId">Id отключаемого игрока.</param>
        private void OnClientDisconnected(ulong clientId)
        {
            ConnectedPlayers.Remove(clientId);
            SendPlayersMessage();
            ConnectedPlayersChanged?.Invoke();
        }

        /// <summary>
        /// Обработчик события подключения клиента.
        /// </summary>
        /// <param name="clientId">Id подключаемого клиента.</param>
        private void OnClientConnected(ulong clientId)
        {
            if (!ConnectedPlayers.ContainsKey(clientId))
            {
                return;
            }

            SendPlayersMessage();
        }

        /// <summary>
        /// Метод для отправки сетевых сообщения.
        /// </summary>
        private void SendPlayersMessage()
        {
            var json = JsonConvert.SerializeObject(ConnectedPlayers);
            var writer = new FastBufferWriter(json.Length + 24, Allocator.Temp);
            writer.WriteValueSafe(json, true);
            CustomMessagingManager.SendNamedMessageToAll(PlayerConnectedMessage, writer);
        }

        /// <summary>
        /// Метод для отправки сетевого сообщения об отключении клиента.
        /// Клиент, получивший это сообщение будет отключен от сессии.
        /// </summary>
        /// <param name="clientId">Id отключаемого клиента.</param>
        public void SendDisconnectMessage(ulong clientId)
        {
            var writer = new FastBufferWriter(10, Allocator.Temp);
            CustomMessagingManager.SendNamedMessage(DisconnectPlayerMessage, clientId, writer);
        }

        /// <summary>
        /// Метод для отправки сообщения хосту о готовности клиента к старту сессии.
        /// </summary>
        public void SendIsReadyMessage()
        {
            var writer = new FastBufferWriter(10, Allocator.Temp);
            CustomMessagingManager.SendNamedMessage(LoadedProjectMessage, ServerClientId, writer);
        }

        /// <summary>
        /// Метод для отправки сообщения всем клиентам о старте игровой сессии.
        /// </summary>
        public void SendStartProjectMessage()
        {
            StartProjectPlayerCount = ConnectedClients.Count;
            var writer = new FastBufferWriter(1100, Allocator.Temp);
            writer.WriteValueSafe(Settings.Instance.RemoteWebHost);
            writer.WriteValueSafe(Request.AccessToken);
            writer.WriteValueSafe(Request.RefreshToken);
            writer.WriteValueSafe(ProjectInfo.ProjectId);
            writer.WriteValueSafe(ProjectInfo.SceneId);
            writer.WriteValueSafe(ProjectInfo.ProjectConfigurationId);
            writer.WriteValueSafe(VarwinVersionInfoContainer.VersionNumber);
            CustomMessagingManager.SendNamedMessageToAll(StartProjectMessage, writer);
        }

        /// <summary>
        /// Запуск проекта.
        /// </summary>
        public void StartProject()
        {
            StartProjectReceived?.Invoke(Settings.Instance.RemoteWebHost, Request.AccessToken, Request.RefreshToken, ProjectInfo.ProjectId, ProjectInfo.SceneId, ProjectInfo.ProjectConfigurationId, VarwinVersionInfoContainer.VersionNumber);            
            SendStartProjectMessage();
        }
    }
}