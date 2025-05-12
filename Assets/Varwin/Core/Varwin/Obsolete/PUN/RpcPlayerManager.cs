using System;
using UnityEngine;

namespace Varwin
{
    [Obsolete]
    public class RpcPlayerManager : MonoBehaviour
    {
        public string UserId;

        public float FallingTime
        {
            get => PlayerManager.FallingTime;
            set => PlayerManager.FallingTime = value;
        }

        public IPlayerController CurrentRig => PlayerManager.CurrentRig;

        public double RespawnHeight => PlayerManager.RespawnHeight;

        public void LoadScene(string sid)
        {
            Scene.Load(sid);
        }
        
        private void LoadSceneRpc(string userId, string sid)
        {
            Scene.Load(sid);
        }
        
        public void LoadConfiguration(string sid)
        {
            Configuration.Load(sid);
        }
        
        private void LoadConfigurationRpc(string userId, string sid)
        {
            Configuration.Load(sid);
        }

        public void Respawn()
        {
            PlayerManager.Respawn();
        }

        private void RespawnRpc(string userId)
        {
            PlayerManager.Respawn();
        }

        public void SetSpawnPoint(Transform targetTransform)
        {
            PlayerAnchorManager.SpawnPoint = targetTransform;
        }

        private int GetTransformId(Transform targetTransform)
        {
            ObjectBehaviourWrapper objectBehaviourWrapper = targetTransform.gameObject.GetComponent<ObjectBehaviourWrapper>();

            if (!objectBehaviourWrapper)
            {
                throw new Exception("Transforms without wrapper not allowed");
            }

            return objectBehaviourWrapper.OwdObjectController.Id;
        }

        private void SetSpawnPointRpc(int instanceId, string userId)
        {
            PlayerAnchorManager.SpawnPoint = FindTransform(instanceId);
        }

        private Transform FindTransform(int instanceId)
        {
            WrappersCollection wrappersCollection = GameStateData.GetWrapperCollection();

            if (!wrappersCollection.ContainsKey(instanceId))
            {
                throw new Exception("Transform not found!");
            }

            return wrappersCollection.Get(instanceId).GetGameObject().transform;
        }

        public void Rotate(float angle)
        {
            PlayerManager.CurrentRig.Rotation = Quaternion.Euler(PlayerManager.CurrentRig.Rotation.eulerAngles + angle * Vector3.up);
        }

        public void RotateTo(Transform targetTransform)
        {
            RotateToTransform(targetTransform);
        }

        private void RotateToTransform(Transform targetTransform)
        {
            Vector3 lookPos = targetTransform.position - PlayerManager.CurrentRig.Position;
            lookPos.y = 0;
            PlayerManager.CurrentRig.Rotation = Quaternion.LookRotation(lookPos);
        }

        public void SetRotation(Vector3 targetAngle)
        {
            PlayerManager.CurrentRig.Rotation = Quaternion.Euler(targetAngle);
        }

        public bool UseGravity
        {
            get => PlayerManager.UseGravity;
            set => SetPlayerGravity(value);
        }

        private void SetPlayerGravity(bool value)
        {
            PlayerManager.UseGravity = value;
        }

        private void SetPlayerGravityRpc(bool value, string userId)
        {
            PlayerManager.UseGravity = value;
        }
        
        public void TeleportTo(Vector3 position)
        {
            PlayerManager.TeleportTo(position);
        }
    }
}