using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Varwin;
using Varwin.Public;

[Obsolete("Use sockets instead")]
public class ChainedJointController : ChainedJointControllerBase
{
    public int ConnectedBehaviours => _connectedBehaviours.Count;

    public bool IsGrabbed;

    [SerializeField] private List<JointBehaviour> _connectedBehaviours = new List<JointBehaviour>();

    public bool Connecting => _connectedBehaviours.Any(x => x.Connecting);

    public float MeshSize => _connectedBehaviours.Sum(a => a.MeshSize);

    private HashSet<Collider> _frameIgnored = new HashSet<Collider>();

    public static ChainedJointController CreateNewChainedController()
    {
        var newGO = new GameObject("ChainedController");

        return newGO.AddComponent<ChainedJointController>();
    }

    private void FixedUpdate()
    {
        _frameIgnored.Clear();
    }

    public void OnChainedBehaviourConnection(JointBehaviour firstBehaviour, JointBehaviour secondBehaviour)
    {
        if (firstBehaviour.IsGrabbed || secondBehaviour.IsGrabbed)
        {
            return;
        }

        foreach (JointBehaviour behaviour in _connectedBehaviours)
        {
            if (!behaviour.CollisionController)
            {
                behaviour.AddCollisionController();
            }

            behaviour.CollisionController.enabled = false;
        }
    }

    public void AddBehaviour(JointBehaviour jointBehaviour)
    {
        if (_connectedBehaviours.Contains(jointBehaviour))
        {
            return;
        }

        _connectedBehaviours.Add(jointBehaviour);
        jointBehaviour.OnBehaviourConnected += OnChainedBehaviourConnection;

        if (!jointBehaviour.CollisionController)
        {
            jointBehaviour.AddCollisionController();
        }

        CheckSwitchKinematicIfNeeded(true);

        if (!IsGrabbed && jointBehaviour.IsGrabbed)
        {
            IsGrabbed = true;
        }

        jointBehaviour.CollisionController.SubscribeToJointControllerEvents(this);
        jointBehaviour.CollisionController.enabled = IsGrabbed;

        jointBehaviour.GrabStarted += JointBehaviourOnGrabStarted;
        jointBehaviour.GrabEnded += JointBehaviourOnGrabEnded;
    }

    public void MergeController(ChainedJointController otherController)
    {
        if (this == otherController)
        {
            return;
        }

        foreach (JointBehaviour behaviour in otherController._connectedBehaviours)
        {
            AddBehaviour(behaviour);
            behaviour.GrabStarted -= otherController.JointBehaviourOnGrabStarted;
            behaviour.GrabEnded -= otherController.JointBehaviourOnGrabEnded;
            behaviour.OnBehaviourConnected += otherController.OnChainedBehaviourConnection;
            behaviour.ChangeChainedController(this);
        }

        Destroy(otherController.gameObject);
    }

    public void BehaviourDisconnected(JointBehaviour sender, JointBehaviour disconnectedBehaviour)
    {
        SplitControllerByJoints(sender, disconnectedBehaviour);
    }

    private ChainedJointController SplitControllerByJoints(JointBehaviour first, JointBehaviour second)
    {
        if (first == second || first.ChainedJointController != second.ChainedJointController)
        {
            return null;
        }

        if (!_connectedBehaviours.Contains(first) || !_connectedBehaviours.Contains(second))
        {
            return null;
        }

        List<JointBehaviour> firstJoints = first.GetAllConnectedJoints();
        List<JointBehaviour> secondJoints = second.GetAllConnectedJoints();
        List<JointBehaviour> toDisconnect = firstJoints;

        if (secondJoints.Count < firstJoints.Count)
        {
            toDisconnect = secondJoints;
        }

        ChainedJointController newController = CreateNewChainedController();

        foreach (JointBehaviour behaviour in toDisconnect)
        {
            RemoveBehaviour(behaviour);
            behaviour.ChangeChainedController(newController);
            newController.AddBehaviour(behaviour);
        }

        CheckSwitchKinematicIfNeeded(false);
        newController.CheckSwitchKinematicIfNeeded(false);

        return newController;
    }

    public bool ContainsBehaviour(JointBehaviour behaviour) => _connectedBehaviours.Contains(behaviour);

