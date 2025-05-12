using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Assertions;

namespace Core.Varwin
{
    public class CommandPipeHandler
    {
        #region constants

        private const string NormalizedPipePathFieldName = "_normalizedPipePath";
        private const string PipeServer = ".";
        
        private const int ReadBufferSize = 2048;
        private const float PipeWaitTime = 0.1f;

        #endregion

        #region properties

        private bool IsRunning => _pipeClientStream?.IsConnected ?? false;

        #endregion

        #region attributes

        private readonly WaitForSeconds _waitForSeconds =  new WaitForSeconds(PipeWaitTime);
        private readonly MonoBehaviour _coroutineStarter;
        private readonly Queue<string> _receiveQueue;
        private readonly Queue<string> _sendQueue;
        private readonly byte[] _buffer = new byte[ReadBufferSize];

        private NamedPipeClientStream _pipeClientStream;

        private string _normalizedPath;

        #endregion

        #region constructors

        public CommandPipeHandler(Queue<string> receiveQueue, Queue<string> sendQueue, string pipeName)
        {
            Assert.IsNotNull(receiveQueue);
            Assert.IsNotNull(sendQueue);
            Assert.IsFalse(string.IsNullOrEmpty(pipeName));

            _receiveQueue = receiveQueue;
            _sendQueue = sendQueue;
            _pipeClientStream = new NamedPipeClientStream(PipeServer, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        }

        #endregion

        #region public methods

        public void PipeStart(MonoBehaviour coroutineStarter)
        {
            coroutineStarter.StartCoroutine(PipeListenCoroutine());
        }

        public void SendPipeCommand(object command, bool verbose = false)
        {
            string serializedCommand = JsonConvert.SerializeObject(command);

            _sendQueue.Enqueue(serializedCommand);
            SendData();

            if (verbose)
            {
                Debug.Log($"Pipe send attempt: {serializedCommand}");
            }
        }

        public void Dispose() => _pipeClientStream.Dispose();

        #endregion

        #region service methods

        private IEnumerator PipeListenCoroutine()
        {
            var filePathField = _pipeClientStream.GetType().GetField(NormalizedPipePathFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            _normalizedPath = (string)filePathField?.GetValue(_pipeClientStream);

            Debug.Log($"normalized pipe path: '{_normalizedPath}'");

            _pipeClientStream.ConnectAsync();

            yield return new WaitWhile(() => !_pipeClientStream.IsConnected);

            while (IsPipeExists())
            {
                yield return _waitForSeconds;

                if (!_pipeClientStream.CanRead)
                {
                    continue;
                }

                Task<int> bytesReceived = _pipeClientStream.ReadAsync(_buffer, 0, _buffer.Length);

                yield return new WaitUntil(() => bytesReceived.IsCompleted);

                if (bytesReceived.Result == 0)
                {
                    continue;
                }

                string data = Encoding.ASCII.GetString(_buffer).TrimEnd('\0');

                if (string.IsNullOrEmpty(data))
                {
                    continue;
                }

                _receiveQueue.Enqueue(data);

                Array.Clear(_buffer, 0, _buffer.Length);
            }
        }

        private bool IsPipeExists() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? IsRunning : File.Exists(_normalizedPath);

        private void SendData()
        {
            if (_pipeClientStream == null
                || !_pipeClientStream.IsConnected
                || !_pipeClientStream.CanWrite
                || _sendQueue.Count == 0)
            {
                return;
            }

            while (_sendQueue.TryDequeue(out string data))
            {
                byte[] dataBytes = Encoding.ASCII.GetBytes(data);
                _pipeClientStream.Write(dataBytes, 0, dataBytes.Length);
            }
        }

        #endregion
    }
}