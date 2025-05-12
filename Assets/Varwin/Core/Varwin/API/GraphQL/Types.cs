// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnassignedField.Global

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Varwin.Data;
using Varwin.Data.ServerData;

namespace Varwin.GraphQL
{
    public static class Types
    {
        public class LocalizedString
        {
            public string En;
            public string Ru;
        }

        // libraryChanged
        public class LibraryChangedInfo : IJsonSerializable
        {
            public class LibraryChangedJsonPayload
            {
                public LibraryChangedJsonData Data;
            }

            public class LibraryChangedJsonData
            {
                public LibraryItemOperationResult[] LibraryChanged;
            }

            public enum LibraryOperationType
            {
                Create,
                Update,
                Delete
            }

            public enum LibraryItemType
            {
                Object,
                Resource,
                SceneTemplate,
                Package,
                ProjectTemplate
            }

            public class LibraryItem
            {
                public LocalizedString Name;
                public bool MobileReady;
                public Package Package;
            }

            public class Package
            {
                public int? Id;
            }

            public class LibraryItemOperationResult
            {
                public LibraryOperationType? Type;
                public LibraryItemType? LibraryItemType;
                public int Id;
                public LibraryItem LibraryItem;
            }

            public LibraryChangedJsonPayload Payload;
        }

        // projectMeta
        public class ProjectStructureJsonInfo : IJsonSerializable
        {
            public class ProjectStructureJsonData
            {
                public ProjectStructure ProjectMeta;
            }

            public ProjectStructureJsonData Data;
        }

        // projectsChanged
        public class ProjectsChangedInfo : IJsonSerializable
        {
            public class SceneObject
            {
                public int Id;
                public bool UsedInSceneLogic;
            }

            public class Scene
            {
                public string Name;
                public SceneObject[] SceneObjects;
                public int SceneTemplateId;
            }

            public enum ProjectChangeType
            {
                Delete,
                UpdateSettings,
                UpdateSceneSettings,
                CreateScene,
                RenameScene,
                DeleteScene,
                UpdateSceneLogic,
                UpdateSceneObjects,
                ReplaceLibraryItems
                
            }

            public class Project
            {
                public bool MobileReady;
            }

            public class ChangedProject
            {
                public int ProjectId { get; set; }

                [JsonConverter(typeof(StringEnumConverter))]
                public ProjectChangeType Type { get; set; }

                public Project Project { get; set; }

                public int SceneId { get; set; }
                public Scene Scene { get; set; }
            }

            public class ProjectsChangedData
            {
                public ChangedProject[] ProjectsChanged;
            }

            public class ProjectsChangedPayload
            {
                public ProjectsChangedData Data;
            }

            public ProjectsChangedPayload Payload;
        }

        // loginResponse
        public class LoginResponse : IJsonSerializable
        {
            public LoginPayload data;

            public class User
            {
                public string login;
                public string lastActivityAt;
                public Preferences preferences;
            }

            public class Login
            {
                public string accessToken;
                public string refreshToken;
                public User user;
            }

            public class LoginPayload
            {
                public Login login;
                public Login loginAsDefaultUser;
            }

            public class Preferences
            {
                public string lang;
            }
        }

        public class LogoutResponse : IJsonSerializable
        {
            public LogoutPayload data;

            public class LogoutPayload
            {
                public bool logout;
            }
        }

        // tokens
        public class TokensInfo : IJsonSerializable
        {
            public class RefreshSession
            {
                public string AccessToken;
                public string RefreshToken;
            }

            public class TokensData
            {
                public RefreshSession RefreshSession;
            }

            public TokensData Data;
        }

        public class TokenPayload : IJsonSerializable
        {
            public int UserId;
            public int Exp; // Unix timestamp
        }

        public class WorkspacesResponse : IJsonSerializable
        {
            public Payload data;

            public class Payload
            {
                public WorkspaceMembership workspaceMembership;
            }

            public enum WorkspaceState
            {
                active,
                blocked
            }

            public class WorkspaceMembership
            {
                public int totalCount;
                public WorkspaceEdge[] edges;
            }

            public class WorkspaceEdge
            {
                public Workspace node;
            }

