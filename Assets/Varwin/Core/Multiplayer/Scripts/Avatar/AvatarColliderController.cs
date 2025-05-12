using UnityEngine;

namespace Varwin.Multiplayer
{
    [RequireComponent(typeof(CapsuleCollider))]
    public class AvatarColliderController : MonoBehaviour
    {
        [SerializeField] private Avatar _avatar;
        private CapsuleCollider _capsuleCollider;

        private void Awake()
        {
            _capsuleCollider = GetComponent<CapsuleCollider>();
        }

        private void FixedUpdate()
        {
            var currentHeight = _avatar.HeadPosition.Value.y - _avatar.RigPosition.Value.y;

            if (Mathf.Abs(_capsuleCollider.height - currentHeight) > 0.01f)
            {
                _capsuleCollider.height = currentHeight;
                _capsuleCollider.center = currentHeight / 2 * Vector3.up;
            }
        }
    }
}