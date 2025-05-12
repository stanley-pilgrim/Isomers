using System;
using Unity.Netcode;
using Varwin.PlatformAdapter;
using Varwin.Public;

namespace Varwin.Multiplayer
{
    /// <summary>
    /// Синхронизация поинтеров.
    /// </summary>
    public class NetworkObjectPointerBehaviour : NetworkBehaviour
    {
        /// <summary>
        /// Целевой объект-контроллер поинтера.
        /// </summary>
        private ObjectPointerBehaviour _objectPointerBehaviour;

        /// <summary>
        /// Инициализация компонента.
        /// </summary>
        /// <param name="objectPointerBehaviour">Объект-контроллер поинтера.</param>
        public void Initialize(ObjectPointerBehaviour objectPointerBehaviour)
        {
            _objectPointerBehaviour = objectPointerBehaviour;

            objectPointerBehaviour.PointerAction.OnPointerClick += OnPointerClick;
            objectPointerBehaviour.PointerAction.OnPointerDown += OnPointerDown;
            objectPointerBehaviour.PointerAction.OnPointerUp += OnPointerUp;
            objectPointerBehaviour.PointerAction.OnPointerIn += OnPointerIn;
            objectPointerBehaviour.PointerAction.OnPointerOut += OnPointerOut;
        }

        /// <summary>
        /// При клике на объект.
        /// </summary>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        private void OnPointerClick(ControllerInteraction.ControllerHand hand)
        {
            OnPointerClickServerRpc(NetworkManager.LocalClient.ClientId, hand);
        }

        /// <summary>
        /// При нажатии на объект.
        /// </summary>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        private void OnPointerDown(ControllerInteraction.ControllerHand hand)
        {
            OnPointerDownServerRpc(NetworkManager.LocalClient.ClientId, hand);
        }

        /// <summary>
        /// При отпускании нажатия на объект.
        /// </summary>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        private void OnPointerUp(ControllerInteraction.ControllerHand hand)
        {
            OnPointerUpServerRpc(NetworkManager.LocalClient.ClientId, hand);
        }

        /// <summary>
        /// При наведении на объект.
        /// </summary>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        private void OnPointerIn(ControllerInteraction.ControllerHand hand)
        {
            OnPointerInServerRpc(NetworkManager.LocalClient.ClientId, hand);
        }

        /// <summary>
        /// При потере фокуса на объекте.
        /// </summary>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        private void OnPointerOut(ControllerInteraction.ControllerHand hand)
        {
            OnPointerOutServerRpc(NetworkManager.LocalClient.ClientId, hand);
        }

        /// <summary>
        /// При клике на объект и передача на сервер с информацией о клиенте, который вызвал событие.
        /// </summary>
        /// <param name="sourceClientId">Идентификатор клиента.</param>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        [ServerRpc(RequireOwnership = false)]
        private void OnPointerClickServerRpc(ulong sourceClientId, ControllerInteraction.ControllerHand hand)
        {
            OnPointerClickClientRpc(sourceClientId, hand);
        }

        /// <summary>
        /// При нажатии на объект и передача на сервер с информацией о клиенте, который вызвал событие.
        /// </summary>
        /// <param name="sourceClientId">Идентификатор клиента.</param>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        [ServerRpc(RequireOwnership = false)]
        private void OnPointerDownServerRpc(ulong sourceClientId, ControllerInteraction.ControllerHand hand)
        {
            OnPointerDownClientRpc(sourceClientId, hand);
        }

        /// <summary>
        /// При отпускании нажатия на объект и передача на сервер с информацией о клиенте, который вызвал событие.
        /// </summary>
        /// <param name="sourceClientId">Идентификатор клиента.</param>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        [ServerRpc(RequireOwnership = false)]
        private void OnPointerUpServerRpc(ulong sourceClientId, ControllerInteraction.ControllerHand hand)
        {
            OnPointerUpClientRpc(sourceClientId, hand);
        }

        /// <summary>
        /// При наведении на объект и передача на сервер с информацией о клиенте, который вызвал событие.
        /// </summary>
        /// <param name="sourceClientId">Идентификатор клиента.</param>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        [ServerRpc(RequireOwnership = false)]
        private void OnPointerInServerRpc(ulong sourceClientId, ControllerInteraction.ControllerHand hand)
        {
            OnPointerInClientRpc(sourceClientId, hand);
        }

        /// <summary>
        /// При потере фокуса на объекте и передача на сервер с информацией о клиенте, который вызвал событие.
        /// </summary>
        /// <param name="sourceClientId">Идентификатор клиента.</param>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        [ServerRpc(RequireOwnership = false)]
        private void OnPointerOutServerRpc(ulong sourceClientId, ControllerInteraction.ControllerHand hand)
        {
            OnPointerOutClientRpc(sourceClientId, hand);
        }

