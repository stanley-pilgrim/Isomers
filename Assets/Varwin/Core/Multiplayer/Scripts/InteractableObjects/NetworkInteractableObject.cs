using System;
using Unity.Netcode;
using UnityEngine;
using Varwin.PlatformAdapter;
using Varwin.Public;

namespace Varwin.Multiplayer
{
    /// <summary>
    /// Синхронизатор интерактивного объекта.
    /// </summary>
    [DefaultExecutionOrder(100000)]
    public class NetworkInteractableObject : NetworkBehaviour
    {
        /// <summary>
        /// Дельта срабатывания события сравнивания (угол).
        /// </summary>
        public const float AngularThreshold = 0.1f;
        
        /// <summary>
        /// Дельта срабатывания события сравнивания (позиция).
        /// </summary>
        public const float VectorThreshold = 0.001f;

        /// <summary>
        /// Является ли объектом, который взяли на этом клиенте.
        /// </summary>
        private bool _isLocalGrabbed = false;

        /// <summary>
        /// Твердое тело.
        /// </summary>
        private Rigidbody _rigidbody;

        /// <summary>
        /// Переменная-контейнер локальной позиции.
        /// </summary>
        public NetworkVariable<Vector3> LocalPosition = new();

        /// <summary>
        /// Переменная-контейнер локального поворота.
        /// </summary>
        public NetworkVariable<Quaternion> LocalRotation = new();

        /// <summary>
        /// Переменная-контейнер локального масштаба.
        /// </summary>
        public NetworkVariable<Vector3> LocalScale = new();

        /// <summary>
        /// Является ли объект взятым в руку.
        /// </summary>
        public NetworkVariable<NetworkInteractionState> IsGrabbed = new();

        /// <summary>
        /// Является ли объект используемым.
        /// </summary>
        public NetworkVariable<NetworkInteractionState> IsUsing = new();

        /// <summary>
        /// Является ли объект касаемым.
        /// </summary>
        public NetworkVariable<NetworkInteractionState> IsTouching = new();

        /// <summary>
        /// Можно ли взять в руку.
        /// </summary>
        public NetworkVariable<bool> IsGrabbable = new();

        /// <summary>
        /// Можно ли использовать.
        /// </summary>
        public NetworkVariable<bool> IsUsable = new();

        /// <summary>
        /// Можно ли касаться.
        /// </summary>
        public NetworkVariable<bool> IsTouchable = new();

        /// <summary>
        /// Контроллер ввода для этого объекта.
        /// </summary>
        private InputController _inputController;

        /// <summary>
        /// Контроллер взаимодействий с объектом.
        /// </summary>
        private ObjectInteraction.InteractObject _interactObject;

        /// <summary>
        /// Проинициализирован ли.
        /// </summary>
        private bool _isInitialized;

        /// <summary>
        /// Цепочка объектов.
        /// </summary>
        private INetworkObjectsChain _networkObjectChain;

        /// <summary>
        /// Взят ли объект на клиенте.
        /// </summary>
        public bool IsLocalGrabbed => _isLocalGrabbed;

        /// <summary>
        /// При инициализации контроллера ввода.
        /// </summary>
        private void OnInputControllerInitialized()
        {
            SubscribeToInput();
        }

        /// <summary>
        /// Инициализация в сети.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            _networkObjectChain = GetComponent<INetworkObjectsChain>();
            _rigidbody = GetComponent<Rigidbody>();
            _inputController = gameObject.GetInputControllers().Find(a => a.IsConnectedToGameObject(gameObject));
            if (_inputController != null)
            {
                _inputController.Initialized += OnInputControllerInitialized;
            }
            
            SubscribeToInput();

            if (IsServer)
            {
                LocalPosition.Value = transform.localPosition;
                LocalRotation.Value = transform.localRotation;
                LocalScale.Value = transform.localScale;

                IsGrabbable.Value = _interactObject?.isGrabbable ?? false;
                IsUsable.Value = _interactObject?.isUsable ?? false;
                IsTouchable.Value = _interactObject?.isTouchable ?? false;
            }

