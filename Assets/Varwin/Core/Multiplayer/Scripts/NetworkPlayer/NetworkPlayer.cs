using System.Collections;
using UnityEngine;
using Varwin.Public;

namespace Varwin.Multiplayer
{
    /// <summary>
    /// Игровой персонаж и его управление.
    /// </summary>
    public class NetworkPlayer : MonoBehaviour, IWrapperAware
    {
        /// <summary>
        /// Делегат события столкновения руки с объектом.
        /// </summary>
        /// <param name="sender">Вызвавший событие объект.</param>
        /// <param name="hand">Рука.</param>
        /// <param name="targetWrapper">C чем произошло событие.</param>
        public delegate void HandCollisionEventHandler(Wrapper sender, Hand hand, Wrapper targetWrapper);

        /// <summary>
        /// Событие, вызываемое при столкновении руки с объектом.
        /// </summary>
        public event HandCollisionEventHandler HandTriggerEnter;

        /// <summary>
        /// Событие, вызываемое при выходе из столкновения руки с объектом.
        /// </summary>
        public event HandCollisionEventHandler HandTriggerExit;
        
        /// <summary>
        /// Враппер персонажа.
        /// </summary>
        private NetworkPlayerWrapper _wrapper;
        
        /// <summary>
        /// Управление игроком через NetworkBehaviour.
        /// </summary>
        private NetworkPlayerController _networkPlayerController;

        /// <summary>
        /// Позииця головы.
        /// </summary>
        public Vector3 HeadPosition => _networkPlayerController.Avatar.Head.transform.position;
        
        /// <summary>
        /// Поворот головы.
        /// </summary>
        public Quaternion HeadRotation => _networkPlayerController.Avatar.Head.transform.rotation;

        /// <summary>
        /// Позиция игрока.
        /// </summary>
        public Vector3 Position => _networkPlayerController.Avatar.transform.position;

        /// <summary>
        /// Поворот игрока.
        /// </summary>
        public Quaternion Rotation => _networkPlayerController.Avatar.transform.rotation;
        
        /// <summary>
        /// Никнейм игрока.
        /// </summary>
        public string Nickname;

        /// <summary>
        /// Коллайдер левой руки.
        /// </summary>
        public HandCollider LeftHand;
        
        /// <summary>
        /// Коллайдер правой руки.
        /// </summary>
        public HandCollider RightHand;

        /// <summary>
        /// Инициализация контроллера.
        /// </summary>
        private void Awake()
        {
            _networkPlayerController = GetComponent<NetworkPlayerController>();
            LeftHand.TriggerEnter += OnHandTriggerEnter;
            LeftHand.TriggerExit += OnHandTriggerExit;
            RightHand.TriggerEnter += OnHandTriggerEnter;
            RightHand.TriggerExit += OnHandTriggerExit;
        }

        /// <summary>
        /// При выходе из коллизии руки.
        /// </summary>
        /// <param name="sender">Объект, который вызвал событие.</param>
        /// <param name="wrapper">Объект колизии.</param>
        private void OnHandTriggerExit(GameObject sender, Wrapper wrapper)
        {
            var hand = Hand.Left;

            if (sender == RightHand.gameObject)
            {
                hand = Hand.Right;
            }

            HandTriggerExit?.Invoke(Wrapper(), hand, wrapper);
        }

        /// <summary>
        /// При входе В коллизию руки.
        /// </summary>
        /// <param name="sender">Объект, который вызвал событие.</param>
        /// <param name="wrapper">Объект колизии.</param>
        private void OnHandTriggerEnter(GameObject sender, Wrapper wrapper)
        {
            var hand = Hand.Left;
            if (sender == LeftHand.gameObject)
            {
                hand = Hand.Left;
            }
            else if (sender == RightHand.gameObject)
            {
                hand = Hand.Right;
            }
            
            HandTriggerEnter?.Invoke(Wrapper(), hand, wrapper);
        }

        /// <summary>
        /// Метод получения враппера игрока.
        /// </summary>
        /// <returns>Враппер игрока.</returns>
        public Wrapper Wrapper()
        {
            if (_wrapper == null)
            {
                _wrapper = new NetworkPlayerWrapper(gameObject);
            }

            return _wrapper;
        }

        /// <summary>
        /// Включить гравитацию.
        /// </summary>
        /// <param name="isGravity">Истина, если гравитацию необходимо включить.</param>
        public void SetGravity(bool isGravity)
        {
            _networkPlayerController.SetGravityClientRpc(isGravity);
        }

        /// <summary>
        /// Включить телепортацию.
        /// </summary>
        /// <param name="teleportAllowed">Истина, если телепортацию необходимо включить.</param>
        public void SetUseTeleport(bool teleportAllowed)
        {
            _networkPlayerController.SetUseTeleportClientRpc(teleportAllowed);
        }

