using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Varwin.Core;
using Varwin.Core.Behaviours.ConstructorLib;
using Varwin.ObjectsInteractions;
using Varwin.Public;
using Varwin.WWW;
using Object = UnityEngine.Object;

namespace Varwin
{
    /// <summary>
    /// Менеджер клонирования объектов.
    /// </summary>
    public static class CloneManager
    {
        /// <summary>
        /// Склонировать объект в заданную позицию.
        /// </summary>
        /// <param name="targetObject">Оригинальный объект.</param>
        /// <param name="position">Позиция.</param>
        /// <returns>Экземпляр клона.</returns>
        public static Wrapper CloneAtPosition(dynamic targetObject, dynamic position)
        {
            if (targetObject is not Wrapper originalObject)
            {
                LogError("ERROR_CANT_CLONE_TYPE_MISMATCH", "type", LocalizationUtils.GetLocalizedType(targetObject?.GetType()));
                return null;
            }

            if (position is not Vector3 targetPosition)
            {
                var nameParameter = new KeyValuePair<string, object>("object_name", originalObject.GetObjectController().GetLocalizedName());
                var targetParameter = new KeyValuePair<string, object>("type", LocalizationUtils.GetLocalizedType(position?.GetType()));
                var needParameter = new KeyValuePair<string, object>("need_type", LocalizationUtils.GetLocalizedType(typeof(Vector3)));

                LogError("ERROR_CANT_CLONE_AT_POSITION_TYPE_MISMATCH", nameParameter, targetParameter, needParameter);
                return null;
            }

            var clone = Clone(originalObject);
            clone.GetGameObject().transform.position = targetPosition;
            return clone;
        }

        /// <summary>
        /// Склонировать объект в позицию объекта.
        /// </summary>
        /// <param name="targetObject">Оригинальный объект.</param>
        /// <param name="locationObject">Объект-позиция.</param>
        /// <returns>Экземпляр клона.</returns>
        public static Wrapper CloneAtObjectPosition(dynamic targetObject, dynamic locationObject)
        {
            if (targetObject is not Wrapper originalObject)
            {
                LogError("ERROR_CANT_CLONE_TYPE_MISMATCH", "type", LocalizationUtils.GetLocalizedType(targetObject?.GetType()));
                return null;
            }

            if (locationObject is not Wrapper targetLocationObject)
            {
                var nameParameter = new KeyValuePair<string, object>("object_name", originalObject.GetObjectController().GetLocalizedName());
                var targetParameter = new KeyValuePair<string, object>("type", LocalizationUtils.GetLocalizedType(locationObject?.GetType()));
                var needParameter = new KeyValuePair<string, object>("need_type", LocalizationUtils.GetLocalizedType(typeof(Wrapper)));

                LogError("ERROR_CANT_CLONE_AT_POSITION_TYPE_MISMATCH", nameParameter, targetParameter, needParameter);
                return null;
            }

            if (IsUnclonableObject(originalObject.GetObjectController()))
            {
                LogError("ERROR_CANT_CLONE_UNCLONABLE_OBJECT", "object_name", originalObject.GetObjectController().GetLocalizedName());
                return null;
            }

            var clone = Clone(originalObject);
            clone.GetGameObject().transform.position = targetLocationObject.GetGameObject().transform.position;
            return clone;
        }

        /// <summary>
        /// Склонировать объект.
        /// </summary>
        /// <param name="targetObject">Оригинальный объект.</param>
        /// <returns>Экземпляр клона.</returns>
        public static Wrapper Clone(dynamic targetObject)
        {
            if (targetObject is not Wrapper originalObject)
            {
                LogError("ERROR_CANT_CLONE_TYPE_MISMATCH", "type", LocalizationUtils.GetLocalizedType(targetObject?.GetType()));
                return null;
            }

            var canClone = PrepareHierarchy(originalObject.GetObjectController(), null);
            
            if (!canClone)
            {
                return null;
            }

            var clone = CloneHierarchy(originalObject);
            clone.GetGameObject().transform.position = originalObject.GetGameObject().transform.position;
            return clone;
        }

