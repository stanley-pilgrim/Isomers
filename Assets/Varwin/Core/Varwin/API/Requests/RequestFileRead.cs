using System;
using System.Collections;
using System.IO;
using System.Threading;
using Varwin.Data;

namespace Varwin.WWW
{
    public class RequestFileRead : Request
    {
        private bool _absoluteUrl = false;
        
        public RequestFileRead(string fileName, object[] userData = null, bool runInParallel = false, bool absoluteUrl = false)
        {
            Uri = fileName;
            UserData = userData;
            RunInParallel = runInParallel;
            _absoluteUrl = absoluteUrl;
            RequestManager.AddRequest(this);
        }

        protected override IEnumerator SendRequest()
        {
            yield return Get();
        }

        #region GET METHOD

        private IEnumerator Get()
        {
            byte[] file = { };
            var done = false;
            var found = false;
            var error = string.Empty;
            var uriFile = _absoluteUrl ? Uri : Settings.Instance.StoragePath + Uri;

            if (File.Exists(uriFile))
            {
                found = true;
            }
            
            new Thread(() =>
            {
                try
                {
                    file = File.ReadAllBytes(uriFile);
                    done = true;
                }
                catch (Exception e)
                {
                    error = e.Message;
                    done = true;
                }
            }).Start();

            while (!done)
            {
                yield return null;
            }


            if (!found)
            {
                error = "file not found";
            }

            if (file.Length == 0 || error != String.Empty)
            {
                ((IRequest) this).OnResponseError($"Load file {Uri} from storage error! {error}");
            }
            else
            {
                var response = new ResponseFileRead {ByteData = file, UserData = UserData};
                ((IRequest) this).OnResponseDone(response);
            }

            yield return true;

            #endregion
        }
    }
}