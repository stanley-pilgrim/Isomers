using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Varwin.Data;
using Varwin.Data.ServerData;

namespace Varwin.GraphQL
{
    public static class Subscriptions
    {
        public static GraphQLSubscription LibraryChanged;
        public static GraphQLSubscription ProjectsChanged;
        public static GraphQLSubscription ProjectsLogicChanged;
        public static GraphQLSubscription ProjectBackup;

        public class LibraryChangedSubscription : GraphQLSubscription
        {
            public LibraryChangedSubscription() : base(GraphQLSchema.LibraryChangedQuery)
            {
            }

            protected override void DataReceiveCallback(string data)
            {
                Debug.Log($"{Name}: {data}");

                if (!ProjectDataListener.Instance)
                {
                    return;
                }

                var changedItems = data.JsonDeserialize<Types.LibraryChangedInfo>()?.Payload?.Data?.LibraryChanged;
                if (changedItems == null)
                {
                    return;
                }

                var changedObjects = changedItems.Where(item => item.LibraryItemType == Types.LibraryChangedInfo.LibraryItemType.Object).ToArray();
                var changedResources = changedItems.Where(item => item.LibraryItemType == Types.LibraryChangedInfo.LibraryItemType.Resource).ToArray();

                var removedPackages = changedItems.Where(
                    item => item.LibraryItemType == Types.LibraryChangedInfo.LibraryItemType.Package
                            && item.Type == Types.LibraryChangedInfo.LibraryOperationType.Delete).ToArray();

                bool itemCreated =
                    changedObjects.Any(item => item.Type == Types.LibraryChangedInfo.LibraryOperationType.Create)
                    || changedResources.Any(item => item.Type == Types.LibraryChangedInfo.LibraryOperationType.Create);

                HandleLibraryObjectsChanged(changedObjects);
                HandleLibraryResourcesChanged(changedResources);
                HandleLibraryPackagesRemoved(removedPackages);

                if (itemCreated)
                {
                    ProjectDataListener.Instance.OnLibraryItemCreated();
                }
            }

            private static void HandleLibraryObjectsChanged(Types.LibraryChangedInfo.LibraryItemOperationResult[] libraryChangedObjects)
            {
                var removedObjects = libraryChangedObjects
                    .Where(item => item.Type == Types.LibraryChangedInfo.LibraryOperationType.Delete)
                    .ToArray();

                HandleRemovedObjects(removedObjects);

                var updatedObjects = libraryChangedObjects
                    .Where(item => item.Type == Types.LibraryChangedInfo.LibraryOperationType.Update)
                    .ToArray();

                HandleUpdateObjects(updatedObjects);
            }

            private static void HandleLibraryPackagesRemoved(IEnumerable<Types.LibraryChangedInfo.LibraryItemOperationResult> libraryChangedPackages)
            {
                int[] removedPackagesIds = libraryChangedPackages.Select(item => item.Id).ToArray();
                ProjectDataListener.Instance.OnLibraryPackagesRemoved(removedPackagesIds);
            }

            private static void HandleRemovedObjects(IEnumerable<Types.LibraryChangedInfo.LibraryItemOperationResult> removedObjects)
            {
                int[] removedObjectsIds = removedObjects.Select(item => item.Id).ToArray();
                ProjectDataListener.Instance.OnLibraryObjectsRemoved(removedObjectsIds);
            }

            private static void HandleUpdateObjects(IEnumerable<Types.LibraryChangedInfo.LibraryItemOperationResult> updatedObjects)
            {
                var updatedLibraryObjects =
                    updatedObjects.Select(updatedObject => new ProjectDataListener.UpdatedLibraryObject
                    {
                        Id = updatedObject.Id,
                        Name = Settings.Instance.Language == "ru" ? updatedObject.LibraryItem.Name.Ru : updatedObject.LibraryItem.Name.En,
                    }).ToList();

                ProjectDataListener.Instance.OnLibraryObjectsUpdated(updatedLibraryObjects);
            }

            private static void HandleLibraryResourcesChanged(IEnumerable<Types.LibraryChangedInfo.LibraryItemOperationResult> libraryChangedResources)
            {
                var removedResources = libraryChangedResources
                    .Where(item => item.Type == Types.LibraryChangedInfo.LibraryOperationType.Delete)
                    .ToArray();

                HandleRemovedResources(removedResources);
            }

            private static void HandleRemovedResources(IEnumerable<Types.LibraryChangedInfo.LibraryItemOperationResult> removedResources)
            {
                int[] removedResourcesIds = removedResources.Select(item => item.Id).ToArray();
                ProjectDataListener.Instance.OnLibraryResourcesRemoved(removedResourcesIds);
            }
        }

        public class TaskProgressSubscription : GraphQLSubscription
        {
            public event Action<float> Progressed;
            public event Action<string> Finished;

            public TaskProgressSubscription(string guid) : base(GraphQLSchema.TasksChanged)
            {
                Variables = new Dictionary<string, object>
                {
                    {"guid", guid}
                };
            }

            protected override void DataReceiveCallback(string data)
            {
                Debug.Log($"{Name}: {data}");

                var response = data.JsonDeserialize<Types.TaskChangedResponse>();
                var progress = response.payload.data.taskChanged.progress;

                Progressed?.Invoke(progress);

                if (progress >= 100)
                {
                    var url = response.payload.data.taskChanged.downloadResultUrl;
                    Finished?.Invoke(url);
                }
            }
        }