        /// <summary>
        /// Подготовка иерархии к копированию.
        /// </summary>
        /// <param name="rootObject">Исходный объект.</param>
        /// <param name="callback">Метод, вызываемый для каждого объекта иерархии.</param>
        /// <returns>Истина, если все объекты</returns>
        private static bool PrepareHierarchy(ObjectController rootObject, Action<ObjectController> callback)
        {
            if (rootObject.IsEmbedded)
            {
                LogError("ERROR_CANT_CLONE_EMBEDDED_OBJECT", "object_name", rootObject.GetLocalizedName());
                return false;
            }

            if (rootObject.gameObject.GetComponent<CloneTarget>())
            {
                LogError("ERROR_CANT_CLONE_A_CLONED_OBJECT", "object_name", rootObject.GetLocalizedName());
                return false;
            }

            if (IsUnclonableObject(rootObject))
            {
                LogError("ERROR_CANT_CLONE_UNCLONABLE_OBJECT", "object_name", rootObject.GetLocalizedName());
                return false;
            }

            var clonedSource = rootObject.gameObject.GetComponent<CloneSource>();
            if (!clonedSource)
            {
                clonedSource = rootObject.gameObject.AddComponent<CloneSource>();
                clonedSource.Initialize();
            }

            clonedSource.BeforeCreatingClone();
            callback?.Invoke(rootObject);

            return rootObject.Children.All(arg => PrepareHierarchy(arg, callback));
        }

        /// <summary>
        /// Склонировать иерархию объекта.
        /// </summary>
        /// <param name="originalObject">Оригинальный объект.</param>
        /// <param name="newParent">Новый родитель для объекта.</param>
        /// <returns>Клон объекта с иерархией.</returns>
        private static Wrapper CloneHierarchy(Wrapper originalObject, ObjectController newParent = null)
        {
            var originalObjectController = originalObject.GetObjectController();
            var clonedSource = originalObject.GetGameObject().GetComponent<CloneSource>();
            var originalHierarchyController = originalObjectController.GetHierarchyController();
            var configurableJoint = originalHierarchyController.Joint;
            var jointAnchor = Vector3.zero;
            var jointConnectedAnchor = Vector3.zero;
            Rigidbody jointConnectedBody = null;

            bool hasJoint = configurableJoint;

            if (configurableJoint)
            {
                jointAnchor = configurableJoint.anchor;
                jointConnectedAnchor = configurableJoint.connectedAnchor;
                jointConnectedBody = configurableJoint.connectedBody;

                configurableJoint.connectedBody = null;
                Object.Destroy(configurableJoint);
            }

            var spawnInitParams = originalObjectController.GetSpawnInitParams();
            var instance = Object.Instantiate(originalObject.GetGameObject());
            var objectBehaviourWrapper = instance.GetComponent<ObjectBehaviourWrapper>();

            if (instance.TryGetComponent<CollisionController>(out var collisionController))
            {
                Object.Destroy(collisionController);

                var collisions = instance.GetComponentsInChildren<CollisionControllerElement>(true);
                foreach (var controllerElement in collisions)
                {
                    Object.Destroy(controllerElement);
                }
            }
            
            if (objectBehaviourWrapper)
            {
                Object.DestroyImmediate(objectBehaviourWrapper);
            }

            spawnInitParams.IdInstance = 0;

            if (newParent != null)
            {
                spawnInitParams.ParentId = newParent.Id;
            }

            Helper.InitObject(spawnInitParams.IdObject, spawnInitParams, instance, originalObjectController.GetLocalizedNames(), true);

            var instanceClonedSource = instance.GetComponent<CloneSource>();
            if (instanceClonedSource)
            {
                Object.Destroy(instanceClonedSource);
            }

            var instanceClonedTarget = instance.AddComponent<CloneTarget>();
            instanceClonedTarget.Initialize(clonedSource);

            if (originalObjectController.LockParent)
            {
                foreach (var child in originalObjectController.Children)
                {
                    var childClone = CloneHierarchy(child.gameObject.GetWrapper(), instance.GetWrapper().GetObjectController());
                    if (childClone != null)
                    {
                        childClone.GetGameObject().transform.localScale = child.gameObject.transform.localScale;
                    }
                }
            }
            
            clonedSource.AddClone(instanceClonedTarget.gameObject);
            CloneParams(originalObject, instance.GetWrapper());

            if (hasJoint)
            {
                CreateHierarchyJoint(originalObject, jointConnectedBody, jointConnectedAnchor, jointAnchor);
            }
            
            return instance.GetWrapper();
        }

