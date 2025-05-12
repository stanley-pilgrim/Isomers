using System.Collections.Generic;

using UnityEngine;
using Varwin.Public;

namespace Varwin.Extension
{
    /// <summary>
    /// Класс, предназначенный для создания превью цепи объектов.
    /// </summary>
    public static class MeshPreviewBuilder
    {
        /// <summary>
        /// Сборка превью цепи объектов.
        /// </summary>
        /// <param name="parentJointBehaviour">Первый элемент цепи.</param>
        /// <param name="stack">Стек уже обработанных сегментов.</param>
        /// <returns>Указатель на объект.</returns>
        public static GameObject GetJointPreview(JointBehaviour parentJointBehaviour, List<JointBehaviour> stack = null)
        {
            if (stack == null)
            {
                stack = new List<JointBehaviour>();
            }

            stack.Add(parentJointBehaviour);
            
            var obj = GameObject.Instantiate(parentJointBehaviour.PreviewObject);
            obj.SetActive(true);
            
            obj.transform.position = parentJointBehaviour.transform.position;
            obj.transform.rotation = parentJointBehaviour.transform.rotation;

            foreach (var jointBehaviour in parentJointBehaviour.ConnectedJointBehaviours)
            {
                if (stack.Contains(jointBehaviour))
                {
                    continue;
                }

                var newObj = GetJointPreview(jointBehaviour, stack);
                newObj.transform.parent = obj.transform;
            }

            return obj.gameObject;
        }

        /// <summary>
        /// Получение превью объекта.
        /// </summary>
        /// <param name="source">Исходный объект для получения превью.</param>
        /// <returns>Клонированный объект.</returns>
        public static GameObject GetPreview(GameObject source)
        {
            var obj = GetPreviewObject(source);
            obj.transform.localScale = source.transform.localScale;
            
            var highlighter = obj.GetComponent<Highlighter>();
            if (highlighter)
            {
                highlighter.SetConfig(HighlightAdapter.Instance.Configs.MeshPreview);
            }

            obj.SetActive(false);

            return obj;
        }

        /// <summary>
        /// Получение превью одного объекта.
        /// </summary>
        /// <param name="source">Исходный объект.</param>
        /// <returns>Превью объекта.</returns>
        public static GameObject GetPreviewObject(GameObject source)
        {
            var previewObject = new GameObject($"Preview_{source.name}");
            
            previewObject.gameObject.SetActive(source.activeSelf);

            previewObject.CopyComponent(source.GetComponent<MeshFilter>());
            previewObject.CopyComponent(source.GetComponent<MeshRenderer>());
            previewObject.CopyComponent(source.GetComponent<Renderer>());
            previewObject.CopyComponent(source.GetComponent<Highlighter>());

            for (int i = 0; i < source.transform.childCount; i++)
            {
                var childTransform = source.transform.GetChild(i);
                var childPreview = GetPreviewObject(childTransform.gameObject);
                childPreview.transform.parent = previewObject.transform;
                childPreview.transform.localScale = childTransform.localScale;
                childPreview.transform.localRotation = childTransform.localRotation;
                childPreview.transform.localPosition = childTransform.localPosition;
            }

            return previewObject;
        }
    }
}