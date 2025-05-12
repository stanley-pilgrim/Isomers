using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using Varwin.Core.Behaviours;
using Varwin.Data;
using Varwin.Data.ServerData;
using Varwin.Public;
using Varwin.Types;
using Varwin.WWW;
using Object = UnityEngine.Object;

namespace Varwin
{
    public static class GameStateData
    {
        private static readonly Dictionary<string, Assembly> Assemblies = new Dictionary<string, Assembly>();

        private static readonly Dictionary<ObjectController, int> ObjectsIds = new Dictionary<ObjectController, int>();
        private static readonly Dictionary<int, GameObject> Prefabs = new Dictionary<int, GameObject>();
        private static readonly Dictionary<int, PrefabObject> PrefabsData = new Dictionary<int, PrefabObject>();
        private static readonly Dictionary<int, GameEntity> ObjectTypeEntities = new Dictionary<int, GameEntity>();
        private static readonly Dictionary<int, Sprite> ObjectIcons = new Dictionary<int, Sprite>();

        private static readonly Dictionary<string, ResourceObject> Resources = new Dictionary<string, ResourceObject>();
        private static readonly Dictionary<string, ResourceDto> ResourcesData = new Dictionary<string, ResourceDto>();
        private static readonly Dictionary<int, ResourceDto> ResourcesIds = new();
        private static readonly Dictionary<string, GameEntity> ResourceEntities = new Dictionary<string, GameEntity>();

        private static readonly List<int> EmbeddedObjects = new List<int>();
        private static readonly WrappersCollection WrappersCollection = new WrappersCollection();
        private static LogicInstance _logicInstance = null;
        private static GameEntity _logicEntity;
        private static PrefabObject _playerObject;
        private static List<int> _selectedObjectsIds = new List<int>();
        private static HashSet<int> _sceneObjectLockedIds = new HashSet<int>();

        public static HashSet<Hash128> LoadedAssetBundleParts = new HashSet<Hash128>();
        public static readonly HashSet<string> LoadedResources = new HashSet<string>();

        /// <summary>
        /// Список Id отсутствующий в RMS и заспавненных на активной сцене объектов.
        /// </summary>
        public static HashSet<int> MissingInRmsSpawnedObjects { get; } = new();

        /// <summary>
        /// Количество объектов, использующих отсутствующие (в библиотеке RMS) ресурсы.   
        /// </summary>
        public static int CountOfObjectsUsesMissingResources { get; set;  }

        /// <summary>
        /// Флаг, показывающих наличие в сцене отсутствующих (в библиотеке RMS) объектов или ресурсов.
        /// </summary>
        public static bool IsSceneContainsMissingResourcesOrObjects => MissingInRmsSpawnedObjects.Count > 0 || CountOfObjectsUsesMissingResources > 0;

        /// <summary>
        /// Список guid'ов ресурсов, которые были удалены на протяжении работы со сценой.
        /// </summary>
        public static HashSet<string> MissingResourcesGuids { get; } = new();

        public delegate void ObjectRenameHandler(Wrapper wrapper, string oldName, string newName);

        public static event ObjectRenameHandler OnObjectWasRenamed;

        public static void ClearAllData()
        {
            ObjectsIds.Clear();
            Prefabs.Clear();
            PrefabsData.Clear();
            ObjectTypeEntities.Clear();
            ClearObjectIcons();
            ClearResources();
            EmbeddedObjects.Clear();
            WrappersCollection.Clear();
            _selectedObjectsIds.Clear();
            _sceneObjectLockedIds.Clear();

            LoadedAssetBundleParts.Clear();
            LoadedResources.Clear();

            MissingInRmsSpawnedObjects.Clear();
            MissingResourcesGuids.Clear();
            CountOfObjectsUsesMissingResources = 0;
            VarwinBehaviourHelper.ClearCache();
        }

        private static void ClearObjectIcons()
        {
            foreach (var icon in ObjectIcons)
            {
                if (icon.Value)
                {
                    Object.Destroy(icon.Value);
                }
            }

            ObjectIcons.Clear();
        }

