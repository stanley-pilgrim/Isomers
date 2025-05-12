using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using Varwin.Extension;
using Varwin.ObjectsInteractions;
using Varwin.Public;

namespace Varwin
{
    [Obsolete("Use sockets instead")]
    public class JointBehaviour : MonoBehaviour, IGrabbingAware
    {
        public static bool IsTempConnectionCreated;

        public bool IsGrabbed
        {
            get => _chainedController ? _chainedController.IsGrabbed : _isGrabbed;

            private set => _isGrabbed = value;
        }

        public CollisionController CollisionController
        {
            get
            {
                if (_collisionController)
                {
                    return _collisionController;
                }

                _collisionController = GetComponent<CollisionController>();

                return _collisionController ? _collisionController : null;
            }

            private set => _collisionController = value;
        }

        public bool AutoLock = true;
        public List<JointPoint> JointPoints;

        public bool Connecting => _isGrabbed && _nearJointBehaviour;

        public Wrapper Wrapper;

        public bool IsJoined => CountConnections > 0;
        public bool IsFree => CountConnections == 0;

        public bool IsNearJointPointHighlighted => _nearJointPoint;

        public bool ForceLock
        {
            get => IsAnyDisconnectBlocked();
            set => BlockDisconnectForConnections(value);
        }

        public bool IsKinematic => _rigidbody.isKinematic;
        public int CountConnections => JointPoints.Count(jointPoint => !jointPoint.IsFree);

        public ChainedJointController ChainedJointController
        {
            get => _chainedController;
            private set => _chainedController = value;
        }

        public delegate void ConnectedHandler();

        public delegate void DisconnectedHandler();

        public delegate void GrabEventHandler(JointBehaviour sender);

        public event ConnectedHandler OnConnect;
        public event DisconnectedHandler OnDisconnect;

        public event Action<JointBehaviour, JointBehaviour> OnBehaviourConnected;
        public event GrabEventHandler GrabStarted;
        public event GrabEventHandler GrabEnded;

        public event JointPoint.JointEnterHandler OnJointEnter;
        public event JointPoint.JointExitHandler OnJointExit;

        private GameObject _shownDrawConnectionObject;

        public List<JointBehaviour> ConnectedJointBehaviours = new List<JointBehaviour>();

        private readonly List<Wrapper> _connectedWrappers = new List<Wrapper>();

        private JointPoint _senderJointPoint;
        private JointPoint _nearJointPoint;
        private JointBehaviour _nearJointBehaviour;
        private Rigidbody _rigidbody;
        private RigidbodyConstraints _rigidbodyDefaultConstraints;
        private IHighlightComponent _highlightEffect;
        private Transform _parentOnStart;

        private bool _childMoved;
        private Transform _savedParentTransform;
        private Color _savedColor;

        [SerializeField]
        private ChainedJointController _chainedController;

        private CollisionController _collisionController;

        private bool _kinematicConstraint;
        private bool _kinematicConstraintPreviousState;
        private bool _forcedKinematicPreviousState;
        private bool _forcedKinematic;
        private bool _isGrabbed;

        private MeshVolumeCalculator _meshVolumeCalculator;
        public float MeshSize => _meshVolumeCalculator?.MeshSize ?? 0f;

        public GameObject PreviewObject;

        public void Init()
        {
            JointPoints = gameObject.GetComponentsInChildren<JointPoint>().ToList();

            foreach (JointPoint jointPoint in JointPoints)
            {
                jointPoint.Init(this);
                jointPoint.OnJointEnter += JointPointOnJointEnter;
                jointPoint.OnJointExit += JointPointOnJointExit;
            }

            _highlightEffect = GetComponent<IHighlightComponent>();

            if (_highlightEffect != null)
            {
                _savedColor = _highlightEffect.OutlineColor;
            }
        }

