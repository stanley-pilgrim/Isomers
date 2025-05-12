using UnityEngine;
using Varwin.Public;

namespace Varwin
{
    /// <summary>
    /// Компонент, описывающий клона.
    /// </summary>
    public class CloneTarget : MonoBehaviour
    {
        /// <summary>
        /// Исходный компонент клонирования.
        /// </summary>
        public CloneSource Source { get; private set; }
        
        /// <summary>
        /// Список компонентов клона.
        /// </summary>
        private ICloneTarget[] _cloneTargets;

        /// <summary>
        /// Инициализация клона.
        /// </summary>
        /// <param name="source">Оригинальный объект.</param>
        public void Initialize(CloneSource source)
        {
            Source = source;
            _cloneTargets = GetComponentsInChildren<ICloneTarget>(true);

            foreach (var cloneTarget in _cloneTargets)
            {
                cloneTarget.OnInitialize(Source.gameObject.GetWrapper());
            }
        }

        /// <summary>
        /// Удаление клона из списка клонов.
        /// </summary>
        private void OnDestroy()
        {
            Source.DestroyClone(gameObject);
        }
    }
}