        /// <summary>
        /// Создание joint'a иерархии.
        /// </summary>
        /// <param name="originalObject">Оригинальный объект.</param>
        /// <param name="jointConnectedBody">Объект, который является родительским.</param>
        /// <param name="jointConnectedAnchor">Сдвиг подключенного объектаю</param>
        /// <param name="jointAnchor">Ось подключенного объекта.</param>
        private static void CreateHierarchyJoint(Wrapper originalObject, Rigidbody jointConnectedBody, Vector3 jointConnectedAnchor, Vector3 jointAnchor)
        {
            var originalHierarchyController = originalObject.GetObjectController().GetHierarchyController();
            originalHierarchyController.Joint = originalObject.GetGameObject().AddComponent<ConfigurableJoint>();

            originalHierarchyController.Joint.xMotion = ConfigurableJointMotion.Locked;
            originalHierarchyController.Joint.yMotion = ConfigurableJointMotion.Locked;
            originalHierarchyController.Joint.zMotion = ConfigurableJointMotion.Locked;

            originalHierarchyController.Joint.angularXMotion = ConfigurableJointMotion.Locked;
            originalHierarchyController.Joint.angularYMotion = ConfigurableJointMotion.Locked;
            originalHierarchyController.Joint.angularZMotion = ConfigurableJointMotion.Locked;

            originalHierarchyController.Joint.projectionMode = JointProjectionMode.PositionAndRotation;
            originalHierarchyController.Joint.projectionDistance = 0;
            originalHierarchyController.Joint.projectionAngle = 0;

            originalHierarchyController.Joint.connectedBody = jointConnectedBody;
            originalHierarchyController.Joint.connectedAnchor = jointConnectedAnchor;
            originalHierarchyController.Joint.anchor = jointAnchor;
        }

        /// <summary>
        /// Склонировать параметры объекта через ожидание кадра, так как в ObjectController'е
        /// значения ресурсов устанавливаются спустя кадр.
        /// </summary>
        /// <param name="source">Оригинальный объект.</param>
        /// <param name="target">Целевой объект.</param>
        private static void CloneParams(Wrapper source, Wrapper target)
        {
            CloneWrapper(source, target);
            CloneCollisionEvents(source, target);
            CloneBehaviours(source, target);
        }

        /// <summary>
        /// Склонировать параметры стандартных поведений.
        /// </summary>
        /// <param name="original">Оригинальный объект.</param>
        /// <param name="clone">Клон объекта.</param>
        private static void CloneBehaviours(Wrapper original, Wrapper clone)
        {
            foreach (var originalVarwinBehaviour in original.GetBehaviours())
            {
                if (originalVarwinBehaviour.GetType().FastGetCustomAttribute<ObsoleteAttribute>(true) != null)
                {
                    continue;
                }

                foreach (var clonedVarwinBehaviour in clone.GetBehaviours())
                {
                    if (clonedVarwinBehaviour.GetType().FastGetCustomAttribute<ObsoleteAttribute>(true) != null)
                    {
                        continue;
                    }

                    if (originalVarwinBehaviour.GetType() != clonedVarwinBehaviour.GetType())
                    {
                        continue;
                    }

                    CloneUtils.CloneEvents(originalVarwinBehaviour, clonedVarwinBehaviour);
                    CloneUtils.CloneProperties(originalVarwinBehaviour, clonedVarwinBehaviour);

                    if (originalVarwinBehaviour is VisualizationBehaviour originalVisualization)
                    {
                        CloneVisualizationBehaviour(originalVisualization, (VisualizationBehaviour)clonedVarwinBehaviour);
                    }
                }
            }
        }

        /// <summary>
        /// Установить свойства для Visualization Behaviour у склонированного объекта.
        /// Нужно, чтобы пробросить значения в MaterialPropertyBlock и скопировать текстуры.
        /// </summary>
        /// <param name="originalBehaviour">Оригинальный Visualization Behaviour.</param>
        /// <param name="clonedBehaviour">Клонированный Visualization Behaviour.</param>
        private static void CloneVisualizationBehaviour(VisualizationBehaviour originalBehaviour, VisualizationBehaviour clonedBehaviour)
        {
            originalBehaviour.CopyPropertiesTo(clonedBehaviour);
        }

        /// <summary>
        /// Склонировать параметры Wrapper'a (те, что предоставляются из SDK).
        /// </summary>
        /// <param name="original">Оригинальный объект.</param>
        /// <param name="clone">Клон.</param>
        private static void CloneWrapper(Wrapper original, Wrapper clone)
        {
            CloneUtils.CloneEvents(original, clone);
            CloneUtils.CloneProperties(original, clone);
        }

