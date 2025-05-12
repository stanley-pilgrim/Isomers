using UnityEngine;
using Varwin;

namespace Varwin.Multiplayer
{
    public class NetworkPlayerWrapper : Wrapper
    {
        private NetworkPlayer _networkPlayer;

        public NetworkPlayer NetworkPlayer
        {
            get
            {
                if (!_networkPlayer)
                {
                    _networkPlayer = GameObject.GetComponent<NetworkPlayer>();
                }

                return _networkPlayer;
            }
        }
        
        public NetworkPlayerWrapper(GameEntity entity) : base(entity)
        {
        }

        public NetworkPlayerWrapper(GameObject gameObject) : base(gameObject)
        {
        }
    }
}