        /// <summary>
        /// Скрыть луч телепорта при отсутствии возможности телепортироваться.
        /// </summary>
        /// <param name="hide">Истина, если скрывать.</param>
        public void SetHideRayOnTurnedOffTeleport(bool hide)
        {
            _networkPlayerController.SetHideRayOnTurnedOffTeleportClientRpc(hide);
        }

        /// <summary>
        /// Включить перемещение WASD.
        /// </summary>
        /// <param name="useWasd">Истина, если включить.</param>
        public void SetMovingWasd(bool useWasd)
        {
            _networkPlayerController.SetMovingWasdClientRpc(useWasd);
        }
        
        /// <summary>
        /// Включить поворот мышью.
        /// </summary>
        /// <param name="isMouseLookEnabled">Истина, если включить.</param>
        public void SetMouseLook(bool isMouseLookEnabled)
        {
            _networkPlayerController.SetMouseLookClientRpc(isMouseLookEnabled);
        }
        
        /// <summary>
        /// Включить отображение курсора.
        /// </summary>
        /// <param name="isCursorVisible">Истина, если включить.</param>
        public void SetCursorVisibility(bool isCursorVisible)
        {
            _networkPlayerController.SetCursorVisibilityClientRpc(isCursorVisible);
        }

        /// <summary>
        /// Включить взаимодействие с объектами.
        /// </summary>
        /// <param name="interaction">Истина, если включить.</param>
        public void SetObjectInteraction(bool interaction)
        {
            _networkPlayerController.SetObjectInteractionClientRpc(interaction);
        }

        /// <summary>
        /// Задать длину луча.
        /// </summary>
        /// <param name="length">Длина луча.</param>
        public void SetRayLength(float length)
        {
            _networkPlayerController.SetRayLengthClientRpc(length);
        }

        /// <summary>
        /// Задать ширину луча.
        /// </summary>
        /// <param name="width">Ширина луча.</param>
        /// <param name="part">Часть луча, где необходимо выполнить настройку.</param>
        public void SetRayWidth(RayPart part, float width)
        {
            _networkPlayerController.SetRayWidthClientRpc(part, width);
        }

        /// <summary>
        /// Задать цвет луча.
        /// </summary>
        /// <param name="color">Цвет луча.</param>
        /// <param name="part">Часть луча, где необходимо выполнить настройку.</param>
        public void SetRayColor(RayPart part, Color color)
        {
            _networkPlayerController.SetRayColorClientRpc(part, color);
        }

        /// <summary>
        /// Задать радиус коллайдера руки.
        /// </summary>
        /// <param name="radius">Радиус коллайдера руки.</param>
        public void SetHandColliderRadius(float radius)
        {
            _networkPlayerController.SetHandColliderRadiusClientRpc(radius);
        }

        /// <summary>
        /// Вернуть на стартовую позиицию.
        /// </summary>
        public void ReturnToStartPoint()
        {
            _networkPlayerController.ReturnToStartPointClientRpc();
        }

        /// <summary>
        /// Телепортироваться к объекту.
        /// </summary>
        /// <param name="targetObj">Целевой объект.</param>
        public void TeleportToObject(Wrapper targetObj)
        {
            var targetGameObject = targetObj.GetGameObject();
            if (!targetGameObject)
            {
                return;
            }
            
            var gameObjectSerializable = new GameObjectSerializable();
            gameObjectSerializable.SetGameObject(targetGameObject);
            _networkPlayerController.TeleportToObjectClientRpc(gameObjectSerializable);
        }

        /// <summary>
        /// Телепортироваться к точке.
        /// </summary>
        /// <param name="position">Целевая точка.</param>
        public void TeleportTo(Vector3 position)
        {
            _networkPlayerController.TeleportToClientRpc(position);
        }

        /// <summary>
        /// Перемещаться в позицию с заданной скоростью.
        /// </summary>
        /// <param name="position">Позиция.</param>
        /// <param name="speed">Скорость.</param>
        public IEnumerator MoveTo(Vector3 position, float speed)
        {
            yield return MoveToPointWithSpeed(position, speed);
        }

        /// <summary>
        /// Перемещаться к объекту с заданной скоростью.
        /// </summary>
        /// <param name="targetWrapper">Целевой объект.</param>
        /// <param name="speed">Скорость.</param>
        public IEnumerator MoveTo(Wrapper targetWrapper, float speed)
        {
            var targetGameObject = targetWrapper.GetGameObject();
            if (!targetGameObject)
            {
                yield break; 
            }
            
            yield return MoveToPointWithSpeed(targetGameObject.transform.position, speed);
        }
        
