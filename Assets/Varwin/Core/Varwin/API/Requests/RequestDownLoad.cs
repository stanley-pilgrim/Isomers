using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Varwin.Core;
using Varwin.Data;
using Debug = UnityEngine.Debug;

namespace Varwin.WWW
{
    [SuppressMessage("ReSharper", "SpecifyACultureInStringConversionExplicitly")]
    [SuppressMessage("ReSharper", "RedundantAssignment")]
    public class RequestDownLoad : Request
    {
        public ResponseDownLoad Response;

#if !UNITY_ANDROID
        private const int BufferSize = 16 * 1024 * 1024;
#else
        private const int BufferSize = 8 * 1024 * 1024;
#endif

        private const int Square1024 = 1024 * 1024;

        #region PRIVATE VARS

        private BaseLoader _loader;
        private readonly string _stringFormat;
        private bool _isGetting;
        private bool _isDownloading;
        private string _fileName;
        private readonly string _downLoadPath;
        private uint _contentLength;
        private ulong _n;
        private int _read;
        private NetworkStream _networkStream;
        private FileStream _fileStream;
        private Socket _client;
        private readonly List<string> _uris;
        private byte[] _buffer = new byte[BufferSize];

        private readonly LoaderAdapter.ProgressUpdate _onLoadingUpdate;

        #endregion

        /// <summary>
        /// Download large file request
        /// </summary>
        /// <param name="uris"></param>
        /// <param name="downLoadPath">Local download path</param>
        /// <param name="onLoadingUpdate"></param>
        /// <param name="loader">Text, to show progress</param>
        /// <param name="stringFormat">String format for progress {0}:File name {1}:Downloaded bytes {2}: File length</param>
        public RequestDownLoad(List<string> uris, string downLoadPath, LoaderAdapter.ProgressUpdate onLoadingUpdate, BaseLoader loader = null, string stringFormat = null)
        {
            Uri = uris.ToString();
            _uris = uris;
            _loader = loader;
            _stringFormat = stringFormat;
            _downLoadPath = downLoadPath;
            _onLoadingUpdate = onLoadingUpdate;
            OnUpdate += Update;

            Response = new ResponseDownLoad
            {
                LocalFilesPathes = new List<string>(),
                Filenames = new List<string>()
            };
            RequestManager.AddRequest(this);
        }

        public RequestDownLoad(string uri, string downLoadPath, BaseLoader loader = null, string stringFormat = null)
        {
            Uri = uri;
            _uris = new List<string> {uri};
            _loader = loader;
            _stringFormat = stringFormat;
            _downLoadPath = downLoadPath;
            OnUpdate += Update;

            Response = new ResponseDownLoad
            {
                LocalFilesPathes = new List<string>(),
                Filenames = new List<string>()
            };
            RequestManager.AddRequest(this);
        }

        protected override IEnumerator SendRequest()
        {
            foreach (string uri in _uris)
            {
                _fileName = Path.GetFileName(uri);
                _isGetting = true;
                var thread = new Thread(() => { StartDownload(Settings.Instance.ApiHost + uri); }) {IsBackground = true};
                thread.Start();

                _loader.FeedBackText = "Getting file...";
                while (_isGetting)
                {
                    yield return null;
                }

                _loader.FeedBackText = "Downloading file...";
                while (_isDownloading)
                {
                    yield return false;
                }
            }

            ((IRequest) this).OnResponseDone(Response);

            yield return true;
        }

        #region DOWNLOAD METHODS

        private void Update()
        {
            if (!_isDownloading)
            {
                return;
            }

            if (_n < _contentLength)
            {
                if (!Response.Filenames.Contains(_fileName))
                {
                    Response.Filenames.Add(_fileName);
                }

                if (_networkStream.DataAvailable)
                {
                    _read = _networkStream.Read(_buffer, 0, _buffer.Length);
                    _n += (ulong) _read;
                    _fileStream.Write(_buffer, 0, _read);
                }

                //if (NLoggerSettings.LogDebug) Debug.Log("Downloaded: " + _n + " of " + _contentLength + " bytes ...");

                string downloadedSize = Math.Round((decimal) _n / Square1024, 0).ToString();
                string fileSize = Math.Round((decimal) _contentLength / Square1024, 1).ToString();

                if (_loader != null)
                {
                    _loader.FeedBackText = I18next.Format(_stringFormat,
                        new ("file_name", ""),
                        new ("downloaded_size", downloadedSize),
                        new ("file_size", fileSize));
                }

                _onLoadingUpdate?.Invoke(_n / (float) _contentLength);
            }
            else
            {
                _isDownloading = false;

                if (_loader != null)
                {
                    _loader.FeedBackText = "Download complete!";
                }

                Debug.Log("Файл" + _fileName + "загружен");

                _fileStream.Flush();
                _fileStream.Close();
                _client.Close();
                Response.LocalFilesPathes.Add(Path.Combine(_downLoadPath, _fileName));
            }
        }


        private void StartDownload(string uri)
        {
            if (_isDownloading)
            {
                return;
            }

            _contentLength = 0;
            _n = 0;
            _read = 0;

            var myUri = new Uri(uri);
            string host = myUri.Host;
            int port = myUri.Port;
            var accessKey = "";
            var clientId = "";

            if (!string.IsNullOrEmpty(AccessToken))
            {
                accessKey = $"Authorization: Bearer {AccessToken}\r\n";
            }

            var id = SocketId?.ToString();
            if (!string.IsNullOrEmpty(id))
            {
                clientId = $"X-Socket-ID: {id}";
            }

            string query = "GET " + uri.Replace(" ", "%20") + " HTTP/1.1\r\n" +
                           "Host: " + host + "\r\n" +
                           "Port: " + port + "\r\n" +
                           "User-Agent: undefined\r\n" +
                           "Connection: close\r\n" +
                           accessKey + "\r\n" +
                           clientId + "\r\n";

            Debug.Log(query);

            _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

            _client.Connect(host, port);

            _networkStream = new NetworkStream(_client);

            var bytes = Encoding.Default.GetBytes(query);
            _networkStream.Write(bytes, 0, bytes.Length);

            var bReader = new BinaryReader(_networkStream, Encoding.Default);

            string response = "";
            string line;
            char c;

            do
            {
                line = "";
                c = '\u0000';

                while (true)
                {
                    c = bReader.ReadChar();

                    if (c == '\r')
                    {
                        break;
                    }

                    line += c;
                }

                c = bReader.ReadChar();
                response += line + "\r\n";
            } while (line.Length > 0);

            Debug.Log(response);

            Regex reContentLength = new Regex(@"(?<=Content-Length:\s)\d+", RegexOptions.IgnoreCase);
            _contentLength = uint.Parse(reContentLength.Match(response).Value);

            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            _fileName = invalid.Aggregate(_fileName, (current, chr) => current.Replace(chr.ToString(), ""));
            string path = Path.Combine(_downLoadPath, _fileName);

            if (!Directory.Exists(_downLoadPath))
            {
                Directory.CreateDirectory(_downLoadPath);
            }
            
            _fileStream = new FileStream(path, FileMode.Create);

            _isGetting = false;
            _isDownloading = true;
        }
    }

    #endregion
}