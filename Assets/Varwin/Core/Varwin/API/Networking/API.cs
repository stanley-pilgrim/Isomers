using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Varwin.Data;
using Varwin.Data.ServerData;
using Varwin.GraphQL;
using Varwin.Log;
using Varwin.WWW.Models;
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace Varwin.WWW
{
    public static class API
    {
        private static Task _accessTokenExpireTask;
        private static CancellationToken _taskCancellationToken;
        private static CancellationTokenSource TaskCancellationTokenSource;
        private static readonly DateTime UnixEpochStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        private const int SessionRefreshOverrunMinutes = 5;

        [Obsolete]
        public static void GetServerConfig(Action<ServerConfig> onFinish = null, Action<string> onError = null)
        {
            GetServerConfig(onFinish, onError: (error, code) => onError?.Invoke(error));
        }

        public static void GetServerConfig(
            Action<ServerConfig> onFinish = null,
            Action<string, long> onError = null,
            Action<string, long> onConnectionError = null, 
            Action<string> onParseServerInfoError = null
        )
        {
            GraphQLQuery query = Queries.GetServerInfoQuery();
            var requestGraph = new RequestGraph(query);

            requestGraph.OnFinish = response =>
            {
                var responseGraph = (ResponseGraph) response;
                ServerConfig result;

                try
                {
                    string json = responseGraph.Data;
                    result = json.JsonDeserialize<GraphQL.Types.ServerInfo>().data.serverInfo;
                }
                catch (Exception e)
                {
                    Debug.LogError(RequestGraph.GetResponseErrorMessage(requestGraph, responseGraph)
                                 + Environment.NewLine
                                 + $"Exception = {e}");
                    result = null;
                    onParseServerInfoError?.Invoke(e.ToString());
                    return;
                }

                onFinish?.Invoke(result);
            };

            requestGraph.OnError = message =>
            {
                var error = $"{message}\n\n{query.Query}";

                switch (requestGraph.ErrorStatus)
                {
                    case RequestGraph.RequestErrorStatus.Unknown:
                        onError?.Invoke(error, requestGraph.RequestResponseCode);
                        break;
                    case RequestGraph.RequestErrorStatus.ConnectionError:
                        onConnectionError?.Invoke(error, requestGraph.RequestResponseCode);
                        break;
                    case RequestGraph.RequestErrorStatus.GraphQLError:
                        onParseServerInfoError?.Invoke(error);
                        break;
                    default:
                        onError?.Invoke(error, requestGraph.RequestResponseCode);
                        break;
                }

                Debug.LogError($"Can't get server config: {error}");
            };
        }

        public static void GetResources(string search = "", int limit = 50, string after = null, ResourceRequestType requestType = ResourceRequestType.All,
            Action<List<ResourceDto>> callback = null, Action<string> endCursorCallback = null)
        {
            GraphQLQuery query = Queries.GetResourcesQuery(limit, after, search, requestType);
            var requestGraph = new RequestGraph(query);

            requestGraph.OnFinish = response =>
            {
                var responseGraph = (ResponseGraph) response;
                List<ResourceDto> result;

                try
                {
                    string json = responseGraph.Data;
                    var definition = new
                    {
                        data = new
                        {
                            resources = new {pageInfo = new {endCursor = ""}, edges = new List<ResourceDtoContainer>()}
                        }
                    };
                    var edges = JsonConvert.DeserializeAnonymousType(json, definition).data.resources.edges;
                    result = edges.Select(x => x.Node).ToList();

                    endCursorCallback?.Invoke(JsonConvert.DeserializeAnonymousType(json, definition).data.resources
                        .pageInfo.endCursor);
                }
                catch (Exception e)
                {
                    Debug.LogError(RequestGraph.GetResponseErrorMessage(requestGraph, responseGraph)
                                   + Environment.NewLine
                                   + $"Exception = {e}");
                    result = null;
                }

                callback?.Invoke(result);
            };

            requestGraph.OnError = message =>
            {
                Debug.LogError($"Can't get resources list: {message}");
                callback?.Invoke(new List<ResourceDto>());
            };
        }

        public static void GetResourceByGuid(string guid, Action<ResourceDto> callback = null)
        {
            GetResources(
                guid,
                1,
                null,
                ResourceRequestType.All,
                results => callback?.Invoke(results.FirstOrDefault())
            );
        }

        public static void GetLibraryObjects(string search = "", bool onlyMobile = false, Action<List<PrefabObject>> callback = null, int limit = 50, string after = null,
            string[] rootGuids = null)
        {
            GraphQLQuery query = Queries.GetLibraryObjectsQuery(ProjectData.SceneId, onlyMobile, limit, after, search, rootGuids);
            var requestGraph = new RequestGraph(query);

            requestGraph.OnFinish = response =>
            {
                var responseGraph = (ResponseGraph) response;
                List<PrefabObject> result;

                try
                {
                    string json = responseGraph.Data;
                    var definition = new
                    {
                        data = new
                        {
                            objects = new
                            {
                                pageInfo = new
                                {
                                    endCursor = ""
                                },
                                edges = new List<PrefabObjectContainer>()
                            }
                        }
                    };

                    var deserializedData = JsonConvert.DeserializeAnonymousType(json, definition).data.objects;
                    List<PrefabObjectContainer> edges = deserializedData.edges;
                    result = edges.Select(x => x.Node).ToList();

                    PrefabObject.EndCursor = deserializedData.pageInfo.endCursor;
                }
                catch (Exception e)
                {
                    Debug.LogError(RequestGraph.GetResponseErrorMessage(requestGraph, responseGraph)
                                   + Environment.NewLine
                                   + $"Exception = {e}");
                    result = null;
                }

                callback?.Invoke(result);
            };

            requestGraph.OnError = message =>
            {
                Debug.LogError($"Can't get objects list: {message}");
                callback?.Invoke(new List<PrefabObject>());
            };
        }

        public static void GetLibraryObjectsCount(Action<int> callback = null)
        {
            GraphQLQuery query = Queries.GetLibraryObjectsCountQuery();
            var requestGraph = new RequestGraph(query);
            var result = 0;

            requestGraph.OnFinish = response =>
            {
                var responseGraph = (ResponseGraph) response;

                try
                {
                    string json = responseGraph.Data;
                    var definition = new
                    {
                        data = new
                        {
                            objects = new
                            {
                                totalCount = 0
                            }
                        }
                    };

                    var deserializedData = JsonConvert.DeserializeAnonymousType(json, definition).data.objects;
                    result = deserializedData.totalCount;
                }
                catch (Exception e)
                {
                    Debug.LogError(RequestGraph.GetResponseErrorMessage(requestGraph, responseGraph)
                                 + Environment.NewLine
                                 + $"Exception = {e}");
                }

                callback?.Invoke(result);
            };

            requestGraph.OnError = message => { Debug.LogError($"Can't get object count: {message}"); };
        }

        public static void GetProjectMeta(int projectId, Action<ProjectStructure> callback)
        {
            GraphQLQuery query = Queries.GetProjectMetaQuery(projectId);
            var requestGraph = new RequestGraph(query);

            requestGraph.OnFinish = response =>
            {
                var responseGraph = (ResponseGraph) response;
                ProjectStructure result = null;
                try
                {
                    var info = responseGraph.Data.JsonDeserialize<GraphQL.Types.ProjectStructureJsonInfo>();
                    result = info.Data.ProjectMeta;
                }
                catch (Exception e)
                {
                    Debug.LogError(RequestGraph.GetResponseErrorMessage(requestGraph, responseGraph)
                                   + Environment.NewLine
                                   + $"Exception = {e}");
                }

                callback?.Invoke(result);
            };

            requestGraph.OnError = message => { Debug.LogError($"Can't get project meta: {message}"); };
        }

        public static void GetWorkspaces(Action<WorkspacesList> onFinish, Action<string> onError, int limit = 25, int offset = 0)
        {
            GraphQLQuery query = Queries.GetWorkspacesQuery(limit, offset);
            var request = new RequestGraph(query);

            request.OnFinish += response =>
            {
                var responseGraph = response as ResponseGraph;

                try
                {
                    var workspacesResponse = responseGraph?.Data.JsonDeserialize<GraphQL.Types.WorkspacesResponse>();
                    var workspaceList = workspacesResponse.data.workspaceMembership.edges.Select(a => a.node).ToArray();

                    WorkspacesList resultWorkspaceItems = new WorkspacesList();

                    for (int i = 0; i < workspaceList.Count(); i++)
                    {
                        var workspaceItem = new WorkspaceItem();
                        workspaceItem.Id = workspaceList[i].id;
                        workspaceItem.Name = workspaceList[i].name;
                        workspaceItem.UpdatedAt = workspaceList[i].updatedAt;
                        resultWorkspaceItems.Edges.Add(workspaceItem);
                    }

                    resultWorkspaceItems.TotalCount = workspacesResponse.data.workspaceMembership.totalCount;
                    onFinish?.Invoke(resultWorkspaceItems);
                }
                catch (Exception e)
                {
                    onError?.Invoke(e.Message);
                }
            };
        }

        public static void GetProjects(Action<ProjectsList> onFinish, Action<string> onError, int limit = 25, bool onlyMobile = true, string after = null, string search = "", int offset = 0)
        {
            var query = Queries.GetProjectsQuery(limit, onlyMobile, after, search, offset);
            var requestGraph = new RequestGraph(query);

            requestGraph.OnFinish += response =>
            {
                var responseGraph = response as ResponseGraph;

                try
                {
                    var projectsResponse = responseGraph?.Data.JsonDeserialize<GraphQL.Types.ProjectsResponse>();
                    var sourceProjectsList = projectsResponse.data.projects;

                    var targetProjectsList = new ProjectsList
                    {
                        Edges = new List<ProjectItem>(), 
                        TotalCount = sourceProjectsList.totalCount
                    };

                    foreach (var edge in sourceProjectsList.edges)
                    {
                        var projectData = edge.node;
                        var confData = projectData.configurations;

                        var projectConfigurations = confData.Select(t => new ProjectConfiguration
                        {
                            Id = t.id,
                            Name = t.name,
                            Sid = t.sid,
                            StartSceneId = t.startScene?.id,
                            LoadingSceneTemplateId = t.loadingSceneTemplate?.id,
                            CreatedAt = t.createdAt,
                            UpdatedAt = t.updatedAt,
                            Lang = t.lang,
                            PlatformMode = t.platformMode,
                            DisablePlatformModeSwitching = t.disablePlatformModeSwitching
                        }).ToList();

                        targetProjectsList.Edges.Add(new ProjectItem
                        {
                            Id = projectData.id,
                            Name = projectData.name,
                            Guid = projectData.guid,
                            SceneCount = projectData.scenes.Length,
                            Configurations = projectConfigurations,
                            CreatedAt = projectData.createdAt,
                            UpdatedAt = projectData.updatedAt,
                            Multiplayer = projectData.multiplayer,
                            ProjectPath = null,
                            Cursor = projectData.cursor
                        });
                    }
                    
                    onFinish?.Invoke(targetProjectsList);
                }
                catch (Exception e)
                {
                    onError?.Invoke(e.Message);
                }
            };

            requestGraph.OnError += response =>
            {
                onError?.Invoke(response);
            };
        }

        /// <summary>
        /// Получить сохраненные на сервере поведения.
        /// </summary>
        /// <param name="onBehavioursDiffCalculated">Список объектов, у которых локальные и серверные поведения отличаются.</param>
        public static void GetObjectsBehaviours(Action<List<int>> onBehavioursDiffCalculated)
        {
            var query = Queries.GetSceneObjectsBehaviourQuery(ProjectData.ProjectId);
            var requestGraph = new RequestGraph(query);

            requestGraph.OnFinish += response =>
            {
                var responseGraph = response as ResponseGraph;

                try
                {
                    var dto = responseGraph?.Data.JsonDeserialize<GraphQL.Types.SceneBehavioursQueryResponse.Root>();
                    if (dto == null || dto.data.projects.edges.Count == 0)
                    {
                        throw new Exception("Failed to load object behaviours because there is no projects in DTO");
                    }

                    var currentScene = dto.data.projects.edges
                        .FirstOrDefault(edge => edge.node.scenes.Any(scene => scene.id == ProjectData.SceneId))?.node.scenes
                        .FirstOrDefault(scene => scene.id == ProjectData.SceneId);

                    if (currentScene == null)
                    {
                        throw new NullReferenceException("Failed to load object versions because current scene is not found in query response");
                    }

                    if (currentScene.objectBehaviours == null)
                    {
                        throw new NullReferenceException($"Failed to compare client and server behaviours. Server behaviours for scene {currentScene.id} is empty");
                    }

                    var objectsDiff = BehavioursHelper.GetServerBehavioursDiff(currentScene.objectBehaviours);

                    onBehavioursDiffCalculated?.Invoke(objectsDiff);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    throw;
                }
            };
        }

        public static void IsProjectObjectsHasUpdates(int projectId, int workspaceId, Action<bool> onFinish, Action<string> onError)
        {
            var query = Queries.GetProjectObjectVersions(projectId, workspaceId);
            var requestGraph = new RequestGraph(query);

            requestGraph.OnFinish += response =>
            {
                var responseGraph = response as ResponseGraph;

                try
                {
                    var dto = responseGraph?.Data.JsonDeserialize<GraphQL.Types.ProjectObjectVersions>();

                    if (dto == null || dto.data.projects.edges.Length == 0)
                    {
                        throw new Exception("Failed to load object versions because there is no projects in DTO");
                    }

                    var node = dto.data.projects.edges[0].node;
                    var currentScene = node.scenes.FirstOrDefault(scene => scene.id == ProjectData.SceneId);

                    if (currentScene == null)
                    {
                        throw new Exception("Failed to load object versions because current scene is not found in query response");
                    }

                    var hasUpdates = currentScene.sceneObjects.Any(sceneObject => CheckObjectHasNewVersion(sceneObject.objectId, node.objects));

                    onFinish?.Invoke(hasUpdates);
                }
                catch (Exception e)
                {
                    onError?.Invoke(e.Message);
                }
            };
        }

        private static bool CheckObjectHasNewVersion(int objectId, GraphQL.Types.ProjectObjectVersions.SceneObject[] sceneObjects)
        {
            foreach (var sceneObject in sceneObjects)
            {
                if (sceneObject.id != objectId)
                {
                    continue;
                }

                var currentGuid = sceneObject.guid;
                var currentVersion = default(DateTime);
                var lastVersion = default(DateTime);

                if (sceneObject.versions.Length < 2)
                {
                    return false;
                }

                foreach (var version in sceneObject.versions)
                {
                    if (version.builtAt > lastVersion)
                    {
                        lastVersion = version.builtAt;
                    }

                    if (version.guid == currentGuid)
                    {
                        currentVersion = version.builtAt;
                    }
                }

                return currentVersion < lastVersion;
            }

            return false;
        }

        public static void Login(string login, string password, string clientInfo, Action<GraphQL.Types.LoginResponse> onAuthorised = null, Action onError = null)
        {
            GraphQLQuery mutation = Mutations.GetLoginMutation(login, password, clientInfo);
            var requestGraph = new RequestGraph(mutation, false);

            requestGraph.OnFinish += response =>
            {
                var responseGraph = response as ResponseGraph;

                try
                {
                    var loginResponse = responseGraph?.Data.JsonDeserialize<GraphQL.Types.LoginResponse>();
                    onAuthorised?.Invoke(loginResponse);
                }
                catch (Exception e)
                {
                    onError?.Invoke();
                }
            };

            requestGraph.OnError += s => onError?.Invoke();
        }

        public static void LoginAsDefaultUser(string clientInfo, Action<GraphQL.Types.LoginResponse> onAuthorised = null, Action onError = null)
        {
            GraphQLQuery mutation = Mutations.GetLoginAsDefaultUserMutation(clientInfo);
            var requestGraph = new RequestGraph(mutation, false);

            requestGraph.OnFinish += response =>
            {
                var responseGraph = response as ResponseGraph;

                try
                {
                    var loginResponse = responseGraph?.Data.JsonDeserialize<GraphQL.Types.LoginResponse>();
                    onAuthorised?.Invoke(loginResponse);
                }
                catch (Exception e)
                {
                    onError?.Invoke();
                }
            };

            requestGraph.OnError += s => onError?.Invoke();
        }
        
        public static void Logout(string refreshToken, Action<GraphQL.Types.LogoutResponse> onFinish = null)
        {
            GraphQLQuery mutation = Mutations.GetLogoutMutation(refreshToken);
            var requestGraph = new RequestGraph(mutation, false);

            requestGraph.OnFinish += response =>
            {
                var responseGraph = response as ResponseGraph;

                try
                {
                    var logoutResponse = responseGraph?.Data.JsonDeserialize<GraphQL.Types.LogoutResponse>();
                    onFinish?.Invoke(logoutResponse);
                }
                catch (Exception e)
                {
                    var logoutResponse = new GraphQL.Types.LogoutResponse.LogoutPayload {logout = false};
                    onFinish?.Invoke(new GraphQL.Types.LogoutResponse {data = logoutResponse});
                }
            };

            requestGraph.OnError += s =>
            {
                var logoutResponse = new GraphQL.Types.LogoutResponse.LogoutPayload {logout = false};
                onFinish?.Invoke(new GraphQL.Types.LogoutResponse {data = logoutResponse});
            };
        }

        public static void ExportProject(int projectId, int workspaceId, string guid, Action onFinish, Action<string> onError)
        {
            var mutation = Mutations.GetCreateExportProjectTaskMutation(projectId, guid, workspaceId);
            var requestGraph = new RequestGraph(mutation);

            requestGraph.OnFinish += response =>
            {
                var responseGraph = response as ResponseGraph;

                try
                {
                    var exportResponse = responseGraph?.Data.JsonDeserialize<GraphQL.Types.CreateTaskResponse>();
                    onFinish?.Invoke();
                }
                catch (Exception e)
                {
                    onError?.Invoke(e.ToString());
                }
            };

            requestGraph.OnError += s => onError?.Invoke(s);
        }

        public static void RefreshSession(string refreshToken, Action<string, string> onComplete, Action<string> onError = null)
        {
            GraphQLQuery mutation = Mutations.GetRefreshSessionMutation(refreshToken);
            var requestGraph = new RequestGraph(mutation, false);

            requestGraph.OnFinish += response =>
            {
                var responseGraph = response as ResponseGraph;

                try
                {
                    var responseInfo = responseGraph?.Data.JsonDeserialize<Varwin.GraphQL.Types.TokensInfo>();
                    var refreshSession = responseInfo?.Data?.RefreshSession;
                    onComplete?.Invoke(refreshSession?.AccessToken, refreshSession?.RefreshToken);
                }
                catch (Exception e)
                {
                    Debug.LogError(RequestGraph.GetResponseErrorMessage(requestGraph, responseGraph)
                                   + Environment.NewLine
                                   + $"Exception = {e}");
                }
            };

            requestGraph.OnError += message => { Debug.LogError($"Can't refresh session: {message}"); onError?.Invoke(message);};
        }

        public static void UpdateTokens(string accessToken, string refreshToken)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                return;
            }

            Request.AccessToken = accessToken;
            Request.RefreshToken = refreshToken;
            Debug.Log("Access token updated");

            SetupTokenExpiration(accessToken, refreshToken);
        }

        private static void SetupTokenExpiration(string accessToken, string refreshToken)
        {
            string[] encodedParts = accessToken.Split('.');
            string encodedString = FixBase64Padding(encodedParts[1]);
            byte[] encodedData = Convert.FromBase64String(encodedString);
            string decodedString = Encoding.UTF8.GetString(encodedData);

            var payload = decodedString.JsonDeserialize<Varwin.GraphQL.Types.TokenPayload>();
            DateTime expireTime = UnixTimeStampToDateTime(payload.Exp).AddMinutes(-SessionRefreshOverrunMinutes);

            if (expireTime < DateTime.Now)
            {
#if UNITY_EDITOR

                refreshToken = SessionState.GetString("refreshToken", null);
#else
                Debug.LogError($"Acces token is already expired at {expireTime}");
                return;
#endif
            }
#if UNITY_EDITOR
            else
            {
                SessionState.SetString("refreshToken", refreshToken);
            }
#endif

            StartExpireTimerTask(expireTime, () => { RefreshSession(refreshToken, UpdateTokens); });
        }

        private static void StartExpireTimerTask(DateTime targetTime, Action elapsed)
        {
            if (_accessTokenExpireTask != null && !_accessTokenExpireTask.IsCompleted)
            {
                _taskCancellationToken.ThrowIfCancellationRequested();
                TaskCancellationTokenSource.Cancel();
                TaskCancellationTokenSource.Dispose();
            }

            TaskCancellationTokenSource = new CancellationTokenSource();
            _taskCancellationToken = TaskCancellationTokenSource.Token;
            var refreshTime = (int) targetTime.Subtract(DateTime.Now).TotalMilliseconds;
            if (refreshTime <= 0) // prevent int overflow
            {
                refreshTime = int.MaxValue;
            }

            _accessTokenExpireTask = Task.Run(() => InvokeAfterSeconds(refreshTime, () => elapsed?.Invoke()));
        }

        private static void InvokeAfterSeconds(int seconds, Action callBack)
        {
            Thread.Sleep(seconds);
            callBack?.Invoke();
        }

        private static string FixBase64Padding(string base64)
        {
            int overLength = base64.Length % 4;
            return overLength > 0 ? base64.PadRight(base64.Length + (4 - overLength), '=') : base64;
        }

        private static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            return UnixEpochStartTime.AddSeconds(unixTimeStamp).ToLocalTime();
        }
    }
}