        /// <summary>
        /// Перемещаться к позиции с заданной скоростью.
        /// </summary>
        /// <param name="target">Позиция.</param>
        /// <param name="speed">Скорость.</param>
        private IEnumerator MoveToPointWithSpeed(Vector3 target, float speed)
        {
            var startPos = Position;
            var step = (speed / (startPos - target).magnitude) * Time.fixedDeltaTime;
            var t = 0f;
            while (t <= 1.0f)
            {
                t += step;
                _networkPlayerController.SetPositionClientRpc(Vector3.Lerp(startPos, target, t));
                yield return new WaitForFixedUpdate();
            }
            
            _networkPlayerController.SetPositionClientRpc(target);
        }

        /// <summary>
        /// Повернуть на заданный угол.
        /// </summary>
        /// <param name="angle">Угол.</param>
        public void Rotate(float angle)
        {
            _networkPlayerController.RotateClientRpc(angle);
        }

        /// <summary>
        /// Повернуть с параметрами.
        /// </summary>
        /// <param name="rotationType">Тип поворота.</param>
        /// <param name="targetObject">Целевой объект.</param>
        public void RotateTo(RotationType rotationType, Wrapper targetObject)
        {
            var targetGameObject = targetObject.GetGameObject();
            if (!targetGameObject)
            {
                return;
            }
            
            var gameObjectSerializable = new GameObjectSerializable();
            gameObjectSerializable.SetGameObject(targetGameObject);

            _networkPlayerController.RotateToClientRpc(rotationType, gameObjectSerializable);
        }

        /// <summary>
        /// Задать поворот.
        /// </summary>
        /// <param name="rotation">Целевой поворот.</param>
        public void SetRotation(Vector3 rotation)
        {
            _networkPlayerController.SetRotationClientRpc(rotation);
        }

        /// <summary>
        /// Принудительно взять в руку объект.
        /// </summary>
        /// <param name="hand">Рука.</param>
        /// <param name="targetObject">Целевой объект.</param>
        public void ForceGrab(Hand hand, Wrapper targetObject)
        {
            var targetGameObject = targetObject.GetGameObject();
            if (!targetGameObject)
            {
                return;
            }

            var gameObjectSerializable = new GameObjectSerializable();
            gameObjectSerializable.SetGameObject(targetGameObject);
            _networkPlayerController.ForceGrabClientRpc(hand, gameObjectSerializable);
        }

        /// <summary>
        /// Выбросить объект из руки.
        /// </summary>
        /// <param name="hand">Рука.</param>
        public void ForceDrop(Hand hand)
        {
            _networkPlayerController.ForceDropClientRpc(hand);
        }

        /// <summary>
        /// Передать вибрацию на контроллер.
        /// </summary>
        /// <param name="hand">Рука.</param>
        /// <param name="strength">Сила.</param>
        /// <param name="duration">Длительность.</param>
        /// <param name="interval">Интервал.</param>
        public void Vibrate(Hand hand, float strength, float duration, float interval)
        {
            _networkPlayerController.VibrateClientRpc(hand, strength, duration, interval);
        }

        /// <summary>
        /// Передать вибрацию на контроллер.
        /// </summary>
        /// <param name="hand">Рука.</param>
        /// <param name="strength">Сила.</param>
        public void Vibrate(Hand hand, Strength strength)
        {
            _networkPlayerController.VibrateClientRpc(hand, strength);
        }

        /// <summary>
        /// Определить является ли объект взятым в руку.
        /// </summary>
        /// <param name="hand">Рука.</param>
        /// <param name="targetGameObject">Целевой объект.</param>
        /// <returns>Истина, если объект взят в руку.</returns>
        public bool CheckObjectInHand(Hand hand, GameObject targetGameObject)
        {
            var networkInteractableObject = targetGameObject.GetComponent<NetworkInteractableObject>();
            if (!networkInteractableObject)
            {
                return false;
            }

            var networkInteractionState = networkInteractableObject.IsGrabbed.Value;

            if (networkInteractionState.ClientId != _networkPlayerController.NetworkManager.LocalClient.ClientId)
            {
                return false;
            }

            if (!networkInteractionState.State)
            {
                return false;
            }

            return (networkInteractionState.IsLeftHand && hand == Hand.Left) || (!networkInteractionState.IsLeftHand && hand == Hand.Right);
        }

        /// <summary>
        /// Отписка.
        /// </summary>
        private void OnDestroy()
        {
            LeftHand.TriggerEnter -= OnHandTriggerEnter;
            LeftHand.TriggerExit -= OnHandTriggerExit;
            RightHand.TriggerEnter -= OnHandTriggerEnter;
            RightHand.TriggerExit -= OnHandTriggerExit;
        }
    }
}