            IsGrabbed.OnValueChanged += OnIsGrabbedValueChanged;
            IsUsing.OnValueChanged += OnIsUsedValueChanged;
            IsTouching.OnValueChanged += OnIsTouchedValueChanged;

            if (!IsServer)
            {
                IsTouchable.OnValueChanged += OnIsTouchableValueChanged;
                IsUsable.OnValueChanged += OnIsUsableValueChanged;
                IsGrabbable.OnValueChanged += OnIsGrabbableValueChanged;
            }

            ProjectData.PlatformModeChanging += OnPlatformModeChanging;
            _isInitialized = true;
        }

        /// <summary>
        /// Подписка на объект.
        /// </summary>
        private void SubscribeToInput()
        {
            _interactObject = InputAdapter.Instance.ObjectInteraction.Object.GetFrom(gameObject);

            if (_interactObject != null)
            {
                _interactObject.InteractableObjectGrabbed += OnGrabStarted;
                _interactObject.InteractableObjectUngrabbed += OnGrabEnded;
                _interactObject.InteractableObjectUsed += OnUseStarted;
                _interactObject.InteractableObjectUnused += OnUseEnded;
                _interactObject.InteractableObjectTouched += OnTouchStarted;
                _interactObject.InteractableObjectUntouched += OnTouchEnded;
            }
        }
        
        /// <summary>
        /// Отписка от объекта.
        /// </summary>
        private void UnsubscribeToInput()
        {
            if (_interactObject != null)
            {
                _interactObject.InteractableObjectGrabbed -= OnGrabStarted;
                _interactObject.InteractableObjectUngrabbed -= OnGrabEnded;
                _interactObject.InteractableObjectUsed -= OnUseStarted;
                _interactObject.InteractableObjectUnused -= OnUseEnded;
                _interactObject.InteractableObjectTouched -= OnTouchStarted;
                _interactObject.InteractableObjectUntouched -= OnTouchEnded;
            }
        }

        /// <summary>
        /// При изменении режима просмотра.
        /// </summary>
        /// <param name="newPlatformMode">Новый режим просмотра.</param>
        private void OnPlatformModeChanging(PlatformMode newPlatformMode)
        {
            UnsubscribeToInput();
        }

        /// <summary>
        /// При изменении значения переменной "можно ли коснуться".
        /// </summary>
        /// <param name="previousValue">Старое значение.</param>
        /// <param name="newValue">Новое значение.</param>
        private void OnIsTouchableValueChanged(bool previousValue, bool newValue)
        {
            if (IsServer)
            {
                return;
            }

            if (!IsTouching.Value.State)
            {
                _interactObject.isTouchable = newValue;
            }
        }

        /// <summary>
        /// При изменении значения переменной "можно ли использовать".
        /// </summary>
        /// <param name="previousValue">Старое значение.</param>
        /// <param name="newValue">Новое значение.</param>
        private void OnIsUsableValueChanged(bool previousValue, bool newValue)
        {
            if (!IsUsing.Value.State)
            {
                _interactObject.isUsable = newValue;
            }
        }

        /// <summary>
        /// При изменении значения переменной "можно ли взять в руку".
        /// </summary>
        /// <param name="previousValue">Старое значение.</param>
        /// <param name="newValue">Новое значение.</param>
        private void OnIsGrabbableValueChanged(bool previousValue, bool newValue)
        {
            if (IsServer)
            {
                return;
            }

            if (!IsGrabbed.Value.State)
            {
                _interactObject.isGrabbable = newValue;
            }
        }

