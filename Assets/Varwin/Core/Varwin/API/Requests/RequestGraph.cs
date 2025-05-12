using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Varwin.Data;
using Varwin.GraphQL;
using Varwin.Log;

namespace Varwin.WWW
{
    public class RequestGraph : Request
    {
        public enum RequestErrorStatus
        {
            Unknown,
            ConnectionError,
            GraphQLError
        }

        public long RequestResponseCode { get; private set; }
        public RequestErrorStatus ErrorStatus { get; private set; }

        private static readonly JsonSerializerSettings JsonConvertSettings = new()
        {
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
            StringEscapeHandling = StringEscapeHandling.Default
        };

        private readonly GraphQLQuery _graphQlQuery;
        private readonly string _rawQuery;
        private readonly bool _verbose;

        public RequestGraph(GraphQLQuery query, bool verbose = true)
        {
            Uri = $"{Settings.Instance.ApiHost}/query";
            _graphQlQuery = query;
            _rawQuery = JsonConvert.SerializeObject(_graphQlQuery, JsonConvertSettings);
            _verbose = verbose;

            RequestManager.AddRequest(this);
        }

        protected override IEnumerator SendRequest()
        {
            using (var request = UnityWebRequest.Post(Uri, UnityWebRequest.kHttpVerbPOST))
            {
                var uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(_rawQuery));
                var downloadHandler = new DownloadHandlerBuffer();

                Debug.Log(_verbose ? $"Request: {_rawQuery.Replace("\\r", "\r").Replace("\\n", "\n")}" : "Sending request");

                request.uploadHandler.Dispose();
                request.uploadHandler = uploadHandler;
                request.downloadHandler = downloadHandler;

                if (!string.IsNullOrEmpty(AccessToken))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
                }

                var clientId = SocketId?.ToString();
                if (!string.IsNullOrEmpty(clientId))
                {
                    request.SetRequestHeader("X-Socket-ID", clientId);
                }

                request.SetRequestHeader("Content-Type", "application/json");
                request.SendWebRequest();

                ErrorStatus = RequestErrorStatus.Unknown;

                float timer = 0;
                while (!request.isDone)
                {
                    if (timer > TimeOut)
                    {
                        ErrorStatus = RequestErrorStatus.ConnectionError;
                        RequestResponseCode = request.responseCode;
                        ((IRequest)this).OnResponseError($"{this} Timeout error", request.responseCode);
                        yield break;
                    }

                    timer += Time.deltaTime;
                    yield return null;
                }

                if (request.HasError())
                {
                    ErrorStatus = RequestErrorStatus.ConnectionError;
                    RequestResponseCode = request.responseCode;
                    ((IRequest)this).OnResponseError($"{this} Error\n\n{request.downloadHandler.text}", request.responseCode);
                }
                else
                {
                    var json = request.downloadHandler.text;
                    var errorsDefinition = new { errors = new List<GraphErrorContainer>() };
                    var errors = JsonConvert.DeserializeAnonymousType(json, errorsDefinition).errors;
                    if (errors != null)
                    {
                        var errorMessages = string.Join("\n", errors.Select(x => x.Message));
                        ErrorStatus = RequestErrorStatus.GraphQLError;
                        RequestResponseCode = request.responseCode;
                        ((IRequest)this).OnResponseError($"{this} GraphQL request errors:\n{errorMessages}", request.responseCode);
                        yield break;
                    }

                    var response = new ResponseGraph { Data = json };

                    ((IRequest)this).OnResponseDone(response, request.responseCode);
                }
            }
        }

        public static string GetResponseErrorMessage(RequestGraph request, ResponseGraph response)
        {
            return "API Error" + Environment.NewLine
                               + Environment.NewLine
                               + "\tREQUEST" + Environment.NewLine
                               + $"TYPE = {request._graphQlQuery.Type}" + Environment.NewLine
                               + $"URI = {request.Uri}" + Environment.NewLine
                               + $"RequestBody = {request._rawQuery}" + Environment.NewLine
                               + Environment.NewLine
                               + "\tRESPONSE" + Environment.NewLine
                               + $"Message = {response.Message}" + Environment.NewLine
                               + $"Code = {response.Code}" + Environment.NewLine
                               + $"ResponseCode = {response.ResponseCode}" + Environment.NewLine
                               + $"ResponseBody = {response.Data}" + Environment.NewLine
                               + Environment.NewLine;
        }

        public static string GetResponseInfoMessage(RequestGraph request, ResponseGraph response)
        {
            var code = !string.IsNullOrEmpty(response.Code) ? $"({response.Code})" : "";
            var url = $"{Settings.Instance.ApiHost + request.Uri.Replace(Settings.Instance.ApiHost, "")}";
            var body = $"{request._rawQuery ?? string.Empty}\n";
            return $"{request._graphQlQuery.Type} {url} : {response.Status} {code}\n{body}";
        }
    }

    internal class GraphErrorContainer
    {
        public string Message { get; set; }
    }
}