        private static void ClearResources()
        {
            foreach (var resourceKeyPair in Resources)
            {
                var resource = resourceKeyPair.Value;
                if (resource.Value is Object resourceValue)
                {
                    Object.Destroy(resourceValue);
                }

                resource.Value = null;
            }

            foreach (var entityPair in ResourceEntities)
            {
                entityPair.Value.Destroy();
            }

            ResourceEntities.Clear();
            Resources.Clear();
            ResourcesData.Clear();
            ResourcesIds.Clear();
            Request3dModel.ClearCache();
        }

        public static void OnDeletingObject(this ObjectController self)
        {
            ProjectData.OnDeletingObject(self);
        }

        public static void Dispose(this ObjectController self)
        {
            if (ObjectsIds.ContainsKey(self))
            {
                ObjectsIds.Remove(self);
            }

            if (ObjectTypeEntities.ContainsKey(self.Id))
            {
                ObjectTypeEntities.Remove(self.Id);
            }

            GCManager.Collect();
        }

        public static void SelectObjects(List<int> newSelection)
        {
            var unselectedObjects = new List<ObjectController>();
            var newSelectedObjects = new List<ObjectController>();

            foreach (int objectId in _selectedObjectsIds)
            {
                if (newSelection.Contains(objectId))
                {
                    continue;
                }

                ObjectController objectController = GetObjectControllerInSceneById(objectId);
                if (objectController == null || objectController.Parent != null && objectController.Parent.IsSelectedInEditor)
                {
                    continue;
                }

                unselectedObjects.Add(objectController);
            }

            foreach (int objectId in newSelection)
            {
                if (!_selectedObjectsIds.Contains(objectId))
                {
                    newSelectedObjects.Add(GetObjectControllerInSceneById(objectId));
                }
            }

            foreach (ObjectController controller in unselectedObjects)
            {
                controller?.OnEditorUnselect();
            }

            foreach (ObjectController controller in newSelectedObjects)
            {
                controller?.OnEditorSelect();
            }

            _selectedObjectsIds = newSelection;
        }

        public static void RegisterMeInScene(this ObjectController self, ref int instanceId, string desiredName)
        {
            if (instanceId == 0)
            {
                int newId;

                if (ObjectsIds.Count == 0)
                {
                    newId = 1;
                }
                else
                {
                    newId = ObjectsIds.Values.ToList().Max() + 1;
                }

                instanceId = newId;

                if (string.IsNullOrEmpty(desiredName))
                {
                    desiredName = self.GetLocalizedName();
                }
            }

            RenameObject(self, desiredName);

            ObjectsIds.Add(self, instanceId);
            ObjectTypeEntities.Add(instanceId, self.Entity);
        }

        public static IEnumerable<int> GetObjectIds()
        {
            return ObjectsIds.Values;
        }

        /// <summary>
        /// Переименование объекта
        /// </summary>
        /// <param name="self"></param>
        /// <param name="desiredName">Желаемое имя</param>
        /// <param name="uniqueName"> Должно ли имя быть уникальным на сцене</param>
        public static void RenameObject(this ObjectController self, string desiredName, bool uniqueName = true)
        {
            if (!ProjectData.IsPlayMode && uniqueName && !IsUniqueName(desiredName, self.ParentId))
            {
                desiredName = GetUniqueName(desiredName, self.ParentId);
            }

            var oldName = self.Name;

            self.SetName(desiredName);
            
            OnObjectWasRenamed?.Invoke(self.gameObject.GetWrapper(), oldName, desiredName);
        }

        private static bool IsUniqueName(string desiredName, int parentId)
        {
            var existingNames = ObjectsIds.Keys
                .Where(x => x.ParentId == parentId)
                .Select(x => x.Entity.name.Value)
                .Where(x => x.IndexOf(desiredName, StringComparison.InvariantCulture) == 0);

            return !existingNames.Contains(desiredName);
        }

