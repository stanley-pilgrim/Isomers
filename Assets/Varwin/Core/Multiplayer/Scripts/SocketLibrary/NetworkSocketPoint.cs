using Unity.Netcode;
using Varwin.SocketLibrary;

namespace Varwin.Multiplayer
{
    public class NetworkSocketPoint : NetworkBehaviour
    {
        private SocketPoint _socketPoint;
        private PlugPoint _previewPlugPoint;
        private NetworkSocketController _networkSocketController;

        public bool IsConnecting => _socketPoint && _previewPlugPoint;

        public override void OnNetworkSpawn()
        {
            _socketPoint = GetComponent<SocketPoint>();
            _networkSocketController = GetComponentInParent<NetworkSocketController>();
            if (IsServer)
            {
                _socketPoint.OnConnect += OnConnect;
                _socketPoint.OnDisconnect += OnDisconnect;
                _socketPoint.OnPreviewShow += OnPreviewShow;
                _socketPoint.OnPreviewHide += OnPreviewHide;
            }
            else
            {
                _socketPoint.CanConnect = false;
            }
        }

        private void OnConnect(PlugPoint plugPoint, SocketPoint socketPoint)
        {
            var plugPointNetworkObject = plugPoint.GetComponent<NetworkObject>();
            if (!plugPointNetworkObject)
            {
                return;
            }

            ConnectClientRpc(plugPointNetworkObject.GlobalObjectIdHash, !_networkSocketController.SocketController.IsGrabbed);
        }

        private void OnDisconnect(PlugPoint plugPoint, SocketPoint socketPoint)
        {
            DisconnectClientRpc();
        }

        private void OnPreviewShow(PlugPoint plugPoint, SocketPoint socketPoint)
        {
            var networkObject = plugPoint.GetComponent<NetworkObject>();
            if (!networkObject)
            {
                return;
            }

            ShowPreviewClientRpc(networkObject.GlobalObjectIdHash);
        }

        private void OnPreviewHide(PlugPoint plugPoint, SocketPoint socketPoint)
        {
            HidePreviewClientRpc();
        }

        [ClientRpc]
        private void ShowPreviewClientRpc(ulong plugPointGlobalHashId)
        {
            if (IsServer)
            {
                return;
            }

            var plugPoint = JointPoint.InstancedPlugPoints.Find(a => a.GetComponent<NetworkObject>()?.GlobalObjectIdHash == plugPointGlobalHashId);
            if (!plugPoint)
            {
                return;
            }

            _previewPlugPoint = plugPoint;
        }

        [ClientRpc]
        private void HidePreviewClientRpc()
        {
            if (IsServer || !_previewPlugPoint)
            {
                return;
            }

            _previewPlugPoint.SocketController.PreviewBehaviour.Hide();
            _socketPoint.SocketController.PreviewBehaviour.Hide();
            _socketPoint.SocketController.ConnectionGraphBehaviour.ForEach(controller => controller.ResetHighlight());
            _previewPlugPoint.SocketController.ConnectionGraphBehaviour.ForEach(controller => controller.ResetHighlight());
            _previewPlugPoint = null;
        }

        [ClientRpc]
        private void ConnectClientRpc(ulong plugPointGlobalHashId, bool destroyCollisionController)
        {
            if (IsServer)
            {
                return;
            }

            var plugPoint = JointPoint.InstancedPlugPoints.Find(a => a.GetComponent<NetworkObject>()?.GlobalObjectIdHash == plugPointGlobalHashId);
            if (!plugPoint)
            {
                return;
            }

            var oldPlugPointCanConnect = plugPoint.CanConnect;
            plugPoint.CanConnect = true;
            _socketPoint.CanConnect = true;
            SocketController.Connect(plugPoint, _socketPoint);
            _socketPoint.CanConnect = false;
            plugPoint.CanConnect = oldPlugPointCanConnect;

            if (destroyCollisionController)
            {
                _socketPoint.SocketController.ConnectionGraphBehaviour.ForEach(a => a.CollisionProvider.OnGrabEnd());
            }
        }

        [ClientRpc]
        private void DisconnectClientRpc()
        {
            if (IsServer)
            {
                return;
            }

            SocketController.Disconnect(_socketPoint);
        }

        private void LateUpdate()
        {
            if (IsServer)
            {
                return;
            }
            
            if (_socketPoint && _previewPlugPoint)
            {
                _previewPlugPoint.SocketController.ConnectionGraphBehaviour.ForEach(controller => controller.SetJoinHighlight());
                _socketPoint.SocketController.ConnectionGraphBehaviour.ForEach(controller => controller.SetJoinHighlight());
                
                _previewPlugPoint.SocketController.PreviewBehaviour.Show(_socketPoint, _previewPlugPoint);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                _socketPoint.OnConnect -= OnConnect;
                _socketPoint.OnDisconnect -= OnDisconnect;
                _socketPoint.OnPreviewShow -= OnPreviewShow;
                _socketPoint.OnPreviewHide -= OnPreviewHide;
            }
        }
    }
}