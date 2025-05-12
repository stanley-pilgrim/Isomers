using System;
using UnityEngine;
using Varwin.Public;

public class ChainedJointControllerBase : MonoBehaviour
{
    public event Action<Collider> OnCollisionEnter;
    public event Action<Collider> OnCollisionExit;
    
    public event Action<JointPoint, JointPoint> OnJointEnter;
    public event Action<JointPoint, JointPoint> OnJointExit;

    public virtual void CollisionEnter(Collider other) {}
    public virtual void CollisionExit(Collider other) { }
    
    public bool Connecting { get; set; }

    public void OnCollisionEnterInvoke(Collider collider)
    {
        OnCollisionEnter?.Invoke(collider);
    }
    
    public void OnCollisionExitInvoke(Collider collider)
    {
        OnCollisionExit?.Invoke(collider);
    }
    
    public void JointEnter(JointPoint senderPoint, JointPoint nearPoint)
    {
        OnJointEnter?.Invoke(senderPoint, nearPoint);
    }

    public void JointExit(JointPoint senderPoint, JointPoint nearPoint)
    {
        OnJointExit?.Invoke(senderPoint, nearPoint);
    }

}