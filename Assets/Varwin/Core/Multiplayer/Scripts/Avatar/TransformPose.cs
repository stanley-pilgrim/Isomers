using UnityEngine;

namespace Varwin.Multiplayer
{
    public class TransformPose : MonoBehaviour
    {
        public Quaternion RotationOffset;
        public Vector3 PositionOffset;

        public void SetPosition(Vector3 position)
        {
            transform.position = PositionOffset + position;
        }

        public void SetRotation(Quaternion rotation)
        {
            transform.rotation = rotation * RotationOffset;
        }
    }
}