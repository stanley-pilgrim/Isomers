using System.Collections;
using UnityEngine;

namespace Varwin.WWW
{
    public class RequestLoadAssetFromFile : Request
    {
        public string AssetName { get; }
        public byte[] Bytes;

        public RequestLoadAssetFromFile(string assetName, string assetPath, object[] userObjects = null)
        {
            AssetName = assetName;
            UserData = userObjects;
            Uri = Settings.Instance.StoragePath + assetPath;
            RequestManager.AddRequest(this);
        }

        protected override IEnumerator SendRequest()
        {
            Debug.Log($"RequestLoadAssetFromFile: \"{AssetName}\"");
            yield return AssetBundleManager.Instance.InstantiateAssetFromFileAsync(this);
        }

    }

}


