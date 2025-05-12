namespace Varwin.GraphQL
{
    public static class GraphQLSchema
    {
        #region Mutations

        // language=gql
        public const string UpdateSceneObjects = @"
mutation UpdateSceneObjects($sceneData: UpdateSceneObjectsInput!)
{
    updateSceneObjects(input: $sceneData)
    {
        type,
        scene
        {
            sceneObjects
            {
                id
                instanceId
            }
        }
    }
}";

        // language=gql
        public const string RefreshSession = @"
mutation refreshSession($refreshToken: String!)
{
    refreshSession(input:{refreshToken: $refreshToken})
    {
        accessToken,
        refreshToken
    }
}";

        // language=gql
        public const string Login = @"
mutation login($login : String!, $password : String!, $clientInfo : String!) 
{
    login(input: 
    {
        login: $login
        password: $password
        clientInfo: $clientInfo
    }) 
    {
        accessToken
        refreshToken
        user
        {
            login
            lastActivityAt
            preferences
            {
                lang
            }
        }
    }
}";

        // language=gql
        public const string LoginAsDefaultUser = @"
mutation loginAsDefaultUser($clientInfo : String!) 
{
    loginAsDefaultUser(input: 
    {
        clientInfo: $clientInfo
    }) 
    {
        accessToken
        refreshToken
        user
        {
            login
            lastActivityAt
            preferences
            {
                lang
            }
        }
    }
}";

        // language=gql
        public const string Logout = @"
mutation logout($refreshToken :String!) 
{
    logout (    
        input: {
            refreshToken: $refreshToken
        })
}";

        // language=gql
        public const string CreateExportProjectTask = @"
mutation createExportProjectTask($projectId : ID!, $guid : GUID!, $workspaceId : ID!)
{
    createExportProjectTask(input: 
    {
        projectId: $projectId,
        format: zip
        data:{}
        guid: $guid
        workspaceId : $workspaceId
    }) 
    {
        createdAt
        progress
        status
        statusLabel
        ... on ExportProjectTask 
        {
            downloadResultUrl
        }
    }
 }";

        #endregion Mutations

        #region Queries

        // language=gql
        public const string GetProjectObjectVersions = @"
query projects($workspaceId: ID!, $ids: [ID!]) {
  projects(workspaceId: $workspaceId, ids: $ids) {
    edges {
      node {
        scenes {
            id
            sceneObjects {
              objectId
            }
          }
        objects {
            id
            guid
            versions {
              builtAt
              guid
              id
            }
        }
      }
    }
  }
}
";

        // language=gql
        public const string GetProjectMeta = @"
query GetProjectMeta($id: ID!)
{
    projectMeta(id: $id)
}";

        // language=gql
        public const string GetServerInfo = @"
query GetServerInfo
{
    serverInfo
    {
        appVersion
        remoteAddr
        remoteAddrPort
        macAddr
        defaultUserAuthorizationAllowed
        appLicenseInfo
	    {
		    guid
		    expiresAt
		    firstName
		    lastName
		    email
		    company
		    edition
		}
    }
}";

        // language=gql
        public const string GetObjects = @"
query GetObjects($workspaceId: ID!, $first: Int, $after: String, $search: String, $mobileReady: Boolean, $rootGuids: [GUID!], $sceneId: ID!)
{
    objects(workspaceId: $workspaceId, first: $first, after: $after, search: $search, mobileReady: $mobileReady, rootGuids: $rootGuids, sceneId: $sceneId)
    {
        pageInfo
        {
            endCursor
        }
        edges
        {
            node
            {
                id
                guid
                rootGuid
                name
                {
                    en
                    ru
                    cn
                    ko
                    kk
                }
                embedded
                config
                linuxReady
                assets
                paid
                packages
                {
                    id
                }
                builtAt
            }
        }
    }
}";

        // language=gql
        public const string GetObjectsCount = @"
query GetObjectsCount($workspaceId: ID!)
{
    objects(workspaceId: $workspaceId)
    {
        totalCount
    }
}";

        // language=gql
        public const string GetResources = @"
query GetResources($workspaceId: ID!, $first: Int, $after: String, $search: String, $formats: [String!])
{
    resources(workspaceId: $workspaceId, first: $first, after: $after, search: $search, formats: $formats)
    {
        pageInfo
        {
            endCursor
        }
        edges
        {
            node
            {
                id
                guid
                rootGuid
                name
                {
                    en
                    ru
                    cn
                    ko
                    kk
                }
                format
                assets
            }
        }
    }
}";

        // language=gql
        public const string GetWorkspaces = @"query membership($limit: Int, $offset: Int) {
    workspaceMembership (offset: $offset, first: $limit)
    {
        totalCount
        edges {
            node {
                id
                createdAt
                updatedAt
                blockedAt
                name
                state
                blockReason
            }
        }
    }
}";

        //language=gql
        public const string GetObjectsBehaviour = @"
query GetObjectsBehaviour($workspaceId: ID!, $projectIds: [ID!])
{
    projects(workspaceId: $workspaceId, ids: $projectIds)
    {
        edges
        {
            node
            {
                scenes 
                {
                    id,
                    objectBehaviours 
                    {
                        objectId,
                        behaviours
                    }
                }
            }
        }
    }
}
 ";

        //language=gql
        public const string GetProjects = @"
query GetProjects($workspaceId: ID!, $limit: Int, $after: String, $mobileReady: Boolean, $search: String, $offset: Int)
{
    projects(workspaceId: $workspaceId, mobileReady : $mobileReady, first: $limit, after: $after, search: $search, offset: $offset)
    {
        totalCount
        edges
        {
            cursor
            node
            {
                id
                createdAt
                updatedAt
                guid
                name
                mobileReady
                multiplayer
                scenes
                {
                    id
                }
                configurations
                {
                    id
                    name
                    sid
                    project
                    {
                        id
                    }
                    startScene
                    {
                        id
                    }
                    loadingSceneTemplate
                    {
                        id
                    }
                    createdAt
                    updatedAt
                    lang
                    platformMode
                    disablePlatformModeSwitching
                }
            }
        }
    }
}";

        #endregion Queries

        #region Subscriptions

        //language=gql
        public const string RestoreSceneBackupQuery = @"
        subscription ProjectChanged($projectIds: [ID!])
        {
            projectsChanged(input:
            {
                projectIds: $projectIds,
                operationTypes: [restoreSceneBackup] 
            })
            {
                projectId,
                ... on SceneOperationResult
                {
                    sceneId
                }
            }
        }
";

        // language=gql
        public const string LibraryChangedQuery = @"
subscription LibraryChanged
{
    libraryChanged(input:
    {
        operationTypes:[create, update, delete]
    })
    {
        libraryItem
        {
            name
            {
                en
                ru
                cn
                ko
                kk
            }
            ... on Object
            {
                mobileReady
                packages
                {
                    id
                }
            }
        }
        libraryItemType
        type
        id
    }
}";

        // language=gql
        public const string ProjectsEditorLogicChangedQuery = @"
subscription ProjectsEditorLogicChanged($projectIds: [ID!])
{
    projectsChanged(input:
    {
        projectIds: $projectIds,
        operationTypes:[updateSceneLogicEditorData, updateSceneLogic]
    })
    {
        projectId,
        ... on SceneOperationResult
        {
            sceneId,
            scene
            {
                sceneObjects
                {
                    id,
                    usedInSceneLogic
                }
            }
        }
    }
}";

        // language=gql
        public const string ProjectsChangedQuery = @"
subscription ProjectsChanged($projectIds: [ID!])
{
    projectsChanged(input:
    {
        projectIds: $projectIds,
        operationTypes:[
            delete,
            updateSettings,
            updateSceneSettings,
            createScene,
            renameScene,
            deleteScene,
            updateSceneObjects,
            replaceLibraryItems]
    })
    {
        projectId,
        type,
        project
        {
            mobileReady
        }
        ... on SceneOperationResult
        {
            sceneId,
            scene
            {
                name
                sceneTemplateId
            }
        }
    }
}";

        // language=gql
        public const string TasksChanged = @"
subscription taskChanged($guid : GUID!) 
{
    taskChanged(input: 
    {
        guid: $guid
    }) 
    {
        status
        statusLabel
        progress
        errorDetails
        ... on ExportProjectTask 
            {
                downloadResultUrl
            }
        }
}";

        #endregion Subscriptions
    }
}