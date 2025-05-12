using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Varwin.Data;

namespace Varwin.WWW
{
    public class RequestAudio : Request
    {
        private static readonly Dictionary<string, AudioType> AudioFormats = new()
        {
            { "aif", AudioType.AIFF },
            { "ogg", AudioType.OGGVORBIS },
            { "wav", AudioType.WAV },
            { "wma", AudioType.WAV },
            { "mp2", AudioType.MPEG },
            { "mp3", AudioType.MPEG },
            { "mp4", AudioType.MPEG },
            { "m4a", AudioType.MPEG }
        };

        private readonly bool _streamAudio;

        public RequestAudio(string uri, bool streamAudio = false)
        {
            Uri = uri;
            _streamAudio = streamAudio;
            RequestManager.AddRequest(this);
        }

        protected override IEnumerator SendRequest()
        {
            using var webRequest = UnityWebRequestMultimedia.GetAudioClip(Uri, GetAudioTypeFromUrl(Uri));

            if (!string.IsNullOrEmpty(AccessToken))
            {
                webRequest.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
            }

            var clientId = SocketId?.ToString();
            if (!string.IsNullOrEmpty(clientId))
            {
                webRequest.SetRequestHeader("X-Socket-ID", clientId);
            }

            ((DownloadHandlerAudioClip)webRequest.downloadHandler).streamAudio = _streamAudio;

            yield return webRequest.SendWebRequest();

            if (webRequest.HasError())
            {
                ((IRequest)this).OnResponseError($"Audio can not be loaded due to web request error: {webRequest.error}");
                yield break;
            }

            try
            {
                var responseAudio = new ResponseAudio { AudioClip = DownloadHandlerAudioClip.GetContent(webRequest) };
                ((IRequest)this).OnResponseDone(responseAudio);
            }
            catch (Exception e)
            {
                ((IRequest)this).OnResponseError("Audio can not be loaded! " + e.Message);
            }
        }

        private static AudioType GetAudioTypeFromUrl(string url)
        {
            return AudioFormats.TryGetValue(Path.GetExtension(url).Remove(0, 1).ToLower(), out var audioFormat)
                ? audioFormat
                : AudioType.UNKNOWN;
        }
    }
}