        private void Start()
        {
            RegisterInteractableEvents();

            IWrapperAware wrapperAware = gameObject.GetComponentInChildren<IWrapperAware>();
            Wrapper = wrapperAware != null ? wrapperAware.Wrapper() : new NullWrapper(gameObject);
            _rigidbody = gameObject.GetComponent<Rigidbody>();
            _parentOnStart = transform.parent;
            _rigidbodyDefaultConstraints = _rigidbody.constraints;

            OnGameModeChanged(ProjectData.GameMode);

            ProjectData.GameModeChanged += OnGameModeChanged;
            
            InitVolumeCalculator();

            PreviewObject = MeshPreviewBuilder.GetPreview(gameObject);
        }

        private void OnDestroy()
        {
            ProjectData.GameModeChanged -= OnGameModeChanged;
        }

        private void OnGameModeChanged(GameMode newGameMode)
        {
            _rigidbody.constraints = newGameMode == GameMode.Edit ? RigidbodyConstraints.FreezeAll : _rigidbodyDefaultConstraints;
        }

        /// <summary>
        /// Mapped to intractable object
        /// </summary>
        public void OnGrabStart()
        {
            GrabStartTryDisconnect();
            GrabStarted?.Invoke(this);
            
            if (_nearJointPoint && _senderJointPoint)
            {
                JointPointOnJointEnter(_senderJointPoint, _nearJointPoint);
            }
        }

        private void GrabStartTryDisconnect()
        {
            DisconnectJointChain(this);

            _isGrabbed = true;

            JointBehaviour connectedGrabbedBehaviour =
                ConnectedJointBehaviours.Find(x => x._isGrabbed && !x.IsDisconnectBlockedWith(this));

            if (connectedGrabbedBehaviour)
            {
                UnlockDisconnectBehaviour(connectedGrabbedBehaviour);

                return;
            }

            var connectedKinematics =
                ConnectedJointBehaviours.FindAll(x =>
                    x._rigidbody.isKinematic && !x._forcedKinematic && !x.IsDisconnectBlockedWith(this));

            if (connectedKinematics.Count == 0 && ConnectedJointBehaviours.Count == 1 && ConnectedJointBehaviours[0]._rigidbody.isKinematic && !ConnectedJointBehaviours[0].IsDisconnectBlockedWith(this))
            {
                connectedKinematics.Add(ConnectedJointBehaviours[0]);
            }

            if (!connectedGrabbedBehaviour && connectedKinematics.Count == 1)
            {
                UnlockDisconnectBehaviour(connectedKinematics[0]);

                return;
            }

            if (!_chainedController && CollisionController)
            {
                CollisionController.enabled = true;
            }

            transform.parent = _parentOnStart;
        }

        private static void DisconnectJointChain(JointBehaviour behaviour, HashSet<JointBehaviour> checkedBehaviours = null)
        {
            if (checkedBehaviours == null)
            {
                checkedBehaviours = new HashSet<JointBehaviour>();
            }

            checkedBehaviours.Add(behaviour);

            foreach (JointPoint point in behaviour.JointPoints)
            {
                if (!point.ConnectedJointPoint)
                {
                    continue;
                }

                JointBehaviour connectedBehaviour = point.ConnectedJointPoint.JointBehaviour;

                // TODO: проверка RigidbodyConstraints это костыль, чтобы исправить граб сложных конструкций из джоинтов в уралхиме.
                // При проработке правильного поведения граба сложных конструкций вместо RigidbodyConstraints надо будет использовать isKinematic и/или CanBeDisconnected
                if (connectedBehaviour._rigidbody.constraints == RigidbodyConstraints.FreezeAll)
                {
                    point.ConnectedJointPoint.Disconnect();

                    continue;
                }

                if (!checkedBehaviours.Contains(connectedBehaviour))
                {
                    DisconnectJointChain(connectedBehaviour, checkedBehaviours);
                }
            }
        }

        /// <summary>
        /// Mapped to intractable object
        /// </summary>
        public void OnGrabEnd()
        {
            GrabEndTryConnect();
            GrabEnded?.Invoke(this);
        }

