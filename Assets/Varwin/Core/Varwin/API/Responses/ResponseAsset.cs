using UnityEngine;
using Varwin.WWW;

namespace Varwin.Data.ServerData
{
    public class ResponseAsset : IResponse
    {
        #region factory method

        public static ResponseAsset Get(Object asset, string path, object[] userData) => new ResponseAsset()
        {
            Asset = asset,
            Path = path,
            UserData = userData
        };

        #endregion

        public Object Asset;
        public string Path;
        public object[] UserData;
    }
}