            public class Workspace
            {
                public int id;
                public DateTime createdAt;
                public DateTime updatedAt;
                public DateTime? blockedAt;
                public string blockReason;
                public string name;
                public WorkspaceState state;
            }
        }

        // projectsRequest
        public class ProjectsResponse : IJsonSerializable
        {
            public ProjectsPayload data;

            public class ProjectsPayload
            {
                public ProjectsList projects;
            }

            public class ProjectsList
            {
                public int totalCount;
                public ProjectContainer[] edges;
            }

            public class ProjectContainer
            {
                public Project node;
            }

            public class Project
            {
                public int id;
                public DateTime createdAt;
                public DateTime updatedAt;
                public string guid;
                public string name;
                public bool mobileReady;
                public bool multiplayer;
                public IdHolder[] scenes;
                public ProjectConfiguration[] configurations;
                public string cursor;
            }

            public class ProjectConfiguration
            {
                public int id;
                public string name;
                public string sid;
                public IdHolder project;
                public IdHolder startScene;
                public IdHolder loadingSceneTemplate;
                public string createdAt;
                public string updatedAt;
                public string lang;

                [JsonConverter(typeof(StringEnumConverter))]
                public PlatformMode platformMode;

                public bool disablePlatformModeSwitching;
            }

            public class IdHolder
            {
                public int id;
            }
        }

        public class ProjectObjectVersions : IJsonSerializable
        {
            public ProjectsPayload data;

            public class ProjectsPayload
            {
                public ProjectsList projects;
            }

            public class ProjectsList
            {
                public ProjectContainer[] edges;
            }

            public class ProjectContainer
            {
                public Project node;
            }

            public class Project
            {
                public Scene[] scenes;
                public SceneObject[] objects;
            }


            public class Scene
            {
                public int id;
                public SceneObjectReference[] sceneObjects;
            }

            public class SceneObjectReference
            {
                public int objectId;
            }

            public class SceneObject
            {
                public int id;
                public string guid;
                public SceneObjectVersion[] versions;
            }

            public class SceneObjectVersion
            {
                public DateTime builtAt;
                public string guid;
                public int id;
            }
        }

        // createTask
        public class CreateTaskResponse : IJsonSerializable
        {
            public class CreateTaskResponsePayload
            {
                public TaskInfoContainer data;
            }

            public class TaskInfoContainer
            {
                public TaskInfo createExportProjectTask;
            }

            public class TaskInfo
            {
                public DateTime createdAt;
                public float progress;
                public string status;
                public string statusLabel;
                public string downloadResultUrl;
            }
        }

        // taskSubscription
        public class TaskChangedResponse : IJsonSerializable
        {
            public TaskChangedPayload payload;
            public string id;
            public string type;

            public class TaskChangedPayload
            {
                public TaskChangedInfoContainer data;
            }

            public class TaskChangedInfoContainer
            {
                public TaskChangedInfo taskChanged;
            }

            public class TaskChangedInfo
            {
                public string status;
                public string statusLabel;
                public float progress;
                public string errorDetails;
                public string downloadResultUrl;
            }
        }

        public class ServerInfo : IJsonSerializable
        {
            public class Data
            {
                public ServerConfig serverInfo;
            }

            public Data data;
        }

        public class ProjectBackupSubscriptionResponse
        {
            public Payload payload { get; set; }
            public string id { get; set; }
            public string type { get; set; }

            public class Data
            {
                public List<ProjectsChanged> projectsChanged { get; set; }
            }

            public class Payload
            {
                public Data data { get; set; }
            }

            public class ProjectsChanged
            {
                public int projectId { get; set; }
                public int sceneId { get; set; }
            }
        }

        public class SceneBehavioursQueryResponse
        {
            public class Data
            {
                public Projects projects { get; set; }
            }

            public class Edge
            {
                public Node node { get; set; }
            }

            public class Node
            {
                public List<Scene> scenes { get; set; }
            }

            public class ObjectBehaviour
            {
                public int objectId { get; set; }
                public List<string> behaviours { get; set; }
            }

            public class Projects
            {
                public List<Edge> edges { get; set; }
            }

            public class Root : IJsonSerializable
            {
                public Data data { get; set; }
            }

            public class Scene
            {
                public int id { get; set; }
                public List<ObjectBehaviour> objectBehaviours { get; set; }
            }
        }
    }
}