        public void GrabEndTryConnect()
        {
            _isGrabbed = false;

            bool wasConnected = false;

            if (_nearJointBehaviour)
            {
                wasConnected = _nearJointBehaviour.IsGrabbed;
                wasConnected = _nearJointBehaviour.ConnectCandidate() && wasConnected;
                _nearJointBehaviour.ResetConnecting();
            }

            ResetConnecting();

            HideConnectionJoint();

            if (wasConnected)
            {
                return;
            }

            if (!_chainedController && CollisionController)
            {
                CollisionController.enabled = false;
            }
        }

        /// <summary>
        /// Mapped to intractable object
        /// </summary>
        public void OnTouchStart()
        {
            SetValidHighLightColor();
        }

        /// <summary>
        /// Mapped to intractable object
        /// </summary>
        public void OnTouchEnd()
        {
            SetDefaultHighLight();
        }

        public ConfigurableJoint CreateJoint()
        {
            ConfigurableJoint configurableJoint = gameObject.AddComponent<ConfigurableJoint>();
            configurableJoint.xMotion = ConfigurableJointMotion.Locked;
            configurableJoint.yMotion = ConfigurableJointMotion.Locked;
            configurableJoint.zMotion = ConfigurableJointMotion.Locked;
            configurableJoint.angularXMotion = ConfigurableJointMotion.Locked;
            configurableJoint.angularYMotion = ConfigurableJointMotion.Locked;
            configurableJoint.angularZMotion = ConfigurableJointMotion.Locked;
            configurableJoint.projectionMode = JointProjectionMode.PositionAndRotation;
            configurableJoint.projectionDistance = 0;
            configurableJoint.projectionAngle = 0;

            return configurableJoint;
        }

        public ParentConstraint CreateConstraint() => gameObject.AddComponent<ParentConstraint>();

        /// <summary>
        /// Unlocks all connected JointPoints
        /// <para>Calls Unlock() method on all behaviour's JointPoints, effectively disjoining all connected behaviours thus allowing their joints to be broken by force</para>
        /// </summary>
        public void UnLockAndDisconnectPoints()
        {
            foreach (JointPoint jointPoint in JointPoints)
            {
                jointPoint.Disconnect();
            }
        }


        /// <summary>
        /// Connect chosen Behaviour to this, allowing Wrappers to exchange data 
        /// </summary>
        /// <param name="wrapper">Wrapper to connect</param>
        /// <param name="jointBehaviour">Behaviour to connect</param>
        public void AddConnectedJoint(Wrapper wrapper, JointBehaviour jointBehaviour)
        {
            if (_connectedWrappers.Contains(wrapper) || ConnectedJointBehaviours.Contains(jointBehaviour))
            {
                return;
            }

            _connectedWrappers.Add(wrapper);
            ConnectedJointBehaviours.Add(jointBehaviour);
        }

        /// <summary>
        /// Unlocks and disconnects all JointPoints connected to chosen behaviour
        /// </summary>
        /// <param name="behaviour">Behaviour to unlock and disconnect from</param>
        public void UnlockDisconnectBehaviour(JointBehaviour behaviour)
        {
            foreach (JointPoint jointPoint in JointPoints.Where(jointPoint =>
                jointPoint.ConnectedJointPoint && jointPoint.ConnectedJointPoint.JointBehaviour.Equals(behaviour)))
            {
                jointPoint.Disconnect();
            }
        }

        /// <summary>
        /// Disconnects chosen Behaviour and removes it's Wrapper from connected
        /// </summary>
        /// <param name="wrapper">Wrapper to remove</param>
        /// <param name="jointBehaviour">Behaviour to disconnect</param>
        public void RemoveDisconnectedJoint(Wrapper wrapper, JointBehaviour jointBehaviour)
        {
            if (!_connectedWrappers.Contains(wrapper))
            {
                return;
            }

            _senderJointPoint = null;
            _nearJointBehaviour = null;
            _nearJointPoint = null;

            _connectedWrappers.Remove(wrapper);
            ConnectedJointBehaviours.Remove(jointBehaviour);

            SetDefaultHighLight();
            OnDisconnect?.Invoke();


            if (!_chainedController)
            {
                return;
            }

            _chainedController.BehaviourDisconnected(this, jointBehaviour);

            RemoveConstraint();
        }

