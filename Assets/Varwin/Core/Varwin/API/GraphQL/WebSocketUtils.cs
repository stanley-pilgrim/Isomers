using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Varwin.WWW;

namespace Varwin.GraphQL
{
    public static class WebSocketUtils
    {
        public class WebSocketEventHandler
        {
            public delegate void HandshakeCompleteHandler();

            public delegate void DataReceiveHandler(string data);

            public delegate void DisconnectHandler();

            public delegate void CloseHandler();

            public delegate void ErrorHandler(string data);

            public event HandshakeCompleteHandler OnHandshakeComplete;
            public event DataReceiveHandler OnDataReceive;
            public event DisconnectHandler OnDisconnect;
            public event CloseHandler OnClose;
            public event ErrorHandler OnError;

            public void InvokeDisconnectEvent()
            {
                OnDisconnect?.Invoke();
            }

            public void InvokeHandshakeCompleteEvent()
            {
                OnHandshakeComplete?.Invoke();
            }

            public void InvokeDataReceiveEvent(string data)
            {
                OnDataReceive?.Invoke(data);
            }

            public void InvokeCloseEvent()
            {
                OnClose?.Invoke();
            }

            public void InvokeErrorEvent(string data)
            {
                OnError?.Invoke(data);
            }
        }

        public delegate void DisconnectCallback();

        public static async Task<ClientWebSocket> WebsocketConnect(string subscriptionUrl, object payload, WebSocketEventHandler webSocketEventHandler = null, string socketId = "1")
        {
            var clientWebSocket = new ClientWebSocket();
            clientWebSocket.Options.AddSubProtocol("graphql-ws");

            var regex = new Regex(Regex.Escape("http"));
            var uri = new Uri(regex.Replace(subscriptionUrl, "ws", 1));

            try
            {
                await clientWebSocket.ConnectAsync(uri, CancellationToken.None);
                await WebsocketInit(clientWebSocket, webSocketEventHandler);
                await WebsocketSend(clientWebSocket, socketId, payload);
            }
            catch (Exception e)
            {
                throw new ApplicationException($"WebSocket connection failed: {e.Message}");
            }

            return clientWebSocket;
        }

        public static async Task WebsocketDisconnect(ClientWebSocket clientWebSocket, WebSocketEventHandler webSocketEventHandler = null, string socketId = "1")
        {
            string jsonData = $"{{\"type\":\"stop\",\"id\":\"{socketId}\"}}";
            var bytes = new ArraySegment<byte>(Encoding.ASCII.GetBytes(jsonData));
            await clientWebSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
            webSocketEventHandler?.InvokeCloseEvent();
        }

        private static async Task WebsocketInit(WebSocket clientWebSocket, WebSocketEventHandler webSocketEventHandler = null)
        {
            string jsonData = $"{{\"type\":\"connection_init\", \"payload\":{{ \"Authorization\": \"Bearer {Request.AccessToken}\", \"X-Socket-ID\": \"{Request.SocketId}\" }} }}";
            var bytes = new ArraySegment<byte>(Encoding.ASCII.GetBytes(jsonData));
            await clientWebSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            GetWsReturn(clientWebSocket, webSocketEventHandler);
        }

        private static async Task WebsocketSend(WebSocket clientWebSocket, string id, object payload)
        {
            string jsonData = JsonConvert.SerializeObject(new {id = $"{id}", type = "start", payload});
            var bytes = new ArraySegment<byte>(Encoding.ASCII.GetBytes(jsonData));
            Debug.Log($"WebSocket send: {jsonData}");
            await clientWebSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        //Call GetWsReturn to wait for a message from a websocket. GetWsReturn has to be called for each message
        private static async void GetWsReturn(WebSocket clientWebSocket, WebSocketEventHandler webSocketEventHandler = null)
        {
            while (clientWebSocket.State == WebSocketState.Open)
            {
                ArraySegment<byte> buffer;
                buffer = WebSocket.CreateClientBuffer(1024, 1024);
                WebSocketReceiveResult webSocketReceiveResult;
                string result = "";
                do
                {
                    try
                    {
                        webSocketReceiveResult = await clientWebSocket.ReceiveAsync(buffer, CancellationToken.None);
                    }
                    catch (Exception e)
                    {
                        webSocketEventHandler?.InvokeDisconnectEvent();
                        return;
                    }

                    result += Encoding.UTF8.GetString(buffer.Array ?? throw new ApplicationException("Buf = null"), buffer.Offset, webSocketReceiveResult.Count);
                } while (!webSocketReceiveResult.EndOfMessage);

                if (string.IsNullOrEmpty(result))
                {
                    return;
                }

                JObject jObject;
                try
                {
                    jObject = JObject.Parse(result);
                }
                catch (JsonReaderException e)
                {
                    throw new ApplicationException(e.Message);
                }

                string subType = (string) jObject["type"];
                switch (subType)
                {
                    case "connection_ack":
                    {
                        webSocketEventHandler.InvokeHandshakeCompleteEvent();
                        continue;
                    }
                    case "data":
                    {
                        webSocketEventHandler.InvokeDataReceiveEvent(result);
                        continue;
                    }
                    case "ka":
                    {
                        continue;
                    }
                    case "error":
                    case "connection_error":
                    {
                        webSocketEventHandler.InvokeErrorEvent(result);
                        continue;
                    }
                    case "subscription_fail":
                    {
                        webSocketEventHandler.InvokeErrorEvent("The subscription data failed");
                        continue;
                    }
                }

                break;
            }
        }
    }
}