        /// <summary>
        /// При клике на объект и передача на клиенты информации о клике и передаче информации идентификатора, который вызвал событие.
        /// </summary>
        /// <param name="sourceClientId">Идентификатор клиента.</param>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        [ClientRpc]
        private void OnPointerClickClientRpc(ulong sourceClientId, ControllerInteraction.ControllerHand hand)
        {
            if (NetworkManager.LocalClient.ClientId == sourceClientId)
            {
                return;
            }

            InvokeMethodInBehaviour<IPointerClickInteractionAware>(a => a.OnPointerClick(GetContext(hand)));
            InvokeMethodInBehaviour<IPointerClickAware>(a => a.OnPointerClick());
        }

        /// <summary>
        /// При нажатии на объект и передача на клиенты информации о клике и передаче информации идентификатора, который вызвал событие.
        /// </summary>
        /// <param name="sourceClientId">Идентификатор клиента.</param>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        [ClientRpc]
        private void OnPointerDownClientRpc(ulong sourceClientId, ControllerInteraction.ControllerHand hand)
        {
            if (NetworkManager.LocalClient.ClientId == sourceClientId)
            {
                return;
            }
            InvokeMethodInBehaviour<IPointerDownInteractionAware>(a => a.OnPointerDown(GetContext(hand)));
            InvokeMethodInBehaviour<IPointerDownAware>(a => a.OnPointerDown());
        }

        /// <summary>
        /// При отпускании нажатия на объект и передача на клиенты информации о клике и передаче информации идентификатора, который вызвал событие.
        /// </summary>
        /// <param name="sourceClientId">Идентификатор клиента.</param>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        [ClientRpc]
        private void OnPointerUpClientRpc(ulong sourceClientId, ControllerInteraction.ControllerHand hand)
        {
            if (NetworkManager.LocalClient.ClientId == sourceClientId)
            {
                return;
            }

            InvokeMethodInBehaviour<IPointerUpInteractionAware>(a => a.OnPointerUp(GetContext(hand)));
            InvokeMethodInBehaviour<IPointerUpAware>(a => a.OnPointerUp());
        }

        /// <summary>
        /// При наведении на объект и передача на клиенты информации о клике и передаче информации идентификатора, который вызвал событие.
        /// </summary>
        /// <param name="sourceClientId">Идентификатор клиента.</param>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        [ClientRpc]
        private void OnPointerInClientRpc(ulong sourceClientId, ControllerInteraction.ControllerHand hand)
        {
            if (NetworkManager.LocalClient.ClientId == sourceClientId)
            {
                return;
            }

            InvokeMethodInBehaviour<IPointerInInteractionAware>(a => a.OnPointerIn(GetContext(hand)));
            InvokeMethodInBehaviour<IPointerInAware>(a => a.OnPointerIn());
        }

        /// <summary>
        /// При потере фокуса на объекте и передача на клиенты информации о клике и передаче информации идентификатора, который вызвал событие.
        /// </summary>
        /// <param name="sourceClientId">Идентификатор клиента.</param>
        /// <param name="hand">Рука, производящая взаимодействие.</param>
        [ClientRpc]
        private void OnPointerOutClientRpc(ulong sourceClientId, ControllerInteraction.ControllerHand hand)
        {
            if (NetworkManager.LocalClient.ClientId == sourceClientId)
            {
                return;
            }

            InvokeMethodInBehaviour<IPointerOutInteractionAware>(a => a.OnPointerOut(GetContext(hand)));
            InvokeMethodInBehaviour<IPointerOutAware>(a => a.OnPointerOut());
        }

        /// <summary>
        /// Вызвать метод у компонента.
        /// </summary>
        /// <param name="method">Вызываемый метод.</param>
        /// <typeparam name="T">Тип компонента.</typeparam>
        private void InvokeMethodInBehaviour<T>(Action<T> method)
        {
            var components = _objectPointerBehaviour.GetComponents<T>();
            if (components == null || components.Length == 0)
            {
                return;
            }

            foreach (var component in components)
            {
                method(component);
            }
        }

        private PointerInteractionContext GetContext(ControllerInteraction.ControllerHand hand)
        {
            return new (InputAdapter.Instance?.PlayerController.Nodes.GetControllerReference(hand).GameObject, hand);
        }

        /// <summary>
        /// Отписка.
        /// </summary>
        public override void OnDestroy()
        {
            if (!_objectPointerBehaviour)
            {
                return;
            }
            
            _objectPointerBehaviour.PointerAction.OnPointerClick -= OnPointerClick;
            _objectPointerBehaviour.PointerAction.OnPointerDown -= OnPointerDown;
            _objectPointerBehaviour.PointerAction.OnPointerUp -= OnPointerUp;
            _objectPointerBehaviour.PointerAction.OnPointerIn -= OnPointerIn;
            _objectPointerBehaviour.PointerAction.OnPointerOut -= OnPointerOut;
        }
    }
}