        public List<Wrapper> GetConnectedWrappers() => _connectedWrappers;

        /// <summary>
        /// Makes a connection between two JointPoints
        /// </summary>
        /// <param name="myPoint">caller's JointPoint to connect</param>
        /// <param name="otherPoint">other Behaviour's JointPoint to connect</param>
        public void ConnectToJointPoint(JointPoint myPoint, JointPoint otherPoint)
        {
            if (otherPoint.JointBehaviour._rigidbody.isKinematic)
            {
                TransformToJoint(gameObject, myPoint, otherPoint);
            }
            else
            {
                TransformToJoint(otherPoint.JointBehaviour.gameObject, otherPoint, myPoint);
            }

            otherPoint.Connect(gameObject, myPoint);
            OnConnect?.Invoke();
            otherPoint.JointBehaviour.OnConnect?.Invoke();

            OnBehaviourConnected?.Invoke(this, otherPoint.JointBehaviour);
            otherPoint.JointBehaviour.OnBehaviourConnected?.Invoke(otherPoint.JointBehaviour, this);
            
            if (AutoLock)
            {
                otherPoint.Lock();
            }

            AddBehavioursToChainedController(otherPoint.JointBehaviour);
        }

        /// <summary>
        /// Saves all connected Behaviours' Transform.parent and parents them to caller  
        /// </summary>
        public void MakeConnectionsChild()
        {
            if (_childMoved)
            {
                return;
            }

            foreach (JointPoint jointPoint in JointPoints.Where(jointPoint => !jointPoint.IsFree))
            {
                _savedParentTransform = jointPoint.ConnectedJointPoint.JointBehaviour.gameObject.transform.parent;
                jointPoint.ConnectedJointPoint.JointBehaviour.gameObject.transform.SetParent(gameObject.transform);
                _childMoved = true;
                jointPoint.ConnectedJointPoint.JointBehaviour.MakeConnectionsChild();
            }
        }

        /// <summary>
        /// Restores connected Behaviours' Transform.parent
        /// </summary>
        public void RestoreParents()
        {
            if (!_childMoved)
            {
                return;
            }

            foreach (JointPoint jointPoint in JointPoints.Where(jointPoint => !jointPoint.IsFree))
            {
                jointPoint.ConnectedJointPoint.JointBehaviour.gameObject.transform.SetParent(_savedParentTransform);
                _childMoved = false;
                jointPoint.ConnectedJointPoint.JointBehaviour.RestoreParents();
            }
        }

        /// <summary>
        /// Get list of connected wrappers from all joint points
        /// </summary>
        /// <param name="sender"></param>
        /// <returns>List order by me</returns>
        public List<Wrapper> GetAllConnectedWrappers(List<Wrapper> sender = null)
        {
            var result = sender ?? new List<Wrapper> {Wrapper};

            foreach (JointPoint jointPoint in JointPoints)
            {
                if (jointPoint.IsFree)
                {
                    continue;
                }

                Wrapper candidate = jointPoint.ConnectedJointPoint.JointBehaviour.Wrapper;

                if (result.Contains(candidate))
                {
                    continue;
                }

                result.Add(candidate);
                jointPoint.ConnectedJointPoint.JointBehaviour.GetAllConnectedWrappers(result);
            }


            return result;
        }

