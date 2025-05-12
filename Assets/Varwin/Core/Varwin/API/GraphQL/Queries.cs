using System.Collections.Generic;
using Varwin.Data.ServerData;
using Varwin.WWW;

namespace Varwin.GraphQL
{
    public static class Queries
    {
        public static GraphQLQuery GetSceneObjectsBehaviourQuery(int projectId)
        {
            return new GraphQLQuery
            {
                Query = GraphQLSchema.GetObjectsBehaviour,
                Variables = new ()
                {
                    {"workspaceId", Request.WorkspaceId},
                    {"projectIds", new[] {projectId} }
                },
                Type = GraphRequestType.Query
            };
        }

        public static GraphQLQuery GetLibraryObjectsQuery(int sceneId, bool onlyMobile = false, int limit = 25, string after = null, string search = "", string[] rootGuids = null)
        {
            return new GraphQLQuery
            {
                Query = GraphQLSchema.GetObjects,
                Variables = new Dictionary<string, object>
                {
                    {"workspaceId", Request.WorkspaceId},
                    {"first", limit},
                    {"after", string.IsNullOrEmpty(after) ? null : after},
                    {"search", search},
                    {"mobileReady", onlyMobile ? (bool?) true : null},
                    {"rootGuids", rootGuids},
                    {"sceneId", sceneId}
                },
                Type = GraphRequestType.Query
            };
        }
        
        public static GraphQLQuery GetLibraryObjectsCountQuery()
        {
            return new GraphQLQuery
            {
                Query = GraphQLSchema.GetObjectsCount,
                Variables = new Dictionary<string, object>
                {
                    {"workspaceId", Request.WorkspaceId}
                },
                Type = GraphRequestType.Query
            };
        }

        public static GraphQLQuery GetResourcesQuery(int limit = 25, string after = null, string search = "", ResourceRequestType resourceRequestType = ResourceRequestType.All)
        {
            return new GraphQLQuery
            {
                Query = GraphQLSchema.GetResources,
                Variables = new Dictionary<string, object>
                {
                    {"workspaceId", Request.WorkspaceId},
                    {"first", limit},
                    {"after", string.IsNullOrEmpty(after) ? null : after},
                    {"search", search},
                    {"formats", resourceRequestType.ToSearchArray()}
                },
                Type = GraphRequestType.Query
            };
        }

        public static GraphQLQuery GetProjectsQuery(int limit = 25, bool onlyMobile = true, string after = null, string search = "", int offset = 0)
        {
            return new GraphQLQuery
            {
                Query = GraphQLSchema.GetProjects,
                Variables = new Dictionary<string, object>
                {
                    {"workspaceId", Request.WorkspaceId},
                    {"limit", limit},
                    {"mobileReady", onlyMobile},
                    {"after", after},
                    {"search", search},
                    {"offset", offset}
                },
                Type = GraphRequestType.Query
            };
        }

        public static GraphQLQuery GetProjectObjectVersions(int projectId, int workspaceId)
        {
            return new GraphQLQuery
            {
                Query = GraphQLSchema.GetProjectObjectVersions,
                Variables = new Dictionary<string, object>
                {
                    {"ids", new[] {projectId}},
                    {"workspaceId", workspaceId}
                },
                Type = GraphRequestType.Query
            };
        }

        public static GraphQLQuery GetWorkspacesQuery(int limit = 10, int offset = 0)
        {
            return new GraphQLQuery
            {
                Query = GraphQLSchema.GetWorkspaces,
                Type = GraphRequestType.Query,
                Variables = new Dictionary<string, object>()
                {
                    {"offset", offset},
                    {"limit", limit}
                }
            };
        }
        
        public static GraphQLQuery GetServerInfoQuery()
        {
            return new GraphQLQuery {Query = GraphQLSchema.GetServerInfo, Type = GraphRequestType.Query};
        }

        public static GraphQLQuery GetProjectMetaQuery(int id)
        {
            return new GraphQLQuery
            {
                Query = GraphQLSchema.GetProjectMeta,
                Variables = new Dictionary<string, object> {{"id", id}},
                Type = GraphRequestType.Query
            };
        }
    }
}