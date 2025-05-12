using System.Collections;

namespace Varwin.WWW
{
    public class RequestLoadSceneFromFile : Request
    {
        public string AssetName { get; }
        public byte[] Bytes;
        public readonly bool AlwaysLoaded;

        public RequestLoadSceneFromFile(string assetName, string assetPath, object[] userObjects = null, bool alwaysLoaded = false)
        {
            AssetName = assetName;
            UserData = userObjects;
            Uri = assetPath;
            AlwaysLoaded = alwaysLoaded;
            RequestManager.AddRequest(this);
        }

        protected override IEnumerator SendRequest()
        {
            yield return AssetBundleManager.Instance.InstantiateSceneFromFileAsync(this);
        }

    }

}