        /// <summary>
        /// Get list of connected wrappers from joint point
        /// </summary>
        /// <param name="jointPoint">Target joint point</param>
        /// <param name="sender"></param>
        /// <returns>List order by me</returns>
        public List<Wrapper> GetAllConnectedWrappers(JointPoint jointPoint, List<Wrapper> sender = null)
        {
            if (jointPoint == null)
            {
                return null;
            }

            var result = sender ?? new List<Wrapper>() {Wrapper};

            if (jointPoint.IsFree)
            {
                return result;
            }

            Wrapper candidate = jointPoint.ConnectedJointPoint.JointBehaviour.Wrapper;
            result.Add(candidate);
            jointPoint.ConnectedJointPoint.JointBehaviour.GetAllConnectedWrappers(result);

            return result;
        }

        /// <summary>
        /// Returns List of all JointBehaviours that are connected to this and appends the result to sender if any
        /// </summary>
        /// <param name="sender">List of behaviours to append with the current results</param>
        /// <returns>List of all connected behaviours, including this</returns>
        public List<JointBehaviour> GetAllConnectedJoints(List<JointBehaviour> sender = null)
        {
            var result = sender ?? new List<JointBehaviour> {this};

            foreach (JointPoint jointPoint in JointPoints)
            {
                if (jointPoint.IsFree)
                {
                    continue;
                }

                JointBehaviour candidate = jointPoint.ConnectedJointPoint.JointBehaviour;

                if (result.Contains(candidate))
                {
                    continue;
                }

                result.Add(candidate);
                jointPoint.ConnectedJointPoint.JointBehaviour.GetAllConnectedJoints(result);
            }

            return result;
        }

        //I dunno where this method is used at all, yet, as it is public, I cannot freely remove it
        public List<JointBehaviour> GetAllConnectedJoints(JointPoint jointPoint, List<JointBehaviour> sender = null)
        {
            if (jointPoint == null)
            {
                return null;
            }

            var result = sender ?? new List<JointBehaviour>() {this};

            if (jointPoint.IsFree)
            {
                return result;
            }

            JointBehaviour candidate = jointPoint.ConnectedJointPoint.JointBehaviour;
            result.Add(candidate);
            jointPoint.ConnectedJointPoint.JointBehaviour.GetAllConnectedJoints(result);

            return result;
        }

        /// <summary>
        /// Locks all connected JointPoints
        /// <para>Calls Lock() method on all behaviour's JointPoints, effectively joining all connected behaviours thus preventing their joints from being broken by force</para>
        /// </summary>
        public void LockPoints()
        {
            foreach (JointPoint jointPoint in JointPoints)
            {
                jointPoint.Lock();
            }
        }

        public CollisionController AddCollisionController()
        {
            if (CollisionController)
            {
                return CollisionController;
            }

            _collisionController = gameObject.AddComponent<CollisionController>();
            _collisionController.InitializeController(gameObject.GetRootInputController());

            return _collisionController;
        }

        /// <summary>
        /// Данный метод реализует передачу инерции движения руки всем объектам в цепи.
        /// </summary>
        /// <param name="rigidbody">Взятое в руку тело.</param>
        public void OnHandTransformChanged(Vector3 angularVelocity, Vector3 velocity)
        {
            var chainedJointController = ChainedJointController;

            if (!chainedJointController)
            {
                return;
            }

            var listConnected = chainedJointController.GetConnectedBehaviours();

            listConnected.ForEach(a =>
            {
                var jointBehaviourRigidbody = a.GetComponent<Rigidbody>();

                if (jointBehaviourRigidbody == _rigidbody)
                {
                    return;
                }

                if (jointBehaviourRigidbody)
                {
                    jointBehaviourRigidbody.velocity = _rigidbody.GetPointVelocity(jointBehaviourRigidbody.transform.position);
                }
            });
        }
        
