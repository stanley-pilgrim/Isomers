using UnityEngine;
using Varwin.Core.Behaviours.ConstructorLib;

namespace Varwin.ObjectsInteractions
{
    public class MovableCollisionControllerElement : CollisionControllerElement
    {
        public MovableBehaviour MovableBehaviour { get; set; }
    
        protected override void RaiseOnTriggerEnter(Collider other)
        {
            base.RaiseOnTriggerEnter(other);
            MovableBehaviour.OnGrabbedCollisionEnter(other.gameObject);
        }

        protected override void RaiseOnTriggerExit(Collider other)
        {
            base.RaiseOnTriggerExit(other);
            MovableBehaviour.OnGrabbedCollisionExit(other.gameObject);
        }
    }
}