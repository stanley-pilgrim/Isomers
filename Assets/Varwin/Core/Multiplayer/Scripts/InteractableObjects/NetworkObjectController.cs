using Unity.Netcode;

namespace Varwin.Multiplayer
{
    /// <summary>
    /// Синхронизатор ObjectController.
    /// </summary>
    public class NetworkObjectController : NetworkBehaviour
    {
        /// <summary>
        /// Является ли объект активным.
        /// </summary>
        public NetworkVariable<bool> IsActive = new();

        /// <summary>
        /// Подписка на синхронизацию на клиенте.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                IsActive.OnValueChanged += IsActiveValueChanged;
            }
            else
            {
                IsActive.Value = gameObject.activeInHierarchy;
            }
        }

        /// <summary>
        /// При изменении активности объекта на сервере.
        /// </summary>
        /// <param name="previousValue">Предыдущее значение.</param>
        /// <param name="newValue">Новое значение.</param>
        private void IsActiveValueChanged(bool previousValue, bool newValue)
        {
            gameObject.SetActive(newValue);
        }

        /// <summary>
        /// При активации объекта.
        /// </summary>
        private void OnEnable()
        {
            if (IsServer)
            {
                IsActive.Value = true;
            }
        }

        /// <summary>
        /// При деактивации объекта.
        /// </summary>
        private void OnDisable()
        {
            if (IsServer)
            {
                IsActive.Value = false;
            }
        }

        /// <summary>
        /// Отписка.
        /// </summary>
        public override void OnDestroy()
        {
            if (!IsServer)
            {
                IsActive.OnValueChanged -= IsActiveValueChanged;
            }
        }
    }
}