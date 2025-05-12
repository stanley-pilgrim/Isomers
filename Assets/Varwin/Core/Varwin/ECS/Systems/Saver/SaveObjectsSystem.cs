using System.Collections.Generic;
using System.Linq;
using Entitas;
using Newtonsoft.Json;
using UnityEngine;
using Varwin.Data;
using Varwin.Data.ServerData;
using Varwin.GraphQL;
using Varwin.Models.Data;
using Varwin.Public;
using Varwin.WWW;
using ObjectData = Varwin.Data.ServerData.ObjectData;

namespace Varwin.ECS.Systems.Saver
{
    public sealed class SaveObjectsSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _allEntities;

        public SaveObjectsSystem(Contexts contexts)
        {
            _allEntities = contexts.game.GetGroup(GameMatcher.AllOf(
                GameMatcher.RootGameObject,
                GameMatcher.IdServer, GameMatcher.Name, GameMatcher.Id, GameMatcher.IdObject));
        }

        class ObjectIds
        {
            public int InstanceId;
            public int Id;
        }

        public bool? IsForceSave { private get; set; }

        public void Execute()
        {
            var dictionary = new Dictionary<int, SceneObjectDto>();
            var result = new List<SceneObjectDto>();
            var uniqueObjects = new HashSet<int>();
            var objectBehavioursData = new List<ObjectBehavioursData>();
            var hierarchyTreeViewStates = new Dictionary<int, bool>();
            if (ProjectData.Joints != null)
            {
                ProjectData.Joints.Clear();
            }
            Data.ServerData.Scene projectScene = ProjectData.ProjectStructure.Scenes.GetProjectScene(ProjectData.SceneId);

            var onDemandedResourceGuids = new HashSet<string>();

            foreach (GameEntity entity in _allEntities)
            {
                ObjectController objectController = GameStateData.GetObjectControllerInSceneById(entity.id.Value);

                if (objectController.IsSceneTemplateObject || objectController.IsVirtualObject)
                {
                    continue;
                }

                var transforms = objectController.GetTransforms();
                var joints = objectController.GetJointData();
                var usingResourcesData = objectController.GetUsingResourcesData();

                var objectOnDemandedResourceGuids = objectController.GetOnDemandedResourceGuids();
                foreach (string objectOnDemandedResourceGuid in objectOnDemandedResourceGuids)
                {
                    if (!onDemandedResourceGuids.Contains(objectOnDemandedResourceGuid))
                    {
                        onDemandedResourceGuids.Add(objectOnDemandedResourceGuid);
                    }
                }

                var objectBehaviours = objectController.GetObjectBehaviours();

                var sourcePropertiesData = objectController.GetInspectorPropertiesData();
                var propertiesData = new List<InspectorPropertyData>();
                if (sourcePropertiesData != null)
                {
                    foreach (var inspectorPropertyData in sourcePropertiesData)
                    {
                        propertiesData.Add(new InspectorPropertyData
                        {
                            ComponentPropertyName = inspectorPropertyData.ComponentPropertyName,
                            PropertyValue = new PropertyValue
                            {
                                ResourceGuids = inspectorPropertyData.PropertyValue?.ResourceGuids,
                                ResourceGuid = inspectorPropertyData.PropertyValue?.ResourceGuid,
                                Value = inspectorPropertyData.PropertyValue?.Value
                            }
                        });
                    }
                }

                var newTreeSceneObjectDto = new SceneObjectDto
                {
                    Id = projectScene.HasObjectOnServer(entity.idServer.Value) ? (int?) entity.idServer.Value : null,
                    Name = entity.name.Value,
                    InstanceId = entity.id.Value,
                    ObjectId = entity.idObject.Value,
                    Data = new ObjectData
                    {
                        LocalTransform = objectController.ToLocalTransformDT(),
                        Transform = transforms,
                        RootTransform = objectController.gameObject.transform.ToTransformDT(),
                        JointData = joints,
                        InspectorPropertiesData = propertiesData,
                        LockChildren = objectController.LockChildren,
                        IsDisabled = !objectController.ActiveSelf,
                        IsDisabledInHierarchy = !objectController.ActiveInHierarchy,
                        DisableSelectabilityInEditor = !objectController.SelectableInEditor,
                        Index = objectController.Index
                    },
                    DisableSceneLogic = !objectController.EnableInLogicEditor,
                    ResourceIds = usingResourcesData.Select(resource => resource.Id).ToList(),
                    SceneObjects = new List<SceneObjectDto>()
                };

                if (joints != null)
                {
                    ProjectData.Joints.Add(entity.id.Value, joints);
                }

                if (entity.hasIdParent)
                {
                    if (objectController.Parent.gameObject.GetComponent<VirtualObject>())
                    {
                        newTreeSceneObjectDto.ParentId = objectController.Parent.Parent.Id;
                        newTreeSceneObjectDto.Data.VirtualParentObjectId = objectController.Parent.IdObject;
                    }
                    else
                    {
                        newTreeSceneObjectDto.ParentId = entity.idParent.Value;    
                    }
                }
                else
                {
                    newTreeSceneObjectDto.ParentId = null;
                }

                foreach (var virtualObject in objectController.GetVirtualObjects())
                {
                    newTreeSceneObjectDto.Data.VirtualObjectsInfos ??= new List<VirtualObjectInfo>();
                    newTreeSceneObjectDto.Data.VirtualObjectsInfos.Add(new VirtualObjectInfo
                    {
                        InstanceId = virtualObject.Id,
                        Id = virtualObject.IdObject, 
                        LockChildren = virtualObject.LockChildren,
                        HierarchyExpandedState = virtualObject.HierarchyExpandedState
                    });
                }

                dictionary.Add(entity.id.Value, newTreeSceneObjectDto);
                hierarchyTreeViewStates[entity.id.Value] = objectController.HierarchyExpandedState;

                if (uniqueObjects.Contains(entity.idObject.Value))
                {
                    continue;
                }

                uniqueObjects.Add(entity.idObject.Value);
                objectBehavioursData.Add(
                    new ObjectBehavioursData
                    {
                        ObjectId = entity.idObject.Value,
                        Behaviours = objectBehaviours
                    });
            }

            foreach (SceneObjectDto treeObject in dictionary.Values.OrderBy(x => x.Data.Index))
            {
                if (treeObject.ParentId != null)
                {
                    dictionary[treeObject.ParentId.Value].SceneObjects.Add(treeObject);
                }
                else
                {
                    result.Add(treeObject);
                }
            }

            var objectsData = new SceneTemplateObjectsDto
            {
                SceneId = ProjectData.SceneId,
                SceneObjects = result,
                SceneData = new CustomSceneData
                {
                    CameraSpawnPosition = CameraManager.DesktopEditorCamera.transform.position,
                    CameraSpawnRotation = CameraManager.DesktopEditorCamera.transform.rotation,
                    HierarchyExpandStates = hierarchyTreeViewStates,
                    OnDemandedResourceGuids = onDemandedResourceGuids
                },
                ObjectBehaviours = objectBehavioursData
            };
            ProjectData.UpdateSceneData(objectsData.SceneData);

            GraphQLQuery mutation = Mutations.GetUpdateSceneObjectsMutation(objectsData);
            var _ = new RequestGraph(mutation)
            {
                OnFinish = response =>
                {
                    var definition = new
                    {
                        data = new
                        {
                            updateSceneObjects = new
                            {
                                scene = new
                                {
                                    sceneObjects = new List<ObjectIds>()
                                }
                            }
                        }
                    };

                    List<ObjectIds> objectIds = JsonConvert.DeserializeAnonymousType((response as ResponseGraph)?.Data, definition).data.updateSceneObjects.scene.sceneObjects;
                    foreach (ObjectIds newObjectId in objectIds)
                    {
                        foreach (SceneObjectDto sceneObject in objectsData.SceneObjects.Where(sceneObject => sceneObject.InstanceId == newObjectId.InstanceId))
                        {
                            sceneObject.Id = newObjectId.Id;
                        }
                    }

                    projectScene.UpdateProjectSceneObjects(objectsData.SceneObjects);
                    Debug.Log($"SceneObjects on scene {ProjectData.SceneId} was saved!");
                    ProjectData.OnSave?.Invoke(IsForceSave ?? false);
                    IsForceSave = null;
                },
                OnError = response => { Debug.LogError($"Can't save scene: {response}"); }
            };
        }
    }
}