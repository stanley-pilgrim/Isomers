using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Varwin.WWW
{
    public abstract class Request : IRequest
    {
        public static int WorkspaceId;
        public static string AccessToken { get; set; }
        public static string RefreshToken { get; set; }
        public static Guid? SocketId { get; set; }
        private DateTime _start;
        private DateTime _end;
        public float TimeOut = 300;
        public int RequestTime => CalculateTime();
        public bool Done { get; private set; }
        public bool Error { get; private set; }
        public int Number;
        public Action<IResponse> OnFinish;
        public Action<string> OnError;
        public Action OnUpdate;
        public string Uri;
        public List<string> Uris;
        public object[] UserData;
        public bool RunInParallel { get; protected set; } = false;
        public bool ErrorLogging { get; set; } = true;

        protected abstract IEnumerator SendRequest();

        IEnumerator IRequest.SendRequest()
        {
            _start = DateTime.Now;
            Error = false;
            Done = false;
            Debug.Log($"Send request {this} {Uri}");
            yield return SendRequest();
        }

        void IRequest.OnResponseDone(IResponse response, long statusCode)
        {
            Error = false;
            Done = true;
            _end = DateTime.Now;
            Debug.Log($"Response code={statusCode} from {Uri}");
            OnFinish?.Invoke(response);
        }

        void IRequest.OnResponseError(string message, long statusCode)
        {
            Error = true;
            Done = false;
            OnError?.Invoke(message);
            if (ErrorLogging)
            {
                Debug.LogError($"Response ERROR! code={statusCode} from {Uri} = {message}");
            }
        }

        private int CalculateTime()
        {
            TimeSpan span = _end - _start;
            return span.Milliseconds;
        }
    }

    public interface IRequest
    {
        IEnumerator SendRequest();
        void OnResponseDone(IResponse response, long statusCode = 0);
        void OnResponseError(string message, long statusCode = 0);
    }

    public interface IResponse
    {
    }
}