        /// <summary>
        /// При касании объекта.
        /// </summary>
        /// <param name="sender">Объект, который вызвал событие.</param>
        /// <param name="e">Аргументы события.</param>
        private void OnTouchStarted(object sender, ObjectInteraction.InteractableObjectEventArgs e)
        {
            SetTouchingStateServerRpc(true, NetworkObject.NetworkManager.LocalClient.ClientId, e.Hand == ControllerInteraction.ControllerHand.Left);
        }

        /// <summary>
        /// При прекращении касания объекта.
        /// </summary>
        /// <param name="sender">Объект, который вызвал событие.</param>
        /// <param name="e">Аргументы события.</param>
        private void OnTouchEnded(object sender, ObjectInteraction.InteractableObjectEventArgs e)
        {
            SetTouchingStateServerRpc(false, NetworkObject.NetworkManager.LocalClient.ClientId, e.Hand == ControllerInteraction.ControllerHand.Left);
        }

        /// <summary>
        /// При использовании объекта.
        /// </summary>
        /// <param name="sender">Объект, который вызвал событие.</param>
        /// <param name="e">Аргументы события.</param>
        private void OnUseStarted(object sender, ObjectInteraction.InteractableObjectEventArgs e)
        {
            SetUsingStateServerRpc(true, NetworkObject.NetworkManager.LocalClient.ClientId, e.Hand == ControllerInteraction.ControllerHand.Left);
        }

        /// <summary>
        /// При окончании использования объекта.
        /// </summary>
        /// <param name="sender">Объект, который вызвал событие.</param>
        /// <param name="e">Аргументы события.</param>
        private void OnUseEnded(object sender, ObjectInteraction.InteractableObjectEventArgs e)
        {
            SetUsingStateServerRpc(false, NetworkObject.NetworkManager.LocalClient.ClientId, e.Hand == ControllerInteraction.ControllerHand.Left);
        }

        /// <summary>
        /// При взятии объекта в руку.
        /// </summary>
        /// <param name="sender">Объект, который вызвал событие.</param>
        /// <param name="e">Аргументы события.</param>
        private void OnGrabStarted(object sender, ObjectInteraction.InteractableObjectEventArgs e)
        {
            _isLocalGrabbed = true;
            SetGrabbedStateServerRpc(true, NetworkObject.NetworkManager.LocalClient.ClientId, e.Hand == ControllerInteraction.ControllerHand.Left, Vector3.zero, Vector3.zero);
        }

        /// <summary>
        /// При выпускании объекта из руки.
        /// </summary>
        /// <param name="sender">Объект, который вызвал событие.</param>
        /// <param name="e">Аргументы события.</param>
        private void OnGrabEnded(object sender, ObjectInteraction.InteractableObjectEventArgs e)
        {
            _isLocalGrabbed = false;
            
            SetGrabbedStateServerRpc(false, NetworkObject.NetworkManager.LocalClient.ClientId, e.Hand == ControllerInteraction.ControllerHand.Left, _rigidbody.velocity, _rigidbody.angularVelocity);
        }

        /// <summary>
        /// При изменении значении переменной "касается".
        /// </summary>
        /// <param name="oldInteractionState">Старое значение.</param>
        /// <param name="interactionState">Новое значение.</param>
        private void OnIsTouchedValueChanged(NetworkInteractionState oldInteractionState, NetworkInteractionState interactionState)
        {
            if (interactionState.ClientId == NetworkObject.NetworkManager.LocalClient.ClientId)
            {
                return;
            }

            var hand = interactionState.IsLeftHand ? ControllerInteraction.ControllerHand.Left : ControllerInteraction.ControllerHand.Right;

            if (interactionState.State)
            {
                _interactObject.isTouchable = false;
                _inputController.TouchStart(hand);
            }
            else
            {
                _interactObject.isTouchable = IsTouchable.Value;
                _inputController.TouchEnd(hand);
            }
        }

