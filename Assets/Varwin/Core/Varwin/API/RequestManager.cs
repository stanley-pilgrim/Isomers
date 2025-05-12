using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Varwin.UI;
using Varwin.UI.VRErrorManager;

namespace Varwin.WWW
{
    public class RequestManager : MonoBehaviour
    {
        public bool WorkingWithRequests;
        public static RequestManager Instance;

        private bool _isConcurrentQueueServed = true;
        private const int MaxConcurrentRoutines = 4;
        
        private static List<Request> _requestQueue = new List<Request>();
        private static readonly Queue<Request> concurrentRequestQueue = new Queue<Request>();
        private static readonly List<Request> RequestQueueToAdd = new List<Request>();
        private static int _requestCounter;
        private bool _stop;
        private int _concurrentRoutines;

        private void Awake()
        {
            if (Instance == null)
            {
                DontDestroyOnLoad(gameObject);
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (!WorkingWithRequests && (_requestQueue.Count > 0 || RequestQueueToAdd.Count > 0))
            {
                StartCoroutine(SendRequests());
            }

            foreach (Request request in _requestQueue)
            {
                request.OnUpdate?.Invoke();
            }
        }

        public void StopRequestsWithError(string error)
        {
            _stop = true;
            _requestQueue = new List<Request>();
            RequestQueueToAdd.Clear();

            if (VRErrorManager.Instance)
            {
                VRErrorManager.Instance.ShowFatal(error, Environment.StackTrace);
            }

            if (LauncherErrorManager.Instance)
            {
                LauncherErrorManager.Instance.ShowFatal(error, Environment.StackTrace);
            }
        }

        public static void AddRequest(Request request)
        {
            request.Number = _requestCounter;
            RequestQueueToAdd.Add(request);
            _requestCounter++;
        }

        private IEnumerator SendRequests()
        {
            WorkingWithRequests = true;
            _requestQueue.AddRange(RequestQueueToAdd.Where(x => !x.RunInParallel));
            _requestQueue = _requestQueue.OrderBy(request => request.Number).ToList();

            foreach (Request request in RequestQueueToAdd.Where(x => x.RunInParallel))
            {
                concurrentRequestQueue.Enqueue(request);
            }

            if (concurrentRequestQueue.Count > 0)
            {
                StartCoroutine(RunRequestsConcurrent());
            }

            RequestQueueToAdd.Clear();

            foreach (Request request in _requestQueue)
            {
                if (_stop)
                {
                    break;
                }

                if (request.Done)
                {
                    continue;
                }

                IRequest r = request;

                yield return StartCoroutine(r.SendRequest());
            }
            
            
            while (!_isConcurrentQueueServed)
            {
                yield return null;
            }

            foreach (Request request in _requestQueue)
            {
                if (!request.Error)
                {
                    continue;
                }

                WorkingWithRequests = false;
            }

            for (int i = 0; i < _requestQueue.Count; i++)
            {
                _requestQueue[i] = null;
            }

            _requestQueue.Clear();

            GCManager.Collect();

            WorkingWithRequests = false;

            yield return true;
        }

        private IEnumerator RunRequestsConcurrent()
        {
            while (concurrentRequestQueue.Count > 0)
            {
                _isConcurrentQueueServed = false;
                while (_concurrentRoutines < MaxConcurrentRoutines && concurrentRequestQueue.Count > 0)
                {
                    _concurrentRoutines++;
                    StartCoroutine(ConcurrentRequest(concurrentRequestQueue.Dequeue()));
                }

                yield return null;
            }
        }

        private IEnumerator ConcurrentRequest(IRequest request)
        {
            yield return StartCoroutine(request.SendRequest());
            _concurrentRoutines--;

            if (concurrentRequestQueue.Count == 0 && _concurrentRoutines == 0)
            {
                _isConcurrentQueueServed = true;
            }
        }
    }
}