    private void RemoveBehaviour(JointBehaviour jointBehaviour)
    {
        if (!_connectedBehaviours.Contains(jointBehaviour))
        {
            return;
        }

        _connectedBehaviours.Remove(jointBehaviour);
        jointBehaviour.GrabStarted -= JointBehaviourOnGrabStarted;
        jointBehaviour.GrabEnded -= JointBehaviourOnGrabEnded;

        if (jointBehaviour.CollisionController)
        {
            jointBehaviour.CollisionController.UnsubscribeFromJointControllerEvents(this);
            jointBehaviour.CollisionController.ForcedUnblock();
        }

        jointBehaviour.ResetKinematic();
        CheckSwitchKinematicIfNeeded(true);
        CheckSwitchKinematicIfNeeded(false);
    }

    public override void CollisionEnter(Collider other)
    {
        if (_frameIgnored.Contains(other))
        {
            return;
        }

        _frameIgnored.Add(other);

        OnCollisionEnterInvoke(other);
    }

    public override void CollisionExit(Collider other)
    {
        if (_frameIgnored.Contains(other))
        {
            return;
        }

        _frameIgnored.Remove(other);

        if (_frameIgnored.Count != 0)
        {
            return;
        }

        OnCollisionExitInvoke(other);
    }

    private void CheckSwitchKinematicIfNeeded(bool isForceNeeded)
    {
        StopAllCoroutines();

        if (isForceNeeded != _connectedBehaviours.Any(x => x.IsKinematic))
        {
            return;
        }

        foreach (JointBehaviour behaviour in _connectedBehaviours)
        {
            if (isForceNeeded)
            {
                behaviour.ForceKinematic();
            }
            else
            {
                behaviour.ResetKinematic();
            }
        }
    }

    private void JointBehaviourOnGrabEnded(JointBehaviour sender)
    {
        if (_connectedBehaviours.Any(x => x.CheckIfGrabbedByHand()))
        {
            return;
        }

        IsGrabbed = false;

        var jointListCopy = new List<JointBehaviour>(_connectedBehaviours);

        foreach (JointBehaviour jointBehaviour in _connectedBehaviours)
        {
            jointBehaviour.CollisionController.enabled = false;
        }

        foreach (JointBehaviour jointBehaviour in jointListCopy)
        {
            if (jointBehaviour != sender)
            {
                jointBehaviour.GrabEndTryConnect();
            }
        }

        if (ProjectData.GameMode == GameMode.Edit)
        {
            RemoveConstraintFromChained(sender);
        }
    }

    private void JointBehaviourOnGrabStarted(JointBehaviour sender)
    {
        IsGrabbed = true;

        foreach (JointBehaviour jointBehaviour in _connectedBehaviours)
        {
            if (!jointBehaviour.CollisionController)
            {
                jointBehaviour.AddCollisionController();
            }

            jointBehaviour.CollisionController.enabled = true;
        }

        if (!_connectedBehaviours.Any(x => x.IsNonForcedKinematic()))
        {
            foreach (JointBehaviour jointBehaviour in _connectedBehaviours)
            {
                jointBehaviour.ResetKinematic();
            }
        }

        if (ProjectData.GameMode == GameMode.Edit)
        {
            AddConstraintToChained(sender);
        }
    }

    private void AddConstraintToChained(JointBehaviour toConnectTo)
    {
        foreach (JointBehaviour chainedJoint in _connectedBehaviours)
        {
            if (chainedJoint == toConnectTo)
            {
                continue;
            }

            chainedJoint.AddConstraint();
        }
    }

    private void RemoveConstraintFromChained(JointBehaviour toConnectTo)
    {
        foreach (JointBehaviour chainedJoint in _connectedBehaviours)
        {
            if (chainedJoint == toConnectTo)
            {
                continue;
            }

            chainedJoint.RemoveConstraint();
        }

        toConnectTo.RemoveConstraint();
    }

    public void HideConnectionJoints()
    {
        _connectedBehaviours.ForEach(a => a.DestroyPreview());
        _connectedBehaviours.ForEach(a => a.CollisionController.JointExit(null, null));
    }

    public void DrawConnectionJoints()
    {
        _connectedBehaviours.ForEach(a => a.CollisionController.JointEnter(null, null));
    }


    public List<JointBehaviour> GetConnectedBehaviours()
    {
        return _connectedBehaviours;
    }
}