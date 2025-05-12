using System.Collections;
using UnityEngine;

namespace Varwin.WWW
{
    public class RequestAsset : Request
    {
        public string AssetName { get; }
        public CachedAssetBundle BundleCacheSettings { get; }
        
        public RequestAsset(string assetName, string assetBundleUri, CachedAssetBundle bundleCacheSettings, object[] userObjects = null, bool runInParallel = false)
        {
            AssetName = assetName;
            UserData = userObjects;
            Uri = Settings.Instance.ApiHost + assetBundleUri;
            BundleCacheSettings = bundleCacheSettings;
            RunInParallel = runInParallel;
            RequestManager.AddRequest(this);
        }

        protected override IEnumerator SendRequest()
        {
            yield return AssetBundleManager.Instance.InstantiateGameObjectAsync(this);
        }
        
    }
  
}