        private static string GetUniqueName(string desiredName, int parentId)
        {
            const string regexPattern = @"\((\d*)\)$";
            var match = Regex.Match(desiredName, regexPattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Value != string.Empty)
            {
                desiredName = desiredName.Replace(match.Value, string.Empty);
                desiredName = desiredName.Trim();
            }

            var existingNames = ObjectsIds.Keys
                .Where(x => x.ParentId == parentId)
                .Select(x => x.Entity.name.Value)
                .Where(x => x.IndexOf(desiredName, StringComparison.InvariantCulture) == 0);

            if (!existingNames.Any())
            {
                return desiredName;
            }

            var maxIndex = -1;
            foreach (var item in existingNames)
            {
                match = Regex.Match(item, regexPattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1 && match.Groups[1].Value != string.Empty)
                {
                    if (int.TryParse(match.Groups[1].Value, out var index))
                    {
                        if (index > maxIndex)
                        {
                            maxIndex = index;
                        }
                    }
                }
            }

            return $"{desiredName} ({maxIndex + 1})";
        }

        public static ObjectController GetObjectControllerInSceneById(int idObject)
        {
            foreach (ObjectController value in ObjectsIds.Keys)
            {
                if (value.Id == idObject)
                {
                    return value;
                }
            }

            return null;
        }

        public static WrappersCollection GetWrapperCollection() => WrappersCollection;

        public static void ClearObjects()
        {
            var objectControllers = GetObjectsInScene();

            foreach (ObjectController objectController in objectControllers)
            {
                objectController.Delete();
            }

            ObjectsIds.Clear();
            ObjectTypeEntities.Clear();
            Debug.Log("Scene objects were deleted");
        }

        public static int GetNextObjectIdInScene()
        {
            if (ObjectsIds.Count == 0)
            {
                return 1;
            }

            int newId = ObjectsIds.Values.ToList().Max() + 1;

            return newId;
        }

        public static List<ObjectController> GetObjectsInScene() => ObjectsIds.Keys.ToList();
        public static int SceneObjectsCount => ObjectsIds.Keys.Count;

        public static List<ObjectController> GetRootObjectsScene()
        {
            List<ObjectController> rootObjects = ObjectsIds.Keys.ToList().Where(objectController => objectController.Parent == null).ToList();
            rootObjects = rootObjects.OrderBy(controller => controller.Index).ToList();

            return rootObjects;
        }

        public static void AddPrefabGameObject(Object asset, PrefabObject data)
        {
            if (!Prefabs.ContainsKey(data.Id))
            {
                Prefabs.Add(data.Id, (GameObject) asset);
            }

            if (!PrefabsData.ContainsKey(data.Id))
            {
                PrefabsData.Add(data.Id, data);
            }
        }

        public static void AddResourceObject(ResourceObject resource, ResourceDto resourceDto, GameEntity entity)
        {
            if (!Resources.ContainsKey(resourceDto.Guid))
            {
                Resources.Add(resourceDto.Guid, resource);
            }

            if (!ResourcesData.ContainsKey(resourceDto.Guid))
            {
                ResourcesData.Add(resourceDto.Guid, resourceDto);
            }

            if (!ResourcesIds.ContainsKey(resourceDto.Id))
            {
                ResourcesIds.Add(resourceDto.Id, resourceDto);
            }
            
            ResourceEntities[resourceDto.Guid] = entity;
        }

        public static void AddObjectIcon(int objectId, Sprite sprite)
        {
            if (!ObjectIcons.ContainsKey(objectId))
            {
                ObjectIcons.Add(objectId, sprite);
            }
        }

        public static void AddToEmbeddedList(int objectId)
        {
            if (!EmbeddedObjects.Contains(objectId))
            {
                EmbeddedObjects.Add(objectId);
            }
        }

        public static GameObject GetPrefabGameObject(int objectId) => Prefabs.ContainsKey(objectId) ? Prefabs[objectId] : null;
        public static PrefabObject GetPrefabData(int objectId) => PrefabsData.ContainsKey(objectId) ? PrefabsData[objectId] : null;
        public static PrefabObject GetPrefabData(string rootGuid) => PrefabsData.Values.FirstOrDefault(a => a.RootGuid == rootGuid);

        public static bool ResourceDataIsLoaded(string resourceGuid) => ResourcesData.ContainsKey(resourceGuid);

        public static ResourceObject GetResource(string resourceGuid) => Resources.ContainsKey(resourceGuid) ? Resources[resourceGuid] : null;

        public static ResourceDto GetResourceDtoById(int id) => ResourcesIds.ContainsKey(id) ? ResourcesIds[id] : null;

