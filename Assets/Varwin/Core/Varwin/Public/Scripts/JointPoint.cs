using System;
using System.Collections.Generic;
using UnityEngine;

namespace Varwin.Public
{
    /// <inheritdoc />
    /// <summary>
    /// Point to join object class
    /// </summary>
    [Obsolete("Use SocketPoint or PlugPoint")]
    [RequireComponent(typeof(Collider))]
    public class JointPoint : MonoBehaviour
    {
        /// <summary>
        /// Is used to identify which JointPoints can connect to each other
        /// </summary>
        public string Key = "joint";

        /// <summary>
        /// List of points for connection
        /// </summary>
        public List<string> AcceptedKeys = new List<string>();

        public delegate void JointEnterHandler(JointPoint senderJoint, JointPoint nearJoint);

        public delegate void JointExitHandler(JointPoint senderJoint, JointPoint nearJoint);

        /// <summary>
        /// Another joint entered me
        /// </summary>
        public event JointEnterHandler OnJointEnter;

        /// <summary>
        /// Another joint exited me
        /// </summary>
        public event JointExitHandler OnJointExit;

        /// <summary>
        /// Is point free
        /// </summary>
        public bool IsFree { get; private set; }

        /// <summary>
        /// Is point locked
        /// </summary>
        public bool IsLocked { get; private set; }

        [Obsolete("IsForceLocked support will be discontinued soon. Use CanBeDisconnected instead.")]
        public bool IsForceLocked
        {
            get => !CanBeDisconnected;
            set => CanBeDisconnected = !value;
        }

        public bool CanBeDisconnected
        {
            get => _canBeDisconnected;
            set
            {
                _canBeDisconnected = value;
                SetOtherConnectedJointPointStatus(value);
            }
        }

        private void SetOtherConnectedJointPointStatus(bool value)
        {
            if (ConnectedJointPoint)
            {
                ConnectedJointPoint._canBeDisconnected = value;
            }
        }

        /// <summary>
        /// Connected Point
        /// </summary>
        public JointPoint ConnectedJointPoint { get; private set; }

        /// <summary>
        /// My joint behaviour
        /// </summary>
        public JointBehaviour JointBehaviour { get; private set; }

        /// <summary>
        /// Jointed wrapper
        /// </summary>
        private GameObject _connectedGameObject;

        private ConfigurableJoint _configurableJoint;
        private FixedJoint _fixedJoint;

        private Wrapper _connectedWrapper;
        private Collider _collider;

        [SerializeField]
        private bool _canBeDisconnected = true;

        public void Init(JointBehaviour jointBehaviour)
        {
            JointBehaviour = jointBehaviour;
            IsFree = true;
            _collider = GetComponent<Collider>();
            _collider.isTrigger = true;
        }

        /// <summary>
        /// Locks this and it's connected point, if present
        /// </summary>
        /// <param name="sender"></param>
        public void Lock(bool sender = true)
        {
            if (IsLocked || IsFree)
            {
                return;
            }

            IsLocked = true;

            if (_fixedJoint)
            {
                Destroy(_fixedJoint);
            }

            if (sender)
            {
                if (_connectedGameObject)
                {
                    Rigidbody jointConnectedBody = _connectedGameObject.GetComponent<Rigidbody>();

                    if (jointConnectedBody)
                    {
                        _configurableJoint = JointBehaviour.CreateJoint();
                        _configurableJoint.connectedBody = jointConnectedBody;
                    }
                }
            }

            if (ConnectedJointPoint)
            {
                ConnectedJointPoint.Lock(false);
            }
            else
            {
                UnLock();
            }
        }

        /// <summary>
        /// Removes joint from this point and it's connected point, if present
        /// </summary>
        private void UnLock()
        {
            if (!IsLocked)
            {
                return;
            }

            IsLocked = false;

            if (_configurableJoint)
            {
                Destroy(_configurableJoint);
            }

            if (ConnectedJointPoint && ConnectedJointPoint.IsLocked)
            {
                ConnectedJointPoint.UnLock();
            }
        }

