using System.Linq;
using UnityEngine;

namespace Varwin.Extension
{
    /// <summary>
    /// Данный класс реализует логику подсчета объема фигуры.
    /// </summary>
    public class MeshVolumeCalculator : MonoBehaviour
    {
        /// <summary>
        /// Массив хранилищ фигур.
        /// </summary>
        private MeshFilter[] _meshFilters;

        /// <summary>
        /// Массив хранилищ для анимированных фигур.
        /// </summary>
        private SkinnedMeshRenderer[] _skinnedMeshRenderers;

        /// <summary>
        /// Получение объема фигуры.
        /// </summary>
        public float MeshSize => GetMeshSize();

        /// <summary>
        /// Инициализация и сбор массивов.
        /// </summary>
        private void Awake()
        {
            _meshFilters = GetComponentsInChildren<MeshFilter>();
            _skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        }

        /// <summary>
        /// Функция для вычисления объема фигуры.
        /// </summary>
        /// <returns>Объем фигуры.</returns>
        private float GetMeshSize()
        {
            var value = 0f;

            if (_meshFilters != null)
            {
                value += _meshFilters.Sum(a => GetVolume(Vector3.Scale(a.sharedMesh.bounds.size, a.transform.lossyScale)));
            }
            
            if (_skinnedMeshRenderers != null)
            {
                value += _skinnedMeshRenderers.Sum(a => GetVolume(Vector3.Scale(a.sharedMesh.bounds.size, a.transform.lossyScale)));
            }

            return value;
        }

        /// <summary>
        /// Вычисление объема одной фигуры.
        /// </summary>
        /// <param name="size">Исходный размер фигуры.</param>
        /// <returns>Объем.</returns>
        private float GetVolume(Vector3 size)
        {
            return size.x * size.y * size.z;
        }
    }
}