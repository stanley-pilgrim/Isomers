using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using UnityEngine;
using Varwin.Jobs;
using Varwin.WWW;

namespace Varwin.GraphQL
{
    public abstract class GraphQLSubscription
    {
        private static string Url => $"{Settings.Instance.ApiHost}/query";

        private string Body { get; }

        protected string Name => GetType().Name;

        private ClientWebSocket WebSocket { get; set; }

        private WebSocketUtils.WebSocketEventHandler WebSocketEventHandler { get; }

        protected Dictionary<string, object> Variables = null;

        private int _connectionTimeout = 4000;
        private DateTime? _reconnectedDateTime;

        protected GraphQLSubscription(string body)
        {
            Body = body;
            WebSocketEventHandler = new WebSocketUtils.WebSocketEventHandler();
            WebSocketEventHandler.OnDataReceive += DataReceived;
            WebSocketEventHandler.OnDisconnect += Reconnect;
            WebSocketEventHandler.OnError += HandleError;
            WebSocketEventHandler.OnClose += Close;
        }

        ~GraphQLSubscription()
        {
            Close();
        }

        public async void Subscribe()
        {
            if (WebSocket != null)
            {
                return;
            }

            try
            {
                var query = new GraphQLQuery
                {
                    Query = Body,
                    Variables = Variables,
                    Type = GraphRequestType.Query
                };
                WebSocket = await WebSocketUtils.WebsocketConnect(Url, query, WebSocketEventHandler, Request.AccessToken);
                _reconnectedDateTime = null;
                _connectionTimeout += 1000;
                
                Debug.Log($"Subscribed to {Name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Can't subscribe to {Name}:{Environment.NewLine}{e.Message}");
                Reconnect();
            }
        }

        private void HandleError(string data)
        {
            throw new ApplicationException($"Error in subscription {Name}:{Environment.NewLine}{data}");
        }

        private void Reconnect()
        {
            if (_reconnectedDateTime != null && (DateTime.Now - _reconnectedDateTime.Value).TotalMilliseconds > _connectionTimeout)
            {
                throw new ApplicationException($"GraphQL subscription disconnected {Name}!");
            }

            _reconnectedDateTime ??= DateTime.Now;
            if (WebSocket != null)
            {
                Unsubscribe();
            }

            Debug.Log($"Reconnecting to subscription {Name}");
            Subscribe();
        }

        private void Close()
        {
            WebSocketEventHandler.OnDataReceive -= DataReceived;
            WebSocketEventHandler.OnDisconnect -= Reconnect;
            WebSocketEventHandler.OnError -= HandleError;
            WebSocketEventHandler.OnClose -= Close;

            Debug.Log($"Closed subscription {Name}");
        }

        public async void Unsubscribe()
        {
            if (WebSocket == null)
            {
                return;
            }

            if (WebSocket.State == WebSocketState.Aborted)
            {
                WebSocket = null;
                return;
            }

            try
            {
                await WebSocketUtils.WebsocketDisconnect(WebSocket, WebSocketEventHandler);
                Debug.Log($"Unsubscribed from {Name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Can't unsubscribe from {Name}. Error: {e.Message}");
            }

            WebSocket = null;
        }

        protected abstract void DataReceiveCallback(string data);

        protected void DataReceived(string data)
        {
            JobManager.AddJob(new JobAction(() => DataReceiveCallback(data)));
        }
    }
}