        /// <summary>
        /// При изменении значении переменной "используется".
        /// </summary>
        /// <param name="oldInteractionState">Старое значение.</param>
        /// <param name="interactionState">Новое значение.</param>
        private void OnIsUsedValueChanged(NetworkInteractionState oldInteractionState, NetworkInteractionState interactionState)
        {
            if (interactionState.ClientId == NetworkObject.NetworkManager.LocalClient.ClientId)
            {
                return;
            }

            var hand = interactionState.IsLeftHand ? ControllerInteraction.ControllerHand.Left : ControllerInteraction.ControllerHand.Right;
            if (interactionState.State)
            {
                _interactObject.isUsable = false;
                _inputController.UseStart(hand);
            }
            else
            {
                _interactObject.isUsable = IsUsable.Value;
                _inputController.UseEnd(hand);
            }
        }

        /// <summary>
        /// При изменении значении переменной "взят в руку".
        /// </summary>
        /// <param name="oldInteractionState">Старое значение.</param>
        /// <param name="interactionState">Новое значение.</param>
        private void OnIsGrabbedValueChanged(NetworkInteractionState oldInteractionState, NetworkInteractionState interactionState)
        {
            if (interactionState.ClientId == NetworkObject.NetworkManager.LocalClient.ClientId)
            {
                return;
            }

            var hand = interactionState.IsLeftHand ? ControllerInteraction.ControllerHand.Left : ControllerInteraction.ControllerHand.Right;
            if (interactionState.State)
            {
                _interactObject.isGrabbable = false;
                _inputController.GrabStart(hand);
            }
            else
            {
                _interactObject.isGrabbable = IsGrabbable.Value;
                _inputController.GrabEnd(hand);
            }
        }

