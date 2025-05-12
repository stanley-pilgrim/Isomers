using System;
using UnityEngine;

namespace Varwin.PUN
{
    [Obsolete]
    public class MasterLoading : MonoBehaviour
    {
        public static event Action SpectatorJoined;
        
        public bool IsLoading;
        
        private string _roomName;
        
        public void SetClientReady()
        {
        }

        public void SpectatorReady()
        {
            SpectatorJoined?.Invoke();
        }
        
        public void OnPhotonSerializeView(object stream, object info)
        {
        }

        public void OnJoinedRoom()
        {
            Destroy(gameObject);
        }

        public void OnDisconnected(object cause)
        {
            Destroy(gameObject);
        }
    }
}