        /// <summary>
        /// Connects chosen JointPoint to this, also connecting their JointBehaviours
        /// </summary>
        /// <param name="jointGameObject"></param>
        /// <param name="jointPoint"></param>
        /// <param name="sender"></param>
        public void Connect(GameObject jointGameObject, JointPoint jointPoint, bool sender = true)
        {
            if (!IsFree || !jointPoint)
            {
                return;
            }

            IsFree = false;

            if (!CanBeDisconnected || !jointPoint.CanBeDisconnected)
            {
                CanBeDisconnected = false;
                jointPoint.CanBeDisconnected = false;
            }

            _connectedGameObject = jointGameObject;
            IWrapperAware wrapperAware = _connectedGameObject.GetComponentInChildren<IWrapperAware>();

            if (wrapperAware != null)
            {
                _connectedWrapper = wrapperAware.Wrapper();
                JointBehaviour.AddConnectedJoint(_connectedWrapper, jointPoint.JointBehaviour);
            }

            if (sender)
            {
                Rigidbody jointConnectedBody = _connectedGameObject.GetComponent<Rigidbody>();

                if (jointConnectedBody && !jointConnectedBody.isKinematic)
                {
                    _fixedJoint = JointBehaviour.gameObject.AddComponent<FixedJoint>();
                    _fixedJoint.connectedBody = jointConnectedBody;
                    _fixedJoint.breakForce = 600f;
                    _fixedJoint.breakTorque = 600f;
                }
            }

            ConnectedJointPoint = jointPoint;
            ConnectedJointPoint.Connect(JointBehaviour.gameObject, this, false);

            jointPoint._collider.enabled = false;
            _collider.enabled = false;
        }

        /// <summary>
        /// Disconnects Point from one it's connected to, if present  
        /// </summary>
        public void Disconnect()
        {
            if (!ConnectedJointPoint)
            {
                return;
            }
            
            JointPoint previousConnected = ConnectedJointPoint;
            Wrapper previousWrapper = _connectedWrapper;
            
            Wrapper otherPreviousWrapper = previousConnected._connectedWrapper;
            JointBehaviour otherBehaviour = previousConnected.JointBehaviour;
            
            ResetConnectionState();
            
            previousConnected.ResetConnectionState();
            JointBehaviour.RemoveDisconnectedJoint(previousWrapper, otherBehaviour);
            otherBehaviour.RemoveDisconnectedJoint(otherPreviousWrapper, JointBehaviour);
        }

        private void ResetConnectionState()
        {
            if (IsFree)
            {
                return;
            }

            ConnectedJointPoint._collider.enabled = true;
            _collider.enabled = true;

            UnLock();
            IsFree = true;
            CanBeDisconnected = true;
            ConnectedJointPoint.CanBeDisconnected = true;

            ConnectedJointPoint = null;
            _connectedWrapper = null;
            _connectedGameObject = null;


            if (_fixedJoint)
            {
                Destroy(_fixedJoint);
            }

            Rigidbody behaviourBody = JointBehaviour.GetComponent<Rigidbody>();

            if (behaviourBody)
            {
                behaviourBody.WakeUp();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            JointPoint otherJointPoint = GetValidJoint(other);

            if (!otherJointPoint)
            {
                return;
            }

            if (!AcceptedKeys.Contains(otherJointPoint.Key) || !otherJointPoint.AcceptedKeys.Contains(Key))
            {
                return;
            }
            
            OnJointEnter?.Invoke(this, otherJointPoint);
        }

        private void OnTriggerExit(Collider other)
        {
            JointPoint otherJointPoint = other.GetComponent<JointPoint>();

            if (!otherJointPoint || !AcceptedKeys.Contains(otherJointPoint.Key) || otherJointPoint.IsLocked)
            {
                return;
            }

            if (ConnectedJointPoint && otherJointPoint != ConnectedJointPoint)
            {
                return;
            }

            if (!JointBehaviour || !otherJointPoint.JointBehaviour)
            {
                return;
            }

            if (JointBehaviour.IsTempConnectionCreated)
            {
                return;
            }

            Disconnect();
            UnLock();
            OnJointExit?.Invoke(this, otherJointPoint);
        }

        private JointPoint GetValidJoint(Collider other)
        {
            if (!IsFree || IsLocked)
            {
                return null;
            }

            var otherJointPoint = other.gameObject.GetComponent<JointPoint>();

            if (!otherJointPoint || !otherJointPoint.IsFree || otherJointPoint.IsLocked)
            {
                return null;
            }

            if (!AcceptedKeys.Contains(otherJointPoint.Key))
            {
                return null;
            }

            return otherJointPoint;
        }

        private void OnDestroy()
        {
            _connectedWrapper = null;
            _connectedGameObject = null;
            _collider = null;
            _configurableJoint = null;
            _fixedJoint = null;
        }
    }
}