        public void MoveAndRotate(Vector3 position, Quaternion rotation)
        {
            // TODO: переделать с SetParent на ручной расчет
            // надо сначала все точки передвинуть на ту дельту, которую перемещаем парент-объект, далее надо повернуть объект,
            // высчитать axis дельты поворота, высчитать угол поворота по этой axis, а дальше применить ко всем этим объектам transform.RotateAround с нужным аргументами

            var allConnectedJoints = GetAllConnectedJoints();
            var connectedJointsParents = new Dictionary<JointBehaviour, Transform>();

            foreach (var connectedJoint in allConnectedJoints)
            {
                if (connectedJoint == this)
                {
                    continue;
                }

                connectedJointsParents.Add(connectedJoint, connectedJoint.transform.parent);
                connectedJoint.transform.SetParent(transform, true);
            }

            transform.position = position;
            transform.rotation = rotation;

            foreach (var connectedJoint in connectedJointsParents)
            {
                connectedJoint.Key.transform.SetParent(connectedJoint.Value, true);
            }

            var rigidbodies = allConnectedJoints.Select(x => x._rigidbody);

            foreach (var jointRigidbody in rigidbodies)
            {
                jointRigidbody.velocity = Vector3.zero;
                jointRigidbody.angularVelocity = Vector3.zero;
            }
        }

        private void SetValidHighLightColor()
        {
            if (CountConnections == 0)
            {
                return;
            }

            if (_highlightEffect == null)
            {
                return;
            }

            if (CountConnections != 1 || ForceLock)
            {
                _highlightEffect.OutlineColor = Color.red;
            }
            else
            {
                _highlightEffect.OutlineColor = Color.green;
            }
        }

        private void SetDefaultHighLight()
        {
            var highlightEffect = GetComponent<IHighlightComponent>();
            if (highlightEffect != null)
            {
                highlightEffect.OutlineColor = _savedColor;
            }
        }

        public void RegisterInteractableEvents()
        {
            var interactableObjectBehaviour = gameObject.GetComponent<InteractableObjectBehaviour>();

            if (!interactableObjectBehaviour)
            {
                interactableObjectBehaviour = gameObject.GetComponentInParent<InteractableObjectBehaviour>();
            }

            if (!interactableObjectBehaviour)
            {
                interactableObjectBehaviour = gameObject.GetComponentInChildren<InteractableObjectBehaviour>(true);
            }

            if (!interactableObjectBehaviour)
            {
                Debug.LogError("IntractableObjectBehaviour cannot be found.", this);

                return;
            }

            interactableObjectBehaviour.OnGrabStarted.AddListener(OnGrabStart);
            interactableObjectBehaviour.OnGrabEnded.AddListener(OnGrabEnd);
            interactableObjectBehaviour.OnTouchStarted.AddListener(OnTouchStart);
            interactableObjectBehaviour.OnTouchEnded.AddListener(OnTouchEnd);
        }

        public void ChangeChainedController(ChainedJointController controller)
        {
            _chainedController = controller;
        }

        public bool CheckIfConnectedTo(JointBehaviour other)
        {
            if (other == this)
            {
                //Not really sure whether that will shoot at my leg or not
                return true;
            }

            if (!_chainedController)
            {
                return ConnectedJointBehaviours.Contains(other);
            }

            return _chainedController.ContainsBehaviour(other);
        }

        public void AddConstraint()
        {
            if (_kinematicConstraint)
            {
                return;
            }

            _kinematicConstraintPreviousState = _rigidbody.isKinematic;
            _rigidbody.isKinematic = false;
            _kinematicConstraint = true;
        }

        public void RemoveConstraint()
        {
            if (!_kinematicConstraint)
            {
                return;
            }

            _rigidbody.isKinematic = _kinematicConstraintPreviousState;
            _kinematicConstraint = false;
        }

        public void ForceKinematic()
        {
            if (!_rigidbody)
            {
                return;
            }

            if (_forcedKinematic)
            {
                return;
            }

            if (_rigidbody.isKinematic)
            {
                return;
            }

            _forcedKinematic = true;
            _forcedKinematicPreviousState = _rigidbody.isKinematic;
            _rigidbody.isKinematic = true;
        }

        public void ResetKinematic()
        {
            if (!_forcedKinematic)
            {
                return;
            }

            _forcedKinematic = false;
            _rigidbody.isKinematic = _forcedKinematicPreviousState;
        }

