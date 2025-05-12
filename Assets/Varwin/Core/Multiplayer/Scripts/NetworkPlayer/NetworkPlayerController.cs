using System;
using Unity.Netcode;
using UnityEngine;
using Varwin.PlatformAdapter;

namespace Varwin.Multiplayer
{
    /// <summary>
    /// Контроллер игрока на стороне клиента.
    /// </summary>
    public class NetworkPlayerController : NetworkBehaviour
    {
        /// <summary>
        /// Аватар.
        /// </summary>
        public Avatar Avatar;
        
        /// <summary>
        /// Стартовая позиция.
        /// </summary>
        private Vector3 _startPoint;
        
        /// <summary>
        /// Инициализация объекта.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                return;
            }
            
            _startPoint = InputAdapter.Instance.PlayerController.Position;
        }

        /// <summary>
        /// Задать гравитацию.
        /// </summary>
        /// <param name="isGravity">Истина, гравитацию необходимо включить.</param>
        [ClientRpc]
        public void SetGravityClientRpc(bool isGravity)
        {
            if (!IsOwner)
            {
                return;
            }

            PlayerManager.UseGravity = isGravity;
        }

        /// <summary>
        /// Задать телепортацию.
        /// </summary>
        /// <param name="teleportAllowed">Истина, телепортацию необходимо включить.</param>
        [ClientRpc]
        public void SetUseTeleportClientRpc(bool teleportAllowed)
        {
            if (!IsOwner)
            {
                return;
            }

            PlayerManager.SetTeleportEnabled(teleportAllowed);
        }

        /// <summary>
        /// Скрыть луч при отключенной телепортации.
        /// </summary>
        /// <param name="hide">Истина, если скрыть.</param>
        [ClientRpc]
        public void SetHideRayOnTurnedOffTeleportClientRpc(bool hide)
        {
            if (!IsOwner)
            {
                return;
            }

            PlayerManager.SetHideArcOnDisabledTeleport(hide);
        }

        /// <summary>
        /// Задать возможность перемещения WASD.
        /// </summary>
        /// <param name="useWasd">Истина, если использовать.</param>
        [ClientRpc]
        public void SetMovingWasdClientRpc(bool useWasd)
        {
            if (!IsOwner)
            {
                return;
            }

            PlayerManager.SetDesktopWasdMovementEnabled(useWasd);
        }
        
        /// <summary>
        /// Задать возможность отображения курсора.
        /// </summary>
        /// <param name="isVisible">Истина, если использовать.</param>
        [ClientRpc]
        public void SetCursorVisibilityClientRpc(bool isVisible)
        {
            if (!IsOwner)
            {
                return;
            }

            PlayerManager.SetCursorVisible(isVisible);
        }

        /// <summary>
        /// Задать возможность поворота мышью.
        /// </summary>
        /// <param name="mouseLookEnabled">Истина, если использовать.</param>
        [ClientRpc]
        public void SetMouseLookClientRpc(bool mouseLookEnabled)
        {
            if (!IsOwner)
            {
                return;
            }

            PlayerManager.SetMouseLookEnabled(mouseLookEnabled);
        }
        
        /// <summary>
        /// Задать возможность взаимодействия с объектами.
        /// </summary>
        /// <param name="interaction">Истина, если взаимодействие разрешено.</param>
        [ClientRpc]
        public void SetObjectInteractionClientRpc(bool interaction)
        {
            if (!IsOwner)
            {
                return;
            }

            PlayerManager.IsInteractable = interaction;
        }

        /// <summary>
        /// Задать длину луча указки.
        /// </summary>
        /// <param name="length">Длина луча.</param>
        [ClientRpc]
        public void SetRayLengthClientRpc(float length)
        {
            if (!IsOwner)
            {
                return;
            }

            DistancePointer.CustomSettings.Distance = length;
        }

        /// <summary>
        /// Задать ширину луча указки.
        /// </summary>
        /// <param name="part">Часть луча.</param>
        /// <param name="width">Длина луча.</param>
        [ClientRpc]
        public void SetRayWidthClientRpc(RayPart part, float width)
        {
            if (!IsOwner)
            {
                return;
            }

            if (part == RayPart.Begin)
            {
                DistancePointer.CustomSettings.StartWidth = width;
            }
            else
            {
                DistancePointer.CustomSettings.EndWidth = width;
            }
        }

        /// <summary>
        /// Задать цвет луча указки.
        /// </summary>
        /// <param name="part">Часть луча.</param>
        /// <param name="color">Цвет луча.</param>
        [ClientRpc]
        public void SetRayColorClientRpc(RayPart part, Color color)
        {
            if (!IsOwner)
            {
                return;
            }

            if (part == RayPart.Begin)
            {
                DistancePointer.CustomSettings.StartColor = color;
            }
            else
            {
                DistancePointer.CustomSettings.EndColor = color;
            }
        }

        /// <summary>
        /// Задать радиус коллайдеров рук.
        /// </summary>
        /// <param name="radius">Радиус коллайдера.</param>
        [ClientRpc]
        public void SetHandColliderRadiusClientRpc(float radius)
        {
            if (!IsOwner)
            {
                return;
            }

            Avatar.HandColliderRadius.Value = radius;
        }

        /// <summary>
        /// Вернуть в исходное положение.
        /// </summary>
        [ClientRpc]
        public void ReturnToStartPointClientRpc()
        {
            if (!IsOwner)
            {
                return;
            }
            
            InputAdapter.Instance.PlayerController.Teleport(_startPoint);
        }

        /// <summary>
        /// Телепортация к объекту.
        /// </summary>
        /// <param name="targetGameObjectSerializable">Целевой объект.</param>
        [ClientRpc]
        public void TeleportToObjectClientRpc(GameObjectSerializable targetGameObjectSerializable)
        {
            var targetGameObject = targetGameObjectSerializable.GetGameObject();
            if (!targetGameObject)
            {
                return;
            }

            InputAdapter.Instance.PlayerController.Teleport(targetGameObject.transform.position);
        }
        
        /// <summary>
        /// Телепортировать к позиции.
        /// </summary>
        /// <param name="position">Позиция.</param>
        [ClientRpc]
        public void TeleportToClientRpc(Vector3 position)
        {
            if (!IsOwner)
            {
                return;
            }

            InputAdapter.Instance.PlayerController.Teleport(position);
        }

        /// <summary>
        /// Задать позицию.
        /// </summary>
        /// <param name="position">Позиция.</param>
        [ClientRpc]
        public void SetPositionClientRpc(Vector3 position)
        {
            if (!IsOwner)
            {
                return;
            }
            
            PlayerManager.CurrentRig.Position = position;
        }

        /// <summary>
        /// Повернуть на угол.
        /// </summary>
        /// <param name="angle">Угол.</param>
        [ClientRpc]
        public void RotateClientRpc(float angle)
        {
            if (!IsOwner)
            {
                return;
            }

            PlayerManager.Rotate(angle);
        }

        /// <summary>
        /// Повернуть с параметрами.
        /// </summary>
        /// <param name="rotationType">Тип поворота.</param>
        /// <param name="targetObjectSerializable">Целевой объект.</param>
        [ClientRpc]
        public void RotateToClientRpc(RotationType rotationType, GameObjectSerializable targetObjectSerializable)
        {
            if (!IsOwner)
            {
                return;
            }

            var targetObject = targetObjectSerializable.GetGameObject();
            if (!targetObject)
            {
                return;
            }

            switch (rotationType)
            {
                case RotationType.ToObject:
                    PlayerManager.RotateTo(targetObject.transform);
                    break;
                case RotationType.SameAsObject:
                    PlayerManager.SetRotation(targetObject.transform.rotation.eulerAngles);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(rotationType), rotationType, null);
            }
        }

        /// <summary>
        /// Задать поворот.
        /// </summary>
        /// <param name="rotation">Поворот.</param>
        [ClientRpc]
        public void SetRotationClientRpc(Vector3 rotation)
        {
            if (!IsOwner)
            {
                return;
            }

            PlayerManager.SetRotation(rotation);
        }

        /// <summary>
        /// Взять в руку объект.
        /// </summary>
        /// <param name="hand">Рука.</param>
        /// <param name="targetGameObjectSerializable">Целевой объект.</param>
        [ClientRpc]
        public void ForceGrabClientRpc(Hand hand, GameObjectSerializable targetGameObjectSerializable)
        {
            if (!IsOwner)
            {
                return;
            }

            var targetGameObject = targetGameObjectSerializable.GetGameObject();
            if (!targetGameObject)
            {
                return;
            }
            
            if (ProjectData.PlatformMode == PlatformMode.Desktop)
            {
                if (ControllerInteraction.DefaultDesktopIterationHand == ControllerInteraction.ControllerHand.Right)
                {
                    InputAdapter.Instance?.PlayerController?.Nodes?.RightHand?.Controller?.ForceGrabObject(targetGameObject);
                }
                else
                {
                    InputAdapter.Instance?.PlayerController?.Nodes?.LeftHand?.Controller?.ForceGrabObject(targetGameObject);
                }
            }
            else
            {
                if (hand == Hand.Left)
                {
                    InputAdapter.Instance.PlayerController.Nodes.LeftHand.Controller.ForceGrabObject(targetGameObject);
                }
                else
                {
                    InputAdapter.Instance.PlayerController.Nodes.RightHand.Controller.ForceGrabObject(targetGameObject);
                }
            }
        }
        
        /// <summary>
        /// Выбросить предмет из руки.
        /// </summary>
        /// <param name="hand">Рука.</param>
        [ClientRpc]
        public void ForceDropClientRpc(Hand hand)
        {
            if (!IsOwner)
            {
                return;
            }

            if (ProjectData.PlatformMode == PlatformMode.Desktop)
            {
                if (ControllerInteraction.DefaultDesktopIterationHand == ControllerInteraction.ControllerHand.Right)
                {
                    InputAdapter.Instance?.PlayerController?.Nodes?.RightHand?.Controller?.ForceDropObject();
                }
                else
                {
                    InputAdapter.Instance?.PlayerController?.Nodes?.LeftHand?.Controller?.ForceDropObject();
                }
            }
            else
            {
                if (hand == Hand.Left)
                {
                    InputAdapter.Instance.PlayerController?.Nodes?.LeftHand?.Controller.ForceDropObject();
                }
                else
                {
                    InputAdapter.Instance.PlayerController?.Nodes?.RightHand?.Controller.ForceDropObject();
                }
            }
        }

        /// <summary>
        /// Вибрация контроллером.
        /// </summary>
        /// <param name="hand">Рука.</param>
        /// <param name="strength">Сила вибрации.</param>
        /// <param name="duration">Длительность вибрации.</param>
        /// <param name="interval">Интервал.</param>
        [ClientRpc]
        public void VibrateClientRpc(Hand hand, float strength, float duration, float interval)
        {
            if (!IsOwner)
            {
                return;
            }

            VibrateWithParams(hand, strength, duration, interval);
        }

        /// <summary>
        /// Вибрация контроллером.
        /// </summary>
        /// <param name="hand">Рука.</param>
        /// <param name="strength">Сила.</param>
        [ClientRpc]
        public void VibrateClientRpc(Hand hand, Strength strength)
        {
            if (!IsOwner)
            {
                return;
            }

            switch (strength)
            {
                case Strength.Weak:
                    VibrateWithParams(hand, 0.1f, 0.1f, 0.005f);
                    break;
                case Strength.Strong:
                    VibrateWithParams(hand, 0.2f, 0.15f, 0.005f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(strength), strength, null);
            }
        }

        /// <summary>
        /// Вибрация контроллером.
        /// </summary>
        /// <param name="hand">Рука.</param>
        /// <param name="strength">Сила вибрации.</param>
        /// <param name="duration">Длительность вибрации.</param>
        /// <param name="interval">Интервал.</param>
        private void VibrateWithParams(Hand hand, float strength, float duration, float interval)
        {
            switch (hand)
            {
                case Hand.Left:
                    InputAdapter.Instance.PlayerController?.Nodes?.LeftHand?.Controller?.TriggerHapticPulse(strength, duration, interval);
                    break;
                case Hand.Right:
                    InputAdapter.Instance.PlayerController?.Nodes?.RightHand?.Controller?.TriggerHapticPulse(strength, duration, interval);
                    break;
                case Hand.Both:
                    InputAdapter.Instance.PlayerController?.Nodes?.LeftHand?.Controller?.TriggerHapticPulse(strength, duration, interval);
                    InputAdapter.Instance.PlayerController?.Nodes?.RightHand?.Controller?.TriggerHapticPulse(strength, duration, interval);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(hand), hand, null);
            }
        }
    }
}