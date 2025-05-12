using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Varwin.Data;

namespace Varwin.WWW
{
    public class RequestVideo : Request
    {
        public static string VideosDirectory => Path.Combine(Application.temporaryCachePath, "Videos");
        
        public RequestVideo(string uri)
        {
            Uri = uri;
            RequestManager.AddRequest(this);
        }

        protected override IEnumerator SendRequest()
        {
            var videoClipName = Uri.Split('/').Last();
            string tempFolder = Path.Combine(VideosDirectory, videoClipName);
           
            if (File.Exists(tempFolder))
            {
                var responseVideo = new ResponseVideo() {VideoUrl = tempFolder};
                ((IRequest) this).OnResponseDone(responseVideo);
            }
            else
            {
                using (UnityWebRequest webRequest = UnityWebRequest.Get(Uri))
                {
                    if (!string.IsNullOrEmpty(AccessToken))
                    {
                        webRequest.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
                    }
                    
                    var clientId = SocketId?.ToString();
                    if (!string.IsNullOrEmpty(clientId))
                    {
                        webRequest.SetRequestHeader("X-Socket-ID", clientId);
                    }

                    if (!Directory.Exists(VideosDirectory))
                    {
                        Directory.CreateDirectory(VideosDirectory);
                    }

                    var downloadHandler = new DownloadHandlerFile(tempFolder) {removeFileOnAbort = true};
                    webRequest.downloadHandler = downloadHandler;

                    yield return webRequest.SendWebRequest();

                    if (webRequest.HasError())
                    {
                        ((IRequest) this).OnResponseError($"Video can not be loaded due to web request error: {webRequest.error}");
                        yield break;
                    }

                    try
                    {
                        var responseVideo = new ResponseVideo() {VideoUrl = tempFolder};
                        ((IRequest) this).OnResponseDone(responseVideo);
                    }
                    catch (Exception e)
                    {
                        ((IRequest) this).OnResponseError("Video can not be loaded! " + e.Message);
                    }
                }
            }
        }
    }
}