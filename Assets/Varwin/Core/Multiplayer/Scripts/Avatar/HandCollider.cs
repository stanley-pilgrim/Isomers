using UnityEngine;
using Varwin.Public;

namespace Varwin.Multiplayer
{
    /// <summary>
    /// Контроллер коллайдера руки.
    /// </summary>
    public class HandCollider : MonoBehaviour
    {
        /// <summary>
        /// Делегат событий столкновений. 
        /// </summary>
        /// <param name="sender">Вызваший событие объект.</param>
        /// <param name="wrapper">Объект, с которым произошло столкновение.</param>
        public delegate void EventHandler(GameObject sender, Wrapper wrapper);

        /// <summary>
        /// Событие, вызываемое при столкновении.
        /// </summary>
        public event EventHandler TriggerEnter;

        /// <summary>
        /// Событие, вызываемое при выходе из столкновения.
        /// </summary>
        public event EventHandler TriggerExit;

        /// <summary>
        /// При столкновении.
        /// </summary>
        /// <param name="other">Объект, с которым произошло столкновение.</param>
        private void OnTriggerEnter(Collider other)
        {
            var wrapper = other.gameObject.GetWrapper();
            if (wrapper == null)
            {
                return;
            }
            
            TriggerEnter?.Invoke(gameObject, wrapper);
        }

        /// <summary>
        /// При выходе из столкновения.
        /// </summary>
        /// <param name="other">Объект, с которым произошло столкновение.</param>
        private void OnTriggerExit(Collider other)
        {
            var wrapper = other.gameObject.GetWrapper();
            if (wrapper == null)
            {
                return;
            }
            
            TriggerExit?.Invoke(gameObject, wrapper);
        }
    }
}