        public bool IsNonForcedKinematic() => _rigidbody.isKinematic && !_forcedKinematic;

        public bool CheckIfGrabbedByHand() => _isGrabbed;

        public void ResetConnecting()
        {
            _nearJointBehaviour = null;
            _senderJointPoint = null;
            _nearJointPoint = null;
        }

        private void JointPointOnJointExit(JointPoint senderJoint, JointPoint nearJoint)
        {
            HideConnectionJoint();
            ResetConnecting();

            OnJointExit?.Invoke(senderJoint, nearJoint);
        }

        private void JointPointOnJointEnter(JointPoint senderJoint, JointPoint nearJoint)
        {
            var senderJointSize = senderJoint.JointBehaviour._chainedController?.MeshSize ?? senderJoint.JointBehaviour.MeshSize;
            var nearJointSize = nearJoint.JointBehaviour._chainedController?.MeshSize ?? nearJoint.JointBehaviour.MeshSize;

            if (senderJointSize < nearJointSize)
            {
                var temp = senderJoint;
                senderJoint = nearJoint;
                nearJoint = temp;
            }

            if (nearJoint.JointBehaviour._rigidbody.isKinematic)
            {
                var temp = senderJoint;
                senderJoint = nearJoint;
                nearJoint = temp;
            }

            _senderJointPoint = senderJoint;
            _nearJointPoint = nearJoint;
            _nearJointBehaviour = nearJoint.JointBehaviour == this? _senderJointPoint.JointBehaviour : nearJoint.JointBehaviour;

            if (!IsGrabbed || !senderJoint.IsFree || !nearJoint.IsFree)
            {
                return;
            }

            DrawConnectionJoint(nearJoint, senderJoint);

            OnJointEnter?.Invoke(senderJoint, nearJoint);
        }

        private void DrawConnectionJoint(JointPoint senderJoint, JointPoint nearJoint)
        {
            if (_shownDrawConnectionObject == null)
            {
                _shownDrawConnectionObject = MeshPreviewBuilder.GetJointPreview(senderJoint.JointBehaviour);
            }

            TransformToJoint(_shownDrawConnectionObject, senderJoint, nearJoint);
            _shownDrawConnectionObject.transform.SetParent(nearJoint.transform);
            _shownDrawConnectionObject.SetActive(true);
            
            var highlighter = _shownDrawConnectionObject.GetComponent<Highlighter>();
            if (highlighter)
            {
                highlighter.IsEnabled = true;
            }
            
            if (_chainedController)
            {
                _chainedController.DrawConnectionJoints();
            }
        }

        private static void TransformToJoint(GameObject go, JointPoint nearJointPoint, JointPoint senderJoint)
        {
            Transform saveTransform = go.transform.parent;

            Transform var = nearJointPoint.transform;
            GameObject temp = new GameObject("temp");
            temp.transform.position = var.position;
            temp.transform.rotation = var.rotation;

            Transform nearPointTransform = nearJointPoint.JointBehaviour.transform;
            go.transform.position = nearPointTransform.position;
            go.transform.rotation = nearPointTransform.rotation;
            go.transform.SetParent(temp.transform, true);

            Transform senderJointTransform = senderJoint.transform;
            temp.transform.position = senderJointTransform.position;
            temp.transform.rotation = senderJointTransform.rotation;
            temp.transform.Rotate(new Vector3(180, 0, 0));

            go.transform.SetParent(saveTransform, true);
            Destroy(temp);
        }

        private void HideConnectionJoint()
        {
            if (_chainedController)
            {
                _chainedController.HideConnectionJoints();
                return;
            }

            DestroyPreview();
        }

        public void DestroyPreview()
        {
            Destroy(_shownDrawConnectionObject);
        }