        /// <summary>
        /// Обновление параметров объектов.
        /// </summary>
        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            UpdateValues();
        }

        /// <summary>
        /// Обновление параметров объектов в физическом пространстве.
        /// </summary>
        private void FixedUpdate()
        {
            if (!_isInitialized)
            {
                return;
            }

            UpdateTransformInfo();
            UpdateInteractable();
            UpdateValues();
        }

        /// <summary>
        /// Обновление параметров объектов.
        /// </summary>
        private void LateUpdate()
        {
            if (!_isInitialized)
            {
                return;
            }

            UpdateValues();
        }

        /// <summary>
        /// Обновление параметров интерактивности.
        /// </summary>
        private void UpdateInteractable()
        {
            if (!IsServer || _interactObject == null)
            {
                return;
            }

            if (_interactObject.isTouchable != IsTouchable.Value && !IsTouching.Value.State)
            {
                IsTouchable.Value = _interactObject.isTouchable;
            }

            if (_interactObject.isUsable != IsUsable.Value && !IsUsing.Value.State)
            {
                IsUsable.Value = _interactObject.isUsable;
            }

            if (_interactObject.isGrabbable != IsGrabbable.Value && !IsGrabbed.Value.State)
            {
                IsGrabbable.Value = _interactObject.isGrabbable;
            }
        }

        /// <summary>
        /// Обновление трансформа на клиенте.
        /// </summary>
        private void UpdateValues()
        {
            var isLocalGrabbed = (_networkObjectChain?.IsLocalGrabbed() ?? _isLocalGrabbed);
            var isGrabbed = (_networkObjectChain?.IsGrabbed() ?? IsGrabbed.Value.State);
            if (isLocalGrabbed || (IsServer && !isGrabbed))
            {
                return;
            }

            transform.localPosition = LocalPosition.Value;
            transform.localRotation = LocalRotation.Value;
            transform.localScale = LocalScale.Value;
        }

        /// <summary>
        /// Обновление трансформа на сервере.
        /// </summary>
        private void UpdateTransformInfo()
        {
            var isLocalGrabbed = (_networkObjectChain?.IsLocalGrabbed() ?? _isLocalGrabbed);
            var isGrabbed = (_networkObjectChain?.IsGrabbed() ?? IsGrabbed.Value.State);

            if (IsClient && isGrabbed && isLocalGrabbed)
            {
                SetValue(LocalPosition.Value, transform.localPosition, SetLocalPositionServerRpc);
                SetValue(LocalRotation.Value, transform.localRotation, SetLocalRotationServerRpc);
                SetValue(LocalScale.Value, transform.localScale, SetLocalScaleServerRpc);
            }
            else if (IsServer && !isGrabbed && !isLocalGrabbed)
            {
                SetValue(LocalPosition, transform.localPosition);
                SetValue(LocalRotation, transform.localRotation);
                SetValue(LocalScale, transform.localScale);
            }
        }

        /// <summary>
        /// Задать значение переменной.
        /// </summary>
        /// <param name="variable">Переменная.</param>
        /// <param name="value">Значение.</param>
        private void SetValue(NetworkVariable<Vector3> variable, Vector3 value)
        {
            if (NeedChangeValue(variable.Value, value))
            {
                variable.Value = value;
            }
        }

        /// <summary>
        /// Задать значение переменной.
        /// </summary>
        /// <param name="variable">Переменная.</param>
        /// <param name="value">Значение.</param>
        private void SetValue(NetworkVariable<Quaternion> variable, Quaternion value)
        {
            if (NeedChangeValue(variable.Value, value))
            {
                variable.Value = value;
            }
        }

        /// <summary>
        /// Задать значение через проверку.
        /// </summary>
        /// <param name="sourceValue">Исходное значение.</param>
        /// <param name="newValue">Новое значение.</param>
        /// <param name="setter">Метод, позволяющий установить значение.</param>
        private void SetValue(Vector3 sourceValue, Vector3 newValue, Action<Vector3> setter)
        {
            if (NeedChangeValue(sourceValue, newValue))
            {
                setter?.Invoke(newValue);
            }
        }

        /// <summary>
        /// Задать значение через проверку.
        /// </summary>
        /// <param name="sourceValue">Исходное значение.</param>
        /// <param name="newValue">Новое значение.</param>
        /// <param name="setter">Метод, позволяющий установить значение.</param>
        private void SetValue(Quaternion sourceValue, Quaternion newValue, Action<Quaternion> setter)
        {
            if (NeedChangeValue(sourceValue, newValue))
            {
                setter?.Invoke(newValue);
            }
        }

        /// <summary>
        /// Нужно ли изменить значение.
        /// </summary>
        /// <param name="sourceValue">Исходное значение.</param>
        /// <param name="newValue">Новое значение.</param>
        /// <returns>Истина, если нужно изменить значение.</returns>
        private bool NeedChangeValue(Quaternion sourceValue, Quaternion newValue)
        {
            var firstAngle = sourceValue.eulerAngles;
            var secondAngle = newValue.eulerAngles;
            var xAngleDelta = Mathf.Abs(Mathf.DeltaAngle(firstAngle.x, secondAngle.x));
            var yAngleDelta = Mathf.Abs(Mathf.DeltaAngle(firstAngle.y, secondAngle.y));
            var zAngleDelta = Mathf.Abs(Mathf.DeltaAngle(firstAngle.z, secondAngle.z));

            return xAngleDelta > AngularThreshold || yAngleDelta > AngularThreshold || zAngleDelta > AngularThreshold;
        }

        /// <summary>
        /// Нужно ли изменить значение.
        /// </summary>
        /// <param name="sourceValue">Исходное значение.</param>
        /// <param name="newValue">Новое значение.</param>
        /// <returns>Истина, если нужно изменить значение.</returns>
        private bool NeedChangeValue(Vector3 sourceValue, Vector3 newValue)
        {
            return (sourceValue - newValue).magnitude > VectorThreshold;
        }

        /// <summary>
        /// Обновление значений переменных на сервере "коснулись".
        /// </summary>
        /// <param name="isTouching">Коснулись ли сейчас.</param>
        /// <param name="clientId">Клиент, который вызвал метод.</param>
        /// <param name="isLeftHand">Левой ли рукой вызван метод.</param>
        [ServerRpc(RequireOwnership = false)]
        private void SetTouchingStateServerRpc(bool isTouching, ulong clientId, bool isLeftHand)
        {
            IsTouching.Value = new NetworkInteractionState(isTouching, clientId, isLeftHand);
        }

        /// <summary>
        /// Обновление значений переменных на сервере "используется".
        /// </summary>
        /// <param name="isUsing">Используется ли сейчас.</param>
        /// <param name="clientId">Клиент, который вызвал метод.</param>
        /// <param name="isLeftHand">Левой ли рукой вызван метод.</param>
        [ServerRpc(RequireOwnership = false)]
        private void SetUsingStateServerRpc(bool isUsing, ulong clientId, bool isLeftHand)
        {
            IsUsing.Value = new NetworkInteractionState(isUsing, clientId, isLeftHand);
        }

        /// <summary>
        /// Обновление значений переменных на сервере "взят в руку".
        /// </summary>
        /// <param name="isGrabbed">Взят ли в руку сейчас.</param>
        /// <param name="clientId">Клиент, который вызвал метод.</param>
        /// <param name="isLeftHand">Левой ли рукой вызван метод.</param>
        /// <param name="velocity">Скорость.</param>
        /// <param name="angularVelocity">Угловое ускорение.</param>
        [ServerRpc(RequireOwnership = false)]
        private void SetGrabbedStateServerRpc(bool isGrabbed, ulong clientId, bool isLeftHand, Vector3 velocity, Vector3 angularVelocity)
        {
            IsGrabbed.Value = new NetworkInteractionState(isGrabbed, clientId, isLeftHand);
            if (isGrabbed)
            {
                NetworkObject.ChangeOwnership(clientId);
            }
            else
            {
                NetworkObject.ChangeOwnership(NetworkManager.LocalClient.ClientId);
            }

            if (_rigidbody)
            {
                _rigidbody.velocity = velocity;
                _rigidbody.angularVelocity = angularVelocity;
            }
        }

        /// <summary>
        /// Задать значение локальной позиции на сервере.
        /// </summary>
        /// <param name="localPosition">Новое значение.</param>
        [ServerRpc(RequireOwnership = false)]
        private void SetLocalPositionServerRpc(Vector3 localPosition)
        {
            SetValue(LocalPosition, localPosition);
        }

        /// <summary>
        /// Задать значение локального поворота на сервере.
        /// </summary>
        /// <param name="localRotation">Новое значение.</param>
        [ServerRpc(RequireOwnership = false)]
        private void SetLocalRotationServerRpc(Quaternion localRotation)
        {
            SetValue(LocalRotation, localRotation);
        }

        /// <summary>
        /// Задать значение локального масштаба на сервере.
        /// </summary>
        /// <param name="localScale">Новое значение.</param>
        [ServerRpc(RequireOwnership = false)]
        private void SetLocalScaleServerRpc(Vector3 localScale)
        {
            SetValue(LocalScale, localScale);
        }

        /// <summary>
        /// Отписка.
        /// </summary>
        public override void OnDestroy()
        {
            UnsubscribeToInput();
            _inputController.Initialized -= OnInputControllerInitialized;
            IsGrabbed.OnValueChanged -= OnIsGrabbedValueChanged;
            IsUsing.OnValueChanged -= OnIsUsedValueChanged;
            IsTouching.OnValueChanged -= OnIsTouchedValueChanged;

            if (!IsServer)
            {
                IsTouchable.OnValueChanged -= OnIsTouchableValueChanged;
                IsUsable.OnValueChanged -= OnIsUsableValueChanged;
                IsGrabbable.OnValueChanged -= OnIsGrabbableValueChanged;
            }
        }
    }
}