using System.Linq;
using UnityEngine;
using Varwin.SocketLibrary;

namespace Varwin.Multiplayer
{
    public class NetworkSocketController : MonoBehaviour, INetworkObjectsChain
    {
        public SocketController SocketController { get; private set; }
        private NetworkInteractableObject _networkInteractableObject;
        private NetworkSocketPoint[] _socketPoints;
        private NetworkPlugPoint[] _plugPoints;
            
        private void Awake()
        {
            SocketController = GetComponent<SocketController>();
            _networkInteractableObject = GetComponent<NetworkInteractableObject>();
            _networkInteractableObject.IsGrabbed.OnValueChanged += OnGrabbedValueChanged;
        }

        private void Start()
        {
            _socketPoints = GetComponentsInChildren<NetworkSocketPoint>();
            _plugPoints = GetComponentsInChildren<NetworkPlugPoint>();
        }

        private void OnGrabbedValueChanged(NetworkInteractionState previousValue, NetworkInteractionState newValue)
        {
            if (_networkInteractableObject.NetworkManager.IsServer)
            {
                return;
            }

            if (!IsGrabbed())
            {
                SocketController.ConnectionGraphBehaviour.ForceStopSubscribe();
                SocketController.ConnectionGraphBehaviour.ForEach(a=>a.CollisionProvider.OnGrabEnd());
            }
        }

        public bool IsGrabbed()
        {
            var isGrabbed = false;
            SocketController.ConnectionGraphBehaviour.ForEach(a => isGrabbed |= a.SelfObject.GetComponent<NetworkInteractableObject>().IsGrabbed.Value.State);
            return isGrabbed;
        }

        public bool IsLocalGrabbed()
        {
            var isGrabbed = false;
            SocketController.ConnectionGraphBehaviour.ForEach(a => isGrabbed |= a.SelfObject.GetComponent<NetworkInteractableObject>().IsLocalGrabbed);
            return isGrabbed;
        }

        private void LateUpdate()
        {
            if (_networkInteractableObject.NetworkManager.IsServer)
            {
                return;
            }
            
            if (_socketPoints == null || _socketPoints.Length == 0)
            {
                return;
            }

            if (_socketPoints.Any(a => a.IsConnecting) || _plugPoints.Any(a=>a.IsConnecting))
            {
                SocketController.CollisionProvider.CheckCanDrop();
            }
        }

        private void OnDestroy()
        {
            _networkInteractableObject.IsGrabbed.OnValueChanged -= OnGrabbedValueChanged;
        }
    }
}