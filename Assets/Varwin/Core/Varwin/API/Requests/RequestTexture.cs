using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Varwin.Data;

namespace Varwin.WWW
{
    public class RequestTexture : Request
    {
        private TextureFormat _format;

        /// <summary>
        /// Request to load texture
        /// </summary>
        /// <param name="uri">link with out api host</param>
        /// <param name="runInParallel">can manager run this request in parallel</param>
        public RequestTexture(string uri, bool runInParallel = false)
        {
            Uri = uri;
            RunInParallel = runInParallel;
            RequestManager.AddRequest(this);
        }

        protected override IEnumerator SendRequest()
        {
            using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(Uri))
            using (var downloadHandler = new DownloadHandlerTexture(false))
            {
                webRequest.downloadHandler = downloadHandler;
            
                if (!string.IsNullOrEmpty(AccessToken))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
                }
                
                var clientId = SocketId?.ToString();
                if (!string.IsNullOrEmpty(clientId))
                {
                    webRequest.SetRequestHeader("X-Socket-ID", clientId);
                }
            
                yield return webRequest.SendWebRequest();

                if (webRequest.HasError())
                {
                    ((IRequest) this).OnResponseError($"Texture can not be loaded due to web request error: {webRequest.error}");
                    yield break;
                }
                
                try
                {
                    var responseTexture = new ResponseTexture {Texture = downloadHandler.texture};
                    ((IRequest) this).OnResponseDone(responseTexture);
                }
                catch (Exception e)
                {
                    ((IRequest) this).OnResponseError("Texture can not be loaded! " + e);
                }
            }
        }
    }
}
