using System;
using System.Collections;
using System.Collections.Generic;
using Core.Varwin;
using Varwin.Log;
using Varwin.Data;
using DesperateDevs.Utils;
using Newtonsoft.Json;
using UnityEngine;
using Varwin.Data.ServerData;
using Varwin.ECS.Systems.Saver;
using Varwin.ECS.Systems.UI;
using Varwin.Models.Data;
using Varwin.Public;
using Varwin.Types;
using Varwin.UI;
using Varwin.UI.VRErrorManager;
using Varwin.UI.VRMessageManager;
using Varwin.PlatformAdapter;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Varwin
{
    /// <summary>
    ///     Класс помошник для разных методов. По мере наполнения, надо будет группировать методы по логиге и разбивать на
    ///     классы
    /// </summary>
    public static class Helper
    {
        private static readonly SaveObjectsSystem SaveObjectsSystem = new SaveObjectsSystem(Contexts.sharedInstance);
        private static readonly ShowUiObjects ShowUiObjectsSystem = new ShowUiObjects(Contexts.sharedInstance);
        private static readonly HideUiObjects HideUiObjectsSystem = new HideUiObjects(Contexts.sharedInstance);
        public static event Action<string> OnWrongResponce;

        public static void SpawnSceneObjects(int newSceneId, int oldSceneId, bool force = false)
        {
            if (!force && newSceneId == oldSceneId && !ProjectData.IsRichSceneTemplate)
            {
                Debug.Log($"Objects for scene {newSceneId} already loaded");

                return;
            }

            Debug.Log($"Loading objects for scene {newSceneId}");

            ProjectData.ObjectsAreChanged = false;
            Data.ServerData.Scene projectScene = ProjectData.ProjectStructure.Scenes.GetProjectScene(newSceneId);
            var logicInstance = new LogicInstance(newSceneId);
            SceneLogicManager.SetSceneId(newSceneId);
            GameStateData.RefreshLogic(logicInstance, projectScene.AssemblyBytes);
            ProjectDataListener.Instance.StartCoroutine(CreateSpawnEntities(projectScene.SceneObjects, newSceneId));
        }

        public static void ReloadScene()
        {
            if (ProjectData.IsRichSceneTemplate)
            {
                LoaderAdapter.LoadProject(ProjectData.ProjectId, ProjectData.SceneId, ProjectData.ProjectConfigurationId);
            }
            else
            {
                ReloadSceneObjects();
            }
        }

        public static void ForceReloadScene()
        {
            LoaderAdapter.LoadProject(ProjectData.ProjectId, ProjectData.SceneId, ProjectData.ProjectConfigurationId, true);
        }

        public static void RestartProject()
        {
            object command = new
            {
                command = PipeCommandType.Restart
            };

            if (CommandPipe.Instance)
            {
                CommandPipe.Instance.SendPipeCommand(command, true);
            }
        }

        public static void ReloadSceneObjects()
        {
            ProjectDataListener.Instance.StartCoroutine(ReloadSceneObjectsCoroutine());
        }

        public static IEnumerator ReloadSceneObjectsCoroutine(Action onComplete = null)
        {
            yield return new WaitForEndOfFrame();

            var sceneObjects = ProjectData.ProjectStructure.Scenes.GetProjectScene(ProjectData.SceneId).SceneObjects;
            var serializedObjects = JsonConvert.SerializeObject(sceneObjects);

            Debug.Log($"Reloading objects on scene {ProjectData.SceneId}");
            GameStateData.ClearObjects();
            ProjectData.ObjectsAreChanged = false;
            Data.ServerData.Scene projectScene = ProjectData.ProjectStructure.Scenes.GetProjectScene(ProjectData.SceneId);
            LogicInstance logicInstance = new LogicInstance(ProjectData.SceneId);
            SceneLogicManager.SetSceneId(ProjectData.SceneId);
            GameStateData.RefreshLogic(logicInstance, projectScene.AssemblyBytes);

            projectScene.SceneObjects = JsonConvert.DeserializeObject<List<SceneObjectDto>>(serializedObjects);
            ProjectDataListener.Instance.StartCoroutine(CreateSpawnEntities(projectScene.SceneObjects, ProjectData.SceneId));

            yield return new WaitForEndOfFrame();

            onComplete?.Invoke();
        }

        private static IEnumerator CreateSpawnEntities(List<SceneObjectDto> sceneObjects, int groupId)
        {
            yield return new WaitForEndOfFrame();

            foreach (SceneObjectDto o in sceneObjects)
            {
                CreateSpawnEntity(o, groupId);
            }
        }

        private static void CreateSpawnEntity(SceneObjectDto o, int sceneId, int? parentId = null)
        {
            bool embedded = false;
            PrefabObject prefabObject = ProjectData.ProjectStructure.Objects.GetById(o.ObjectId);

            if (prefabObject != null)
            {
                embedded = prefabObject.Embedded;
            }
            else
            {
                Debug.LogError("Object is not contains in project structure");
            }

            var param = new SpawnInitParams
            {
                IdScene = sceneId,
                IdInstance = o.InstanceId,
                IdObject = o.ObjectId,
                IdServer = o.Id ?? 0,
                Name = o.Name,
                ParentId = parentId,
                Embedded = embedded,
                DisableSceneLogic = o.DisableSceneLogic,
                InternalSpawn = true
            };

            if (o.Data != null)
            {
                param.Transforms = o.Data.Transform;
                param.RootTransform = o.Data.RootTransform;
                param.LocalTransform = o.Data.LocalTransform;
                param.Joints = o.Data.JointData;
                param.InspectorPropertiesData = o.Data.InspectorPropertiesData;
                param.LockChildren = o.Data.LockChildren;
                param.IsDisabled = o.Data.IsDisabled;
                param.IsDisabledInHierarchy = o.Data.IsDisabledInHierarchy;
                param.DisableSelectabilityInEditor = o.Data.DisableSelectabilityInEditor;
                param.Index = o.Data.Index;
                param.VirtualObjectParentId = o.Data.VirtualParentObjectId;
                param.VirtualObjectInfos = o.Data.VirtualObjectsInfos;
            }

            Spawner.Instance.SpawnAsset(param);

            if (o.SceneObjects == null)
            {
                return;
            }

            foreach (SceneObjectDto dto in o.SceneObjects)
            {
                CreateSpawnEntity(dto, sceneId, o.InstanceId);
            }
        }

        public static void SaveSceneObjects(bool forceSave = false)
        {
            if (ProjectData.GameMode != GameMode.Edit)
            {
                return;
            }

            ProjectData.OnPreSave?.Invoke();

            if (GameStateData.MissingInRmsSpawnedObjects.Count > 0)
            {
                Debug.LogWarning("The scene that are not present in RMS library. Please delete them from the scene and then try to save the scene again.");
                return;                
            }

            if (GameStateData.CountOfObjectsUsesMissingResources > 0)
            {
                Debug.LogWarning("Some object properties contain resources that are not present in the RMS library. Please delete them to be able to save the scene.");
                return;
            }

            SaveObjectsSystem.IsForceSave = forceSave;

            SaveObjectsSystem.Execute();
        }

        public static void AskUserToDo(string question, Action actionYes, Action actionNo, Action actionCancel = null)
        {
            if (VRMessageManager.Instance && VRMessageManager.Instance.IsShowing)
            {
                return;
            }

            HideUi();
            VRMessageManager.Instance.Show(question);

            VRMessageManager.Instance.Result = result =>
            {
                switch (result)
                {
                    case DialogResult.Cancel:
                        actionCancel?.Invoke();

                        break;
                    case DialogResult.Yes:
                        actionYes?.Invoke();

                        break;
                    case DialogResult.No:
                        actionNo?.Invoke();
                        break;
                }
            };
        }

        public static void HideUi(bool switchMenuMode = false)
        {
            HideUiObjectsSystem.Execute();

            if (InputAdapter.Instance != null)
            {
                InputAdapter.Instance.PointerController.IsMenuOpened = switchMenuMode;
            }
        }

        public static void ShowUi()
        {
            if (VRMessageManager.Instance && VRMessageManager.Instance.IsShowing)
            {
                return;
            }

            if (VRErrorManager.Instance && VRErrorManager.Instance.IsShowing)
            {
                return;
            }

            ShowUiObjectsSystem.Execute();

            if (InputAdapter.Instance != null)
            {
                InputAdapter.Instance.PointerController.IsMenuOpened = true;
            }
        }

        public static void ShowFatalErrorLoadObject(PrefabObject o, string error)
        {
            string message = $"Load object {o.Name.en} error!\n{error}.";
            Debug.LogError(message);
            CoreErrorManager.Error(new Exception(message));
            var errorManagerInstance = LauncherErrorManager.Instance;

            if (!errorManagerInstance)
            {
                throw new Exception("LauncherErrorManager.Instance is null");
            }

            errorManagerInstance.ShowFatal(message, string.Empty);
        }

        public static void ShowErrorLoadResource(ResourceDto o, string error)
        {
            string message = $"Load resource {o.Name} error!\n{error}.";
            CoreErrorManager.Error(new Exception(message));
            Debug.LogError(message);
            LauncherErrorManager.Instance.ShowFatal(message, string.Empty);
        }

        public static void ShowErrorLoadScene(string details)
        {
            string message = ErrorHelper.GetErrorDescByCode(ErrorCode.LoadSceneError);
            CoreErrorManager.Error(new Exception(message));
            Debug.LogError("Scene is not loaded");
            LauncherErrorManager.Instance.Show(message, details);
        }

        public static bool HaveSaveTransform(MonoBehaviour behaviour)
        {
            Type type = behaviour.GetType();

            return type.ImplementsInterface<ISaveTransformAware>();
        }

        public static bool HaveInputs(MonoBehaviour behaviour)
        {
            Type type = behaviour.GetType();

            return type.ImplementsInterface<IVarwinInputAware>();
        }


        /// <summary>
        ///     Initialize object in platform
        /// </summary>
        /// <param name="idObject">Object type id. Used for save.</param>
        /// <param name="spawnInitParams">Parameters for spawn</param>
        /// <param name="spawnedGameObject">Game object for init</param>
        /// <param name="localizedNames"></param>
        /// <param name="internalSpawn"></param>
        public static void InitObject(int idObject, SpawnInitParams spawnInitParams, GameObject spawnedGameObject, I18n localizedNames, bool internalSpawn = false)
        {
            GameObject gameObjectLink = spawnedGameObject;
            int idScene = spawnInitParams.IdScene;
            int idServer = spawnInitParams.IdServer;
            int idInstance = spawnInitParams.IdInstance;
            bool embedded = spawnInitParams.Embedded;
            string name = spawnInitParams.Name;
            var parentId = spawnInitParams.ParentId;
            var resources = spawnInitParams.InspectorPropertiesData;
            bool lockChildren = spawnInitParams.LockChildren;
            bool disableSelectabilityFromScene = spawnInitParams.DisableSelectabilityInEditor;
            bool disableSceneLogic = spawnInitParams.DisableSceneLogic;
            bool isDisabled = spawnInitParams.IsDisabled;
            bool isDisabledInHierarchy = spawnInitParams.IsDisabledInHierarchy;
            int index = spawnInitParams.Index;
            bool sceneTemplateObject = spawnInitParams.SceneTemplateObject;

            ObjectController parent = null;

            if (parentId != null)
            {
                parent = GameStateData.GetObjectControllerInSceneById(parentId.Value);
            }

            if (parent != null && spawnInitParams.VirtualObjectParentId != null)
            {
                var virtualParent = parent.GetVirtualObject(spawnInitParams.VirtualObjectParentId.Value);
                if (virtualParent != null)
                {
                    parent = virtualParent;    
                }
            }

            WrappersCollection wrappersCollection = null;

            if (idScene != 0)
            {
                wrappersCollection = GameStateData.GetWrapperCollection();
            }
            
            InitObjectParams initObjectParams = new InitObjectParams
            {
                Id = idInstance,
                IdObject = idObject,
                IdScene = idScene,
                IdServer = idServer,
                Asset = gameObjectLink,
                LocalTransform = spawnInitParams.LocalTransform,
                Name = name,
                RootGameObject = spawnedGameObject,
                WrappersCollection = wrappersCollection,
                Parent = parent,
                Embedded = embedded,
                LocalizedNames = localizedNames,
                ResourcesPropertyData = resources,
                LockChildren = lockChildren,
                DisableSelectabilityInEditor = disableSelectabilityFromScene,
                DisableSceneLogic = disableSceneLogic,
                IsDisabled = isDisabled,
                IsDisabledInHierarchy = isDisabledInHierarchy,
                Index = index,
                SceneTemplateObject = sceneTemplateObject,
                VirtualObjectsData = spawnInitParams.VirtualObjectInfos
            };

            var rootObjectId = spawnedGameObject.GetComponent<ObjectId>();
            var transforms = spawnInitParams.Transforms;
            if (transforms != null && rootObjectId && transforms.ContainsKey(rootObjectId.Id))
            {
                initObjectParams.WorldTransform = transforms[rootObjectId.Id];
            }

            var newController = new ObjectController(initObjectParams);

            SpawnManager.OnObjectSpawned(spawnInitParams.SpawnGuid, newController);

            try
            {
                ProjectData.OnObjectSpawned(newController, internalSpawn, spawnInitParams.Duplicated, spawnInitParams.SpawnedByHierarchy);
            }
            catch (Exception e)
            {
                Debug.LogError("Can not invoke method on spawn object in " + newController.Name);
                Debug.LogError(e.Message + e.StackTrace);
            }
        }
        [Obsolete]
        public static Dictionary<int, JointPoint> GetJointPoints(GameObject go)
        {
            var objectIds = go.GetComponentsInChildren<ObjectId>();
            Dictionary<int, JointPoint> jointPoints = new Dictionary<int, JointPoint>();

            foreach (ObjectId objectId in objectIds)
            {
                var jointPoint = objectId.gameObject.GetComponent<JointPoint>();

                if (!jointPoint)
                {
                    continue;
                }

                if (!jointPoints.ContainsKey(objectId.Id))
                {
                    jointPoints.Add(objectId.Id, jointPoint);
                }
            }

            return jointPoints;
        }
        [Obsolete]
        public static void ReloadJointConnections(ObjectController objectController, JointData saveJointData)
        {
            JointBehaviour jointBehaviour = objectController.RootGameObject.GetComponent<JointBehaviour>();
            var jointPoints = GetJointPoints(objectController.RootGameObject);

            foreach (var jointConnectionsData in saveJointData.JointConnectionsData)
            {
                int pointId = jointConnectionsData.Key;
                JointPoint myJointPoint = jointPoints[pointId];
                JointConnectionsData connectionData = jointConnectionsData.Value;
                ObjectController otherObjectController = GameStateData.GetObjectControllerInSceneById(connectionData.ConnectedObjectInstanceId);
                var otherJointPoints = GetJointPoints(otherObjectController.RootGameObject);
                JointPoint otherJointPoint = otherJointPoints[connectionData.ConnectedObjectJointPointId];
                jointBehaviour.ConnectToJointPoint(myJointPoint, otherJointPoint);
                myJointPoint.CanBeDisconnected = !connectionData.ForceLocked;
                otherJointPoint.CanBeDisconnected = !connectionData.ForceLocked;
            }
        }
    }
}