        public static ResourceDto GetResourceData(string resourceGuid) => ResourcesData.ContainsKey(resourceGuid) ? ResourcesData[resourceGuid] : null;
        public static object GetResourceValue(string resourceGuid) => GetResource(resourceGuid)?.Value;

        public static List<PrefabObject> GetPrefabsData() => PrefabsData.Values.ToList();

        public static List<ResourceDto> GetResourcesData() => ResourcesData.Values.ToList();
        public static Sprite GetObjectIcon(int objectId) => ObjectIcons.ContainsKey(objectId) ? ObjectIcons[objectId] : null;
        public static bool IsEmbedded(int objectId) => EmbeddedObjects.Contains(objectId);

        public static void RefreshLogic(LogicInstance logicInstance, byte[] assemblyBytes)
        {
            DestroyLogic();
            ClearLogic();
            WrappersCollection.Clear();
            GameEntity logicEntity = Contexts.sharedInstance.game.CreateEntity();
            logicEntity.AddType(null);
            logicEntity.AddLogic(logicInstance);
            logicEntity.AddAssemblyBytes(assemblyBytes, false);
            _logicEntity = logicEntity;
            Debug.Log("Logic was refreshed");
        }

        private static void DestroyLogic()
        {
            if (_logicEntity != null)
            {
                _logicEntity.logic.Value = null;
                EcsUtils.Destroy(_logicEntity);
            }
        }

        public static void ClearLogic()
        {
            _logicInstance?.Clear();
            SceneLogicManager.Clear();
        }

        public static void SetLogic(LogicInstance logicInstance)
        {
            _logicInstance = logicInstance;
        }

        public static GameEntity GetSceneEntity() => _logicEntity;

        public static List<GameEntity> GetEntitiesInScene(int sceneId)
        {
            var result = new List<GameEntity>();

            foreach (var value in ObjectsIds)
            {
                result.Add(value.Key.Entity);
            }

            return result;
        }

        public static GameEntity GetEntity(int id) => ObjectTypeEntities.ContainsKey(id) ? ObjectTypeEntities[id] : null;

        public static void GameModeChanged(GameMode newMode, GameMode oldMode)
        {
            var objects = GetAllObjects();

            foreach (ObjectController o in objects)
            {
                o.ApplyGameMode(newMode, oldMode);
                o.ExecuteSwitchGameModeOnObject(newMode, oldMode);
            }
        }

        public static void PlatformModeChanged(PlatformMode newMode, PlatformMode oldMode)
        {
            var objects = GetAllObjects();

            foreach (ObjectController o in objects)
            {
                o.ApplyPlatformMode(newMode, oldMode);
                o.ExecuteSwitchPlatformModeOnObject(newMode, oldMode);
            }
        }

        private static List<ObjectController> GetAllObjects() => ObjectsIds.Keys.ToList();

        public static void AddAssembly(string dllName, Assembly assembly)
        {
            if (Assemblies.ContainsKey(dllName))
            {
                return;
            }

            Assemblies.Add(dllName, assembly);
        }

        public static Assembly GetAssembly(string dllName)
        {
            return Assemblies.TryGetValue(dllName, out var assembly) ? assembly : null;
        }

        public static PrefabObject PlayerObject => _playerObject;

        public static void SetPlayerObject(PrefabObject playerObject)
        {
            if (_playerObject == null)
            {
                _playerObject = playerObject;
            }
        }

        public static bool HasObjectOnScene(int spawnParamIdInstance) => WrappersCollection.ContainsKey(spawnParamIdInstance);

        public static bool ObjectIsLocked(ObjectController objectController) => ObjectIsLocked(objectController.IdServer);
        public static bool ObjectIsLocked(int objectControllerIdServer) => _sceneObjectLockedIds.Contains(objectControllerIdServer);

        public static void UpdateSceneObjectLockedIds(IEnumerable<LockedSceneObject> changedObjects)
        {
            foreach (LockedSceneObject lockedObject in changedObjects)
            {
                if (lockedObject.UsedInSceneLogic)
                {
                    _sceneObjectLockedIds.Add(lockedObject.Id);
                }
                else
                {
                    _sceneObjectLockedIds.Remove(lockedObject.Id);
                }
            }
        }

        public class LockedSceneObject
        {
            public int Id;
            public bool UsedInSceneLogic;
        }
    }
}