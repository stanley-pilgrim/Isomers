using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Varwin.Multiplayer.Types
{
    public struct ApiConnectionData : INetworkSerializable
    {
        public FixedString32Bytes ApplicationVersion;
        public FixedString32Bytes ApiAddress;
        public FixedString32Bytes RefreshToken;
        public FixedString32Bytes AccessToken;
        public int ProjectConfigurationId;
        public int SceneId;
        public ulong ClientId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            var size = ApplicationVersion.Length + ApiAddress.Length + RefreshToken.Length + AccessToken.Length + sizeof(int) * 2 + sizeof(ulong);
            if (!serializer.PreCheck(size))
            {
                Debug.LogError($"Can't write {size} bytes to network serializer");
            }

            serializer.SerializeValue(ref ApplicationVersion);
            serializer.SerializeValue(ref ApiAddress);
            serializer.SerializeValue(ref ProjectConfigurationId);
            serializer.SerializeValue(ref SceneId);
            serializer.SerializeValue(ref RefreshToken);
            serializer.SerializeValue(ref AccessToken);
            serializer.SerializeValue(ref ClientId);
        }
    }
}