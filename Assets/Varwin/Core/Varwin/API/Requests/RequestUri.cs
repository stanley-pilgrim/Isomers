using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Varwin.Data;

namespace Varwin.WWW
{
    public class RequestUri : Request
    {
        public RequestUri(string uri, object[] userData = null, bool runInParallel = false)
        {
            Uri = uri;
            UserData = userData;
            RunInParallel = runInParallel;
            RequestManager.AddRequest(this);
        }

        protected override IEnumerator SendRequest()
        {
            yield return Get();
        }

        #region GET METHOD

        private IEnumerator Get()
        {
            string uri;
            switch (LoaderAdapter.Loader)
            {
                case ApiLoader:
                {
                    uri = Settings.Instance.ApiHost;
                    if (!uri.EndsWith("/"))
                    {
                        uri += "/";
                    }

                    uri += Uri;
                    break;
                }
                case StorageLoader:
                    uri = $"{Settings.Instance.StoragePath}{Uri}";

                    if (!uri.ToLowerInvariant().StartsWith("jar:file:///") && !uri.ToLowerInvariant().StartsWith("file:///"))
                    {
                        uri = $"file:///{uri}";
                    }
                    
                    break;
                default:
                    Debug.LogError($"{this}:Can't load URL: {Uri}");
                    yield break;
            }

            using var webRequest = UnityWebRequest.Get(uri);

            if (!string.IsNullOrEmpty(AccessToken))
            {
                webRequest.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
            }

            var clientId = SocketId?.ToString();
            if (!string.IsNullOrEmpty(clientId))
            {
                webRequest.SetRequestHeader("X-Socket-ID", clientId);
            }

            webRequest.SendWebRequest();

            float timer = 0;
            while (!webRequest.isDone)
            {
                if (timer > TimeOut)
                {
                    ((IRequest)this).OnResponseError($"{this} : TimeOut", webRequest.responseCode);
                    yield break;
                }

                timer += Time.deltaTime;
                yield return null;
            }

            if (webRequest.HasError())
            {
                ((IRequest)this).OnResponseError($"{this} : {webRequest.result}", webRequest.responseCode);
            }
            else
            {
                var response = new ResponseUri
                {
                    ResponseCode = webRequest.responseCode,
                    TextData = webRequest.downloadHandler.text,
                    ByteData = webRequest.downloadHandler.data
                };
                ((IRequest)this).OnResponseDone(response, webRequest.responseCode);
            }
        }

        #endregion
    }
}