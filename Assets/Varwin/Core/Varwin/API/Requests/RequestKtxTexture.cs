using System;
using System.Collections;
using KtxUnity;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Varwin.Data;

namespace Varwin.WWW
{
    public class RequestKtxTexture : Request
    {
#if UNITY_ANDROID
        private const byte MaxRequestsCount = 2;
#else
        private const byte MaxRequestsCount = 32;
#endif
        private static byte _currentRequestsCount;

        private TextureFormat _format;

        /// <summary>
        /// Request to load texture
        /// </summary>
        /// <param name="uri">link with out api host</param>
        /// <param name="runInParallel">can manager run this request in parallel</param>
        public RequestKtxTexture(string uri, bool runInParallel = false)
        {
            Uri = uri;
            RunInParallel = runInParallel;
            RequestManager.AddRequest(this);
        }

        protected override IEnumerator SendRequest()
        {
            while (_currentRequestsCount >= MaxRequestsCount)
            {
                yield return null;
            }
            _currentRequestsCount++;

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

                webRequest.SendWebRequest();

                float timer = 0;
                bool failed = false;

                while (!webRequest.isDone)
                {
                    if (timer > TimeOut)
                    {
                        failed = true;
                        break;
                    }

                    timer += Time.deltaTime;
                    yield return null;
                }

                if (webRequest.HasError())
                {
                    failed = true;
                }

                if (failed)
                {
                    ((IRequest) this).OnResponseError($"{this} Timeout error", webRequest.responseCode);
                }
                else
                {
                    byte[] buffer = webRequest.downloadHandler.data;
                    var nativeArray = new NativeArray<byte>(buffer, KtxNativeInstance.defaultAllocator);

                    try
                    {
                        var texture = new KtxTexture();
                        TextureResult result = texture.LoadBytesRoutine(nativeArray).Result;
                        var responseTexture = new ResponseTexture {Texture = result.texture};

                        ((IRequest) this).OnResponseDone(responseTexture);
                    }
                    catch (Exception e)
                    {
                        ((IRequest) this).OnResponseError("Texture can not be loaded! " + e);
                    }
                    finally
                    {
                        nativeArray.Dispose();
                    }
                }
            }

            _currentRequestsCount--;
        }
    }
}