using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Varwin.Data;
using Varwin.Models.Data;
using Varwin.ObjectsInteractions;
using Varwin.WWW;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Varwin
{
    public static class SpawnManager
    {
        public static event Action<SpawnInitParams> SpawnRequest;
        
        private static GameObject _spawnView;
        private static readonly Dictionary<string, Action<ObjectController>> OnSpawnCallbacks = new();
        private static readonly Dictionary<string, PrefabObject> SearchCache = new();

        public static void OnObjectSpawned(string spawnGuid, ObjectController objectController)
        {
            if (spawnGuid == null)
            {
                return;
            }

            if (!OnSpawnCallbacks.TryGetValue(spawnGuid, out var action))
            {
                return;
            }

            action?.Invoke(objectController);
            OnSpawnCallbacks.Remove(spawnGuid);
        }

        /// <summary>
        /// Spawn all objects were found in library with "search" param.
        /// Should be used inside coroutine method: yield return SpawnObject 
        /// </summary>
        /// <param name="search">search param</param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <param name="callback"></param>
        public static IEnumerator Spawn(string search, Vector3 position, Quaternion rotation, Vector3 scale, Action<ObjectController> callback = null)
        {
            bool spawned = false;

            SpawnObjectBySearchPattern(search, position, rotation, scale, (spawnedController) =>
            {
                spawned = true;
                callback?.Invoke(spawnedController);
            });
            yield return new WaitWhile(() => !spawned);
        }
        
        public static IEnumerator CachePrefabObjects(string[] searchParams)
        {
            int apiCounter = 0;
            var loadedFromAPI = new List<PrefabObject>();
            foreach (var searchParam in searchParams)
            {
                if (SearchCache.ContainsKey(searchParam))
                {
                    apiCounter++;
                    continue;
                }

                API.GetLibraryObjects(search: searchParam, callback: (objects) =>
                {
                    var loadedObject = new List<PrefabObject>(objects)[0];

                    if (loadedObject == null)
                    {
                        Debug.LogError($"Can't find object with search param [{searchParam}]");
                        return;
                    }

                    SearchCache.Add(searchParam, loadedObject);
                    loadedFromAPI.Add(loadedObject);
                    apiCounter++;
                });
            }

            yield return new WaitUntil(() => apiCounter >= searchParams.Length);

            var requiredToLoadObjects = LoaderAdapter.Loader.GetRequiredObjects(loadedFromAPI);

            int loadedCounter = 0;
            foreach (var requiredToLoadObject in requiredToLoadObjects)
            {
                LoaderAdapter.LoadPrefabObject(requiredToLoadObject, (loadedObject) =>
                {
                    loadedCounter++;
                });
            }

            yield return new WaitUntil(() => loadedCounter >= requiredToLoadObjects.Count);

            Debug.LogWarning("All prefab objects are cached");
        }

        private static void SpawnObjectBySearchPattern(string search, Vector3 position, Quaternion rotation, Vector3 scale, Action<ObjectController> callback = null)
        {
            if (SearchCache.TryGetValue(search, out var prefabObject))
            {
                if (prefabObject != null)
                {
                    SpawnObjectsWithCallback(prefabObject, position, rotation, scale, callback);
                }
                else
                {
                    Debug.LogError($"Cached Prefab Objects with search param [{search}] is null]");
                }

                return;
            }

            API.GetLibraryObjects(search, callback: (objects) =>
            {
                var loadedObject = new List<PrefabObject>(objects).FirstOrDefault();

                if (loadedObject == null)
                {
                    Debug.LogError($"Can't find object with search param [{search}]");
                    callback?.Invoke(null);
                    return;
                }

                if (!SearchCache.ContainsKey(search))
                {
                    SearchCache.Add(search, loadedObject);
                }

                SpawnObjectsWithCallback(loadedObject, position, rotation, scale, callback);
            });
        }

        private static void SpawnObjectsWithCallback(PrefabObject prefabObject, Vector3 position, Quaternion rotation, Vector3 scale, Action<ObjectController> callback = null)
        {
            var spawnGuid = Guid.NewGuid().ToString();
            if (GameStateData.GetPrefabGameObject(prefabObject.Id))
            {
                SpawnObject(prefabObject.Id, new TransformDT {PositionDT = position, RotationDT = rotation, ScaleDT = scale}, spawnGuid, false);
                if (callback != null)
                {
                    OnSpawnCallbacks.Add(spawnGuid, callback);
                }
            }
            else
            {
                var transformDT = new TransformDT {PositionDT = position, RotationDT = rotation, ScaleDT = scale};
                LoaderAdapter.LoadPrefabObject(prefabObject, (prefab) =>
                {
                    ProjectData.ProjectStructure.Objects.Add(prefabObject);
                    SpawnObject(prefabObject.Id, transformDT, spawnGuid, true);

                    if (callback != null)
                    {
                        OnSpawnCallbacks.Add(spawnGuid, callback);
                    }
                });
            }
        }

        public static void SpawnObject(int objectId, TransformDT initTransformParam = null, string spawnGuid = null, bool internalSpawn = false)
        {
            if (objectId == 0)
            {
                Debug.LogError($"Wrong object Id passed");
            }

            ProjectData.ObjectsAreChanged = true;

            GameObject gameObject = GameStateData.GetPrefabGameObject(objectId);
            int id = gameObject.GetComponent<ObjectId>().Id;

            TransformDT initTransform = initTransformParam == null ? _spawnView.transform.ToTransformDT() : initTransformParam;
            var transforms = new Dictionary<int, TransformDT> {{id, initTransform}};

            SpawnInitParams param = new SpawnInitParams
            {
                IdObject = objectId,
                IdScene = ProjectData.SceneId,
                Transforms = transforms,
                SpawnGuid = spawnGuid,
                InternalSpawn = internalSpawn
            };

            SpawnRequest?.Invoke(param);
        }

        public static void SetSpawnedObject(int objectId)
        {
            ProjectData.SelectedObjectIdToSpawn = objectId;

            if (_spawnView)
            {
                Object.Destroy(_spawnView);
            }

            GameObject go = GameStateData.GetPrefabGameObject(objectId);
            _spawnView = Object.Instantiate(go, GameObjects.Instance.SpawnPoint);
            SetGameObjectToSpawn(_spawnView);
        }

        private static void SetGameObjectToSpawn(GameObject go)
        {
            go.transform.localPosition = Vector3.zero;

            if (ProjectData.PlatformMode == PlatformMode.Vr)
            {
                go.transform.localRotation = Quaternion.identity;
                go.transform.LookAt(GameObjects.Instance.Head);
            }
            else if (ProjectData.PlatformMode == PlatformMode.Desktop)
            {
                go.transform.localEulerAngles = Vector3.zero;
            }

            go.AddComponent<ObjectForSpawn>();
            go.AddComponent<CollisionController>().InitializeController();

            var transforms = go.GetComponentsInChildren<Transform>();

            foreach (Transform child in transforms)
            {
                if (child.TryGetComponent<Rigidbody>(out var body))
                {
                    body.isKinematic = true;
                }

                if (child.TryGetComponent<Animator>(out var animator))
                {
                    animator.enabled = false;
                }

                if (child.TryGetComponent<MonoBehaviour>(out var monoBehaviour))
                {
                    monoBehaviour.enabled = false;
                }
            }
        }

        public static void ResetSpawnObject()
        {
            var spawn = Object.FindObjectOfType<ObjectForSpawn>();

            if (spawn)
            {
                Object.Destroy(spawn.gameObject);
            }

            ProjectData.SelectedObjectIdToSpawn = 0;
        }

        public static bool CanObjectBeSpawned()
        {
            if (_spawnView == null)
            {
                return false;
            }

            if (_spawnView.TryGetComponent<CollisionController>(out var controller))
            {
                return !controller.IsBlocked();
            }

            return false;
        }
    }
}