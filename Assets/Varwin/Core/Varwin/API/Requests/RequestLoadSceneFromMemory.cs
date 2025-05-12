using System.Collections;

namespace Varwin.WWW
{
    public class RequestLoadSceneFromMemory : Request
    {
        public string AssetName { get; }
        public byte[] Bytes;
        public readonly bool AlwaysLoaded;

        public RequestLoadSceneFromMemory(string assetName, byte[] bytes, object[] userObjects = null, bool alwaysLoaded = false)
        {
            AssetName = assetName;
            UserData = userObjects;
            Bytes = bytes;
            Uri = "";
            AlwaysLoaded = alwaysLoaded;
            RequestManager.AddRequest(this);
        }

        protected override IEnumerator SendRequest()
        {
            yield return AssetBundleManager.Instance.InstantiateSceneFromMemoryAsync(this);
            Bytes = null;
            GCManager.Collect();
        }

    }

}
