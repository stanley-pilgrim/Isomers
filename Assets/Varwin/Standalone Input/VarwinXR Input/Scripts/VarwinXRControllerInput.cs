using System.Collections.Generic;
using UnityEngine;
using Varwin.PlatformAdapter;

namespace Varwin.XR
{
    public class VarwinXRControllerInput : ControllerInput
    {
        public override event ControllerInteractionEventHandler ControllerEnabled;
        public override event ControllerInteractionEventHandler ControllerDisabled;
        public override event ControllerInteractionEventHandler TriggerPressed;
        public override event ControllerInteractionEventHandler TriggerReleased;
        public override event ControllerInteractionEventHandler TouchpadReleased;
        public override event ControllerInteractionEventHandler TouchpadPressed;
        public override event ControllerInteractionEventHandler ButtonTwoPressed;
        public override event ControllerInteractionEventHandler ButtonTwoReleased;
        public override event ControllerInteractionEventHandler GripPressed;
        public event ControllerInteractionEventHandler TurnLeftPressed;
        public event ControllerInteractionEventHandler TurnRightPressed;

        private VarwinXRInteractableObject _interactableObject;

        private List<GameObject> _controllerEvents = new List<GameObject>();

        public override void AddController(ControllerEvents events)
        {
            if (_controllerEvents.Contains(events.gameObject))
            {
                return;
            }

            _controllerEvents.Add(events.gameObject);

            events.ControllerEnabled += (sender, args) => { ControllerEnabled?.Invoke(sender, args); };
            events.ControllerDisabled += (sender, args) => { ControllerDisabled?.Invoke(sender, args); };

            events.TriggerPressed += (sender, args) => { TriggerPressed?.Invoke(sender, args); };
            events.TriggerReleased += (sender, args) => { TriggerReleased?.Invoke(sender, args); };

            events.TouchpadReleased += (sender, args) => { TouchpadReleased?.Invoke(sender, args); };
            events.TouchpadPressed += (sender, args) => { TouchpadPressed?.Invoke(sender, args); };

            events.ButtonTwoPressed += (sender, args) => { ButtonTwoPressed?.Invoke(sender, args); };
            events.ButtonTwoReleased += (sender, args) => { ButtonTwoReleased?.Invoke(sender, args); };

            events.GripPressed += (sender, args) => { GripPressed?.Invoke(sender, args); };

            ((VarwinXRControllerEvents) events).TurnLeftPressed += (sender, args) => { TurnLeftPressed?.Invoke(sender, args); };
            ((VarwinXRControllerEvents) events).TurnRightPressed += (sender, args) => { TurnRightPressed?.Invoke(sender, args); };
        }

        public VarwinXRControllerInput()
        {
            ControllerEventFactory =
                new ComponentWrapFactory<ControllerEvents, VarwinXRControllerEvents, VarwinXRControllerEventComponent>();
        }

        private class VarwinXRControllerEvents : ControllerEvents, IInitializable<VarwinXRControllerEventComponent>
        {
            private VarwinXRControllerEventComponent _eventComponent = null;
            public override GameObject gameObject => _eventComponent.gameObject;
            private bool _inputActionState;

            public override Transform transform => _eventComponent.transform;
            public override event ControllerInteractionEventHandler ControllerEnabled;
            public override event ControllerInteractionEventHandler ControllerDisabled;
            public override event ControllerInteractionEventHandler TriggerPressed;
            public override event ControllerInteractionEventHandler TriggerReleased;
            public override event ControllerInteractionEventHandler TouchpadReleased;
            public override event ControllerInteractionEventHandler TouchpadPressed;
            public override event ControllerInteractionEventHandler ButtonTwoPressed;
            public override event ControllerInteractionEventHandler ButtonTwoReleased;
            public override event ControllerInteractionEventHandler GripPressed;

            public event ControllerInteractionEventHandler TurnLeftPressed;
            public event ControllerInteractionEventHandler TurnRightPressed;

            public override float GetGripValue() => _eventComponent.GripValue;
            public override float GetTriggerValue() => _eventComponent.TriggerValue;
            public override Vector2 GetTrackpadValue() => _eventComponent.Primary2DAxisValue;
            public override bool IsTouchpadPressed() => _eventComponent.IsThumbstickPressed();
            public override bool IsTouchpadReleased() => _eventComponent.IsThumbstickReleased();
            public override bool IsTriggerPressed() => _eventComponent.IsTriggerPressed();
            public override bool IsTriggerReleased() => _eventComponent.IsTriggerReleased();
            public override bool IsButtonPressed(ButtonAlias gripPress) => _eventComponent.IsButtonPressed(gripPress);

            public override bool GetBoolInputActionState(string actionStateName) => _inputActionState;
            public override bool IsEnabled() => _eventComponent.IsInitialized();

            public override void OnGripReleased(ControllerInteractionEventArgs controllerInteractionEventArgs)
            {
                _eventComponent.OnGripReleased(controllerInteractionEventArgs);
            }

            public override GameObject GetController() => _eventComponent.gameObject.gameObject;

            ControllerInteractionEventArgs GetControllerArguments(VarwinXRControllerEventComponent sender)
            {
                var args = new ControllerInteractionEventArgs()
                {
                    controllerReference = new PlayerController.ControllerReferenceArgs()
                    {
                        hand = !sender.IsLeft ? ControllerInteraction.ControllerHand.Right : ControllerInteraction.ControllerHand.Left
                    }
                };

                return args;
            }
            
            public void Init(VarwinXRControllerEventComponent interactableObject)
            {
                _eventComponent = interactableObject;
                
                _eventComponent.TriggerPressed += (sender) => { TriggerPressed?.Invoke(sender, GetControllerArguments(sender)); };

                _eventComponent.TriggerReleased += (sender) => { TriggerReleased?.Invoke(sender, GetControllerArguments(sender));  };

                _eventComponent.ThumbstickReleased += (sender) => { TouchpadReleased?.Invoke(sender, GetControllerArguments(sender)); };

                _eventComponent.ThumbstickPressed += (sender) => { TouchpadPressed?.Invoke(sender, GetControllerArguments(sender)); };

                _eventComponent.ButtonTwoPressed += (sender) => { ButtonTwoPressed?.Invoke(sender, GetControllerArguments(sender)); };

                _eventComponent.ButtonTwoReleased += (sender) => { ButtonTwoReleased?.Invoke(sender, GetControllerArguments(sender)); };

                _eventComponent.Initialized += sender => { ControllerEnabled?.Invoke(sender, GetControllerArguments(sender)); };
                
                _eventComponent.Deinitialized += sender => { ControllerDisabled?.Invoke(sender, GetControllerArguments(sender)); };
                
                _eventComponent.GripPressed += (sender) =>
                {
                    GripPressed?.Invoke(sender, GetControllerArguments(sender));
                    _inputActionState = !_inputActionState;
                };
                _eventComponent.GripReleased += (sender) =>
                {
                    if (!sender.HasGrabbedObject)
                    {
                        _inputActionState = false;
                    }
                };

                _eventComponent.TurnLeftPressed += (sender) => { TurnLeftPressed?.Invoke(sender, GetControllerArguments(sender)); };

                _eventComponent.TurnRightPressed += (sender) => { TurnRightPressed?.Invoke(sender, GetControllerArguments(sender)); };

                InputAdapter.Instance.ControllerInput.AddController(this);
            }
        }
    }
}