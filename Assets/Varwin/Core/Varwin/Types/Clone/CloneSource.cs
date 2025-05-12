using System.Collections.Generic;
using UnityEngine;
using Varwin.Public;

namespace Varwin
{
    /// <summary>
    /// Компонент, описывающий оригинальный объект.
    /// </summary>
    public class CloneSource : MonoBehaviour
    {
        /// <summary>
        /// Список клонов объекта.
        /// </summary>
        private List<GameObject> _clones = new();
        
        /// <summary>
        /// Список интерфейсов оригинального объекта.
        /// </summary>
        private ICloneSource[] _cloneSources;

        /// <summary>
        /// Инициализация компонента.
        /// </summary>
        public void Initialize()
        {
            _cloneSources = gameObject.GetComponentsInChildren<ICloneSource>(true);
        }

        /// <summary>
        /// Метод, вызываемый перед созданием клона.
        /// </summary>
        public void BeforeCreatingClone()
        {
            foreach (var cloneSource in _cloneSources)
            {
                cloneSource.BeforeCreatingClone();
            }
        }
        
        /// <summary>
        /// Метод добавления клона.
        /// </summary>
        /// <param name="clonedObject">Объект-клона.</param>
        public void AddClone(GameObject clonedObject)
        {
            _clones.Add(clonedObject);

            foreach (var cloneSource in _cloneSources)
            {
                cloneSource.AfterCreatingClone(clonedObject.GetWrapper());
            }
        }
        
        /// <summary>
        /// Метод удаления клона.
        /// </summary>
        /// <param name="clonedObject">Клон.</param>
        public void DestroyClone(GameObject clonedObject)
        {
            _clones.Remove(clonedObject);
            
            foreach (var cloneSource in _cloneSources)
            {
                cloneSource.OnCloneDestroyed(clonedObject.GetWrapper());
            }
        }

        /// <summary>
        /// Получение списка клонов объекта.
        /// </summary>
        /// <returns>Список клонов.</returns>
        public IEnumerable<GameObject> GetClones()
        {
            return _clones;
        }
    }
}