        /// <summary>
        /// Склонировать события обработчика столкновений.
        /// </summary>
        /// <param name="original">Оригинальный объект.</param>
        /// <param name="clone">Клон.</param>
        private static void CloneCollisionEvents(Wrapper original, Wrapper clone)
        {
            var originalCollisionCallbackHandler = original.GetGameObject().GetComponent<CollisionDispatcher.CollisionCallbackHandler>();
            if (!originalCollisionCallbackHandler)
            {
                return;
            }

            var cloneCollisionCallbackHandler = clone.GetGameObject().GetComponent<CollisionDispatcher.CollisionCallbackHandler>();

            foreach (var keyPair in originalCollisionCallbackHandler.ObjectStartCollisionHandlers)
            {
                cloneCollisionCallbackHandler.ObjectStartCollisionHandlers.Add(keyPair.Key, keyPair.Value);
            }

            foreach (var keyPair in originalCollisionCallbackHandler.ObjectEndCollisionHandlers)
            {
                cloneCollisionCallbackHandler.ObjectEndCollisionHandlers.Add(keyPair.Key, keyPair.Value);
            }

            foreach (var collisionHandler in originalCollisionCallbackHandler.AllObjectsStartCollisionHandlers)
            {
                cloneCollisionCallbackHandler.AllObjectsStartCollisionHandlers.Add(collisionHandler);
            }

            foreach (var collisionHandler in originalCollisionCallbackHandler.AllObjectsEndCollisionHandlers)
            {
                cloneCollisionCallbackHandler.AllObjectsEndCollisionHandlers.Add(collisionHandler);
            }
        }

        /// <summary>
        /// Удалить склонированный объект.
        /// </summary>
        /// <param name="targetObject">Клон.</param>
        public static void Destroy(dynamic targetObject)
        {
            if (targetObject is not Wrapper originalObject)
            {
                LogError("ERROR_DESTROY_CLONE_TYPE_MISMATCH", "type", LocalizationUtils.GetLocalizedType(targetObject?.GetType()));
                return;
            }

            var originalGameObject = originalObject.GetGameObject();
            if (!originalGameObject)
            {
                LogError("ERROR_DESTROY_CLONE_IS_NOT_CLONE", "object_name", null);
                return;
            }

            var cloneObject = originalGameObject.GetComponent<CloneTarget>();
            if (!cloneObject)
            {
                LogError("ERROR_DESTROY_CLONE_IS_NOT_CLONE", "object_name", originalObject.GetObjectController().GetLocalizedName());
                return;
            }

            Destroy(originalObject.GetObjectController());
        }

        /// <summary>
        /// Удаление всех клонов объекта.
        /// </summary>
        /// <param name="targetObject">Оригинальный объект.</param>
        public static void DestroyAllClones(dynamic targetObject)
        {
            if (targetObject is not Wrapper originalObject)
            {
                LogError("ERROR_DESTROY_CLONES_TYPE_MISMATCH", "type", LocalizationUtils.GetLocalizedType(targetObject?.GetType()));
                return;
            }

            var cloneObject = originalObject.GetGameObject().GetComponent<CloneSource>();
            if (!cloneObject)
            {
                LogError("ERROR_DESTROY_CLONES_IS_NOT_ORIGINAL", "object_name", originalObject.GetObjectController().GetLocalizedName());
                return;
            }

            foreach (var clone in cloneObject.GetClones())
            {
                Destroy(clone.GetWrapper().GetObjectController());
            }
        }

        /// <summary>
        /// Удаление объектов вместе с иерархией.
        /// </summary>
        /// <param name="objectController">Объект для удаления.</param>
        private static void Destroy(ObjectController objectController)
        {
            foreach (var child in objectController.Children)
            {
                Destroy(child);
            }

            objectController.Delete();
        }

        /// <summary>
        /// Проверяет уничтожен ли объект.
        /// </summary>
        /// <param name="targetObject">Объект.</param>
        /// <returns>Истина, если уничтожен.</returns>
        public static bool IsDestroyed(dynamic targetObject)
        {
            if (targetObject is not Wrapper originalObject)
            {
                LogError("ERROR_IS_DESTROYED_TYPE_MISMATCH", "type", LocalizationUtils.GetLocalizedType(targetObject?.GetType()));
                return true;
            }
            
            return originalObject.GetObjectController() == null || originalObject.GetObjectController().IsDeleted;
        }

