using System.Collections.Generic;
using Varwin.Data.ServerData;

namespace Varwin.GraphQL
{
    public static class Mutations
    {
        public static GraphQLQuery GetUpdateSceneObjectsMutation(SceneTemplateObjectsDto sceneData)
        {
            return new GraphQLQuery
            {
                Query = GraphQLSchema.UpdateSceneObjects,
                Variables = new Dictionary<string, object>
                {
                    {"sceneData", sceneData}
                },
                Type = GraphRequestType.Mutation
            };
        }

        public static GraphQLQuery GetRefreshSessionMutation(string refreshToken)
        {
            return new GraphQLQuery
            {
                Query = GraphQLSchema.RefreshSession,
                Variables = new Dictionary<string, object>
                {
                    {"refreshToken", refreshToken}
                },
                Type = GraphRequestType.Mutation
            };
        }

        public static GraphQLQuery GetLoginMutation(string login, string password, string clientInfo)
        {
            return new GraphQLQuery
            {
                Query = GraphQLSchema.Login,
                Variables = new Dictionary<string, object>
                {
                    {"login", login},
                    {"password", password},
                    {"clientInfo", clientInfo}
                },
                Type = GraphRequestType.Mutation
            };
        }
        
        public static GraphQLQuery GetLoginAsDefaultUserMutation(string clientInfo)
        {
            return new GraphQLQuery
            {
                Query = GraphQLSchema.LoginAsDefaultUser,
                Variables = new Dictionary<string, object>
                {
                    {"clientInfo", clientInfo}
                },
                Type = GraphRequestType.Mutation
            };
        }

        public static GraphQLQuery GetLogoutMutation(string refreshToken)
        {
            return new GraphQLQuery
            {
                Query = GraphQLSchema.Logout,
                Variables = new Dictionary<string, object>
                {
                    {"refreshToken", refreshToken}
                },
                Type = GraphRequestType.Mutation
            };
        }

        public static GraphQLQuery GetCreateExportProjectTaskMutation(int projectId, string guid, int workspaceId)
        {
            return new GraphQLQuery
            {
                Query = GraphQLSchema.CreateExportProjectTask,
                Variables = new Dictionary<string, object>
                {
                    {"projectId", projectId},
                    {"guid", guid},
                    {"workspaceId", workspaceId}
                },
                Type = GraphRequestType.Mutation
            };
        }
    }
}