        private bool ConnectCandidate()
        {
            if (_nearJointPoint == null || _senderJointPoint == null)
            {
                return false;
            }

            Dictionary<Collider, bool> objectCollidersStates;
            
            if (_nearJointPoint.JointBehaviour._rigidbody.isKinematic)
            {
                objectCollidersStates = GetObjectColliderStates(_senderJointPoint);
                TransformToJoint(_senderJointPoint.JointBehaviour.gameObject, _senderJointPoint, _nearJointPoint);
            }
            else
            {
                objectCollidersStates = GetObjectColliderStates(_nearJointPoint);
                TransformToJoint(_nearJointPoint.JointBehaviour.gameObject, _nearJointPoint, _senderJointPoint);
            }

            _senderJointPoint.Connect(_nearJointPoint.JointBehaviour.gameObject, _nearJointPoint);

            if (AutoLock)
            {
                _senderJointPoint.Lock();
            }

            AddBehavioursToChainedController(_nearJointBehaviour != this ? _nearJointBehaviour : _senderJointPoint.JointBehaviour);

            OnConnect?.Invoke();
            _nearJointPoint.JointBehaviour.OnConnect?.Invoke();
            OnBehaviourConnected?.Invoke(this, _nearJointBehaviour);
            _nearJointBehaviour.OnBehaviourConnected?.Invoke(_nearJointBehaviour, this);

            foreach (var collider in objectCollidersStates.Keys)
            {
                if (collider)
                {
                    collider.enabled = objectCollidersStates[collider];
                }
            }

            return true;
        }

        private Dictionary<Collider, bool> GetObjectColliderStates(JointPoint senderJointPoint)
        {
            var objectCollidersStates = new Dictionary<Collider, bool>();
            var colliders = senderJointPoint.JointBehaviour.gameObject.GetComponentsInChildren<Collider>();
            foreach (var collider in colliders)
            {
                objectCollidersStates.Add(collider, collider.enabled);
                collider.enabled = false;
            }

            return objectCollidersStates;
        }

        private void CreateChainedControllerIfNeeded()
        {
            if (_chainedController)
            {
                return;
            }

            var chainedController = ChainedJointController.CreateNewChainedController();
            chainedController.AddBehaviour(this);
            _chainedController = chainedController;
        }

        private void AddBehavioursToChainedController(JointBehaviour other)
        {
            if (!other._chainedController)
            {
                CreateChainedControllerIfNeeded();

                SetChainedController(other, _chainedController);
            }
            else
            {
                if (_chainedController)
                {
                    if (_chainedController.ConnectedBehaviours > other._chainedController.ConnectedBehaviours)
                    {
                        _chainedController.MergeController(other._chainedController);
                    }
                    else
                    {
                        other._chainedController.MergeController(_chainedController);
                    }
                }
                else
                {
                    SetChainedController(this, other._chainedController);
                }
            }
        }

        private void SetChainedController(JointBehaviour behaviour, ChainedJointController controller)
        {
            behaviour._chainedController = controller;
            controller.AddBehaviour(behaviour);
        }

        private bool IsAnyDisconnectBlocked()
        {
            return JointPoints.Any(jointPoint => !jointPoint.CanBeDisconnected);
        }

        private bool IsDisconnectBlockedWith(JointBehaviour other)
        {
            return JointPoints.Any(jointPoint =>
                !jointPoint.CanBeDisconnected
                && !jointPoint.IsFree
                && jointPoint.ConnectedJointPoint.JointBehaviour.Equals(other));
        }

        private void BlockDisconnectForConnections(bool value)
        {
            SetValidHighLightColor();

            foreach (JointPoint point in JointPoints.Where(point => !point.IsFree))
            {
                point.CanBeDisconnected = !value;
                point.ConnectedJointPoint.CanBeDisconnected = !value;
                point.ConnectedJointPoint.JointBehaviour.SetValidHighLightColor();
            }
        }
        
        private void InitVolumeCalculator()
        {
            _meshVolumeCalculator = GetComponent<MeshVolumeCalculator>();

            if (!_meshVolumeCalculator)
            {
                _meshVolumeCalculator = gameObject.AddComponent<MeshVolumeCalculator>();
            }
        }
    }
}