        /// <summary>
        /// Получение списка клонов объекта.
        /// </summary>
        /// <param name="targetObject">Оригинальный объект.</param>
        /// <returns>Список клонов объекта.</returns>
        public static List<Wrapper> GetClones(dynamic targetObject)
        {
            if (targetObject is not Wrapper originalObject)
            {
                LogError("ERROR_GET_CLONES_TYPE_MISMATCH", "type", LocalizationUtils.GetLocalizedType(targetObject?.GetType()));
                return null;
            }

            if (originalObject.GetGameObject().GetComponent<CloneTarget>())
            {
                LogError("ERROR_GET_CLONES_IS_NOT_ORIGINAL", "object_name", originalObject.GetObjectController().GetLocalizedName());
                return null;
            }
            
            var cloneObject = originalObject.GetGameObject().GetComponent<CloneSource>();
            if (!cloneObject)
            {
                return new List<Wrapper>();
            }

            return cloneObject.GetClones().Select(a => a.GetWrapper()).ToList();
        }

        /// <summary>
        /// Является ли объект клоном другого объекта.
        /// </summary>
        /// <param name="targetObject">Объект.</param>
        /// <param name="possibleOriginalObject">Предполагаемый оригинальный объект</param>
        /// <returns>Истина, если объект является клоном.</returns>
        public static bool IsCloneOfObject(dynamic targetObject, dynamic possibleOriginalObject)
        {
            if (targetObject is not Wrapper clonedWrapper)
            {
                LogError("ERROR_IS_CLONE_TYPE_MISMATCH", "type", LocalizationUtils.GetLocalizedType(targetObject?.GetType()));
                return false;
            }

            if (possibleOriginalObject is not Wrapper possibleOriginalWrapper)
            {
                LogError("ERROR_IS_CLONE_TYPE_MISMATCH", "type", LocalizationUtils.GetLocalizedType(possibleOriginalObject?.GetType()));
                return false;
            }

            var cloneTarget = clonedWrapper.GetGameObject().GetComponent<CloneTarget>();
            if (!cloneTarget)
            {
                return false;
            }
            
            var originalSource = possibleOriginalWrapper.GetGameObject().GetComponent<CloneSource>();
            if (!originalSource)
            {
                return false;
            }

            return cloneTarget.Source == originalSource;
        }

        /// <summary>
        /// Является ли объект клоном.
        /// </summary>
        /// <param name="targetObject">Объект.</param>
        /// <returns>Истина, если объект является клоном.</returns>
        public static bool IsClone(dynamic targetObject)
        {
            if (targetObject is not Wrapper originalObject)
            {
                LogError("ERROR_IS_CLONE_TYPE_MISMATCH", "type", LocalizationUtils.GetLocalizedType(targetObject?.GetType()));
                return false;
            }

            return originalObject.GetGameObject().GetComponent<CloneTarget>();
        }

        /// <summary>
        /// Попытаться получить оригинал объекта.
        /// </summary>
        /// <param name="sourceObject">Исходный объект.</param>
        /// <param name="originalObject">Оригинальный объект.</param>
        /// <returns>Истина, если получить возможно.</returns>
        public static bool TryGetOriginal(Wrapper sourceObject, out Wrapper originalObject)
        {
            if (sourceObject is not Wrapper wrapper)
            {
                originalObject = null;
                return false;
            }

            var cloneTarget = wrapper.GetGameObject().GetComponent<CloneTarget>();
            if (!cloneTarget)
            {
                originalObject = null;
                return false;
            }

            originalObject = cloneTarget.Source ? cloneTarget.Source.gameObject.GetWrapper() : null;
            return originalObject != null;
        }

        /// <summary>
        /// Проверка объекта на наличие интерфейса Unclonable.
        /// </summary>
        /// <param name="objectController">Object Controller проверяемого объекта.</param>
        /// <returns>true - если объект содержит интерфейс Unclonable, false - не содержит.</returns>
        private static bool IsUnclonableObject(ObjectController objectController) => objectController.gameObject.GetComponent<Unclonable>();

        /// <summary>
        /// Вывод ошибки в консоль.
        /// </summary>
        /// <param name="errorLocale">Имя локали ошибки.</param>
        /// <param name="key">Ключ в локализации.</param>
        /// <param name="value">Значение в локализации.</param>
        private static void LogError(string errorLocale, string key, string value)
        {
            var keyValuePair = new KeyValuePair<string, object>(key, value);
            LogError(errorLocale, keyValuePair);
        }

        /// <summary>
        /// Вывод ошибки в консоль.
        /// </summary>
        /// <param name="errorLocale">Имя локали ошибки.</param>
        /// <param name="arguments">Аргументы.</param>
        private static void LogError(string errorLocale, params KeyValuePair<string, object>[] arguments)
        {
            Debug.LogError(I18next.Format(SmartLocalization.LanguageManager.Instance.GetTextValue(errorLocale), arguments));
        }
    }
}