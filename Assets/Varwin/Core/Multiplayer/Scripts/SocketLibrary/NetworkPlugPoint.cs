using Unity.Netcode;
using Varwin.SocketLibrary;

namespace Varwin.Multiplayer
{
    public class NetworkPlugPoint : NetworkBehaviour
    {
        private PlugPoint _plugPoint;
        private SocketPoint _previewSocketPoint;
        
        public bool IsConnecting => _plugPoint && _previewSocketPoint;

        public override void OnNetworkSpawn()
        {
            _plugPoint = GetComponent<PlugPoint>();
            if (IsServer)
            {
                _plugPoint.OnPreviewShow += OnPreviewShow;
                _plugPoint.OnPreviewHide += OnPreviewHide;
            }
        }

        private void OnPreviewShow(PlugPoint plugPoint, SocketPoint socketPoint)
        {
            var networkObject = socketPoint.GetComponent<NetworkObject>();
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
        private void ShowPreviewClientRpc(ulong socketPointGlobalHashId)
        {
            if (IsServer)
            {
                return;
            }

            var socketPoint = JointPoint.InstancedSocketPoints.Find(a => a.GetComponent<NetworkObject>()?.GlobalObjectIdHash == socketPointGlobalHashId);
            if (!socketPoint)
            {
                return;
            }

            _previewSocketPoint = socketPoint;
        }

        [ClientRpc]
        private void HidePreviewClientRpc()
        {
            if (IsServer || !_previewSocketPoint)
            {
                return;
            }

            _previewSocketPoint = null;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                _plugPoint.OnPreviewShow -= OnPreviewShow;
                _plugPoint.OnPreviewHide -= OnPreviewHide;
            }
        }
    }
}