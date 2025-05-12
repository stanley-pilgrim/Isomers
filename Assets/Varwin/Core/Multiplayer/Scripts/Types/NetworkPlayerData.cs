namespace Varwin.Multiplayer
{
    /// <summary>
    /// Модель данных сетевого игрока.
    /// </summary>
    public class NetworkPlayerData
    {
        /// <summary>
        /// Сетейвой Id.
        /// </summary>
        public ulong NetworkId;

        /// <summary>
        /// Сетевое имя.
        /// </summary>
        public string Nickname;

        /// <summary>
        /// Уникальный Guid сессии игрока.
        /// Используется для идентификации игрока при переподключении.
        /// </summary>
        public string LocalId;

        /// <summary>
        /// Используется мобильное устройство.
        /// </summary>
        public bool IsMobileVr;
    }
}