        public class ProjectsChangedSubscription : GraphQLSubscription
        {
            public ProjectsChangedSubscription(IEnumerable projectIds) : base(GraphQLSchema.ProjectsChangedQuery)
            {
                Variables = new Dictionary<string, object>
                {
                    {"projectIds", projectIds}
                };
            }

            protected override void DataReceiveCallback(string data)
            {
                Debug.Log($"{Name}: {data}");

                Types.ProjectsChangedInfo.ChangedProject[] changedProjects = data.JsonDeserialize<Types.ProjectsChangedInfo>()?.Payload?.Data?.ProjectsChanged;

                if (changedProjects == null || changedProjects.Length == 0)
                {
                    Debug.LogError($"Error reading {Name}");
                    return;
                }

                foreach (Types.ProjectsChangedInfo.ChangedProject changedProject in changedProjects)
                {
                    HandleProjectChange(changedProject);
                }
            }
        }

        public class RestoreSceneBackupSubscription : GraphQLSubscription
        {
            public RestoreSceneBackupSubscription() : base(GraphQLSchema.RestoreSceneBackupQuery) { }

            protected override void DataReceiveCallback(string data)
            {
                Debug.Log($"{Name}: {data}");

                var response = JsonConvert.DeserializeObject<Types.ProjectBackupSubscriptionResponse>(data);

                var projectsChanged = response.payload.data.projectsChanged.FirstOrDefault();
                if (projectsChanged == null)
                {
                    Debug.LogError($"Project backup data contains no changed project. \nData: {data}");
                    return;
                }

                bool isDifferenceScene = projectsChanged.projectId != ProjectData.ProjectId || projectsChanged.sceneId != ProjectData.SceneId;
                if (isDifferenceScene)
                {
                    return;
                }

                ProjectData.OnSceneBackedUp();
            }
        }

        public class ProjectsLogicChangedSubscription : GraphQLSubscription
        {
            public ProjectsLogicChangedSubscription(IEnumerable projectIds) : base(GraphQLSchema.ProjectsEditorLogicChangedQuery)
            {
                Variables = new Dictionary<string, object>
                {
                    {"projectIds", projectIds}
                };
            }

            protected override void DataReceiveCallback(string data)
            {
                Debug.Log($"{Name}: {data}");
                var changedProjects = data.JsonDeserialize<Types.ProjectsChangedInfo>()?.Payload?.Data?.ProjectsChanged;

                if (changedProjects == null || changedProjects.Length == 0)
                {
                    Debug.LogError($"Error reading {Name}");
                    return;
                }

                foreach (Types.ProjectsChangedInfo.ChangedProject changedProject in changedProjects)
                {
                    Types.ProjectsChangedInfo.Scene changedScene = changedProject.Scene;
                    ProjectData.UpdateSceneLogic(changedProject.SceneId);
                    UpdateLogicObjects(changedScene.SceneObjects);
                }
            }
        }

        private static void HandleProjectChange(Types.ProjectsChangedInfo.ChangedProject changedProject)
        {
            Types.ProjectsChangedInfo.Scene changedScene = changedProject.Scene;

            switch (changedProject.Type)
            {
                case Types.ProjectsChangedInfo.ProjectChangeType.Delete:
                    ProjectData.OnProjectRemoved();
                    break;

                case Types.ProjectsChangedInfo.ProjectChangeType.UpdateSettings:
                    if (ProjectData.CurrentScene.Id == changedProject.SceneId && changedProject.Project.MobileReady != ProjectData.ProjectStructure.MobileReady)
                    {
                        ProjectData.OnMobileReadyChanged();
                    }
                    
                    break;

                case Types.ProjectsChangedInfo.ProjectChangeType.UpdateSceneSettings:
                    if (ProjectData.CurrentScene.Id == changedProject.SceneId && ProjectData.CurrentScene.SceneTemplateId != changedProject.Scene.SceneTemplateId)
                    {
                        ProjectData.OnCurrentSceneTemplateChanged();
                    }

                    break;

                case Types.ProjectsChangedInfo.ProjectChangeType.RenameScene:
                    if (ProjectData.CurrentScene.Id == changedProject.SceneId)
                    {
                        ProjectData.RenameCurrentScene(changedScene.Name);
                    }

                    break;

                case Types.ProjectsChangedInfo.ProjectChangeType.DeleteScene:
                    if (ProjectData.CurrentScene.Id == changedProject.SceneId)
                    {
                        ProjectData.OnCurrentSceneRemoved();
                    }

                    break;

                case Types.ProjectsChangedInfo.ProjectChangeType.CreateScene:
                    ProjectData.OnSceneCreated();
                    break;

                case Types.ProjectsChangedInfo.ProjectChangeType.UpdateSceneObjects:
                    if (ProjectData.CurrentScene.Id == changedProject.SceneId)
                    {
                        ProjectData.OnCurrentSceneObjectsChanged();
                    }
                    
                    break;

                case Types.ProjectsChangedInfo.ProjectChangeType.ReplaceLibraryItems:
                    ProjectData.OnLibraryItemsReplaced();
                    break;
            }
        }

        private static void UpdateLogicObjects(IEnumerable<Types.ProjectsChangedInfo.SceneObject> sceneObjects)
        {
            List<GameStateData.LockedSceneObject> lockedObjects =
                sceneObjects.Select(sceneObject => new GameStateData.LockedSceneObject
                {
                    Id = sceneObject.Id, UsedInSceneLogic = sceneObject.UsedInSceneLogic
                }).ToList();

            GameStateData.UpdateSceneObjectLockedIds(lockedObjects);
        }
    }
}