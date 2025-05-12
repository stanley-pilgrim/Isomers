using UnityEngine;
using Varwin.Public;

namespace Varwin.Multiplayer
{
    public class NetworkInteractableObjectBehaviour : MonoBehaviour
    {
        private NetworkInteractableObject _networkInteractableObject;
        private InteractableObjectBehaviour _interactableObjectBehaviour;

        private void Start()
        {
            _networkInteractableObject = GetComponent<NetworkInteractableObject>();
            _interactableObjectBehaviour = GetComponent<InteractableObjectBehaviour>();
            
            if (_networkInteractableObject)
            {
                _networkInteractableObject.IsTouching.OnValueChanged += OnTouchingValueChanged;
                _networkInteractableObject.IsUsing.OnValueChanged += OnUsingValueChanged;
                _networkInteractableObject.IsGrabbed.OnValueChanged += OnGrabbedValueChanged;
            }
        }

        private void OnTouchingValueChanged(NetworkInteractionState previousValue, NetworkInteractionState newValue)
        {
            if (_interactableObjectBehaviour)
            {
                _interactableObjectBehaviour.IsTouched = newValue.State;
            }
        }

        private void OnUsingValueChanged(NetworkInteractionState previousValue, NetworkInteractionState newValue)
        {
            if (_interactableObjectBehaviour)
            {
                _interactableObjectBehaviour.IsUsed = newValue.State;
            }
        }

        private void OnGrabbedValueChanged(NetworkInteractionState previousValue, NetworkInteractionState newValue)
        {
            if (_interactableObjectBehaviour)
            {
                _interactableObjectBehaviour.IsGrabbed = newValue.State;
            }
        }

        private void OnDestroy()
        {
            if (_networkInteractableObject)
            {
                _networkInteractableObject.IsTouching.OnValueChanged -= OnTouchingValueChanged;
                _networkInteractableObject.IsUsing.OnValueChanged -= OnUsingValueChanged;
                _networkInteractableObject.IsGrabbed.OnValueChanged -= OnGrabbedValueChanged;
            }
        }
    }
}