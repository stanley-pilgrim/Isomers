using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using Varwin.Models.Data;
using Varwin.PlatformAdapter;
using Object = UnityEngine.Object;

namespace Varwin
{
    public class HierarchyController
    {
        private const string GhostHierarchyObjectPostfix = "_GhostHierarchyObject";

        /// <summary>
        /// Sort order index
        /// </summary>
        public int Index { get; set; }

        public HierarchyController Parent { get; private set; }

        public bool LockChildren
        {
            get => _isLockChildren;
            set => SetLock(value);
        }

        public HashSet<HierarchyController> Descendants
        {
            get
            {
                var result = new HashSet<HierarchyController>();
                AllHierarchyChildren(result);

                return result;
            }
        }

        public bool TreeExpandedState { get; set; }

        private HierarchyController _lockParent;

        public HierarchyController LockParent
        {
            get
            {
                _lockParent = this;
                while (_lockParent.Parent != null && _lockParent.Parent.LockChildren)
                {
                    _lockParent = _lockParent.Parent;
                }

                return _lockParent;
            }
            private set => _lockParent = value;
        }

        public int ParentId => Parent?.ObjectController.Id ?? 0;

        public List<HierarchyController> Children
        {
            get
            {
                _children.Sort((x, y) => x.Index.CompareTo(y.Index));
                return _children;
            }
        }

        public readonly ObjectController ObjectController;

        private readonly List<HierarchyController> _children = new List<HierarchyController>();
        private Transform _ghostHierarchyTransform;
        private bool _isLockChildren;
        private GameObject _gameObject => ObjectController.gameObject;
        private Rigidbody _rigidBody => ObjectController.RigidBody;

        public PositionConstraint PositionConstraint { get; private set; }
        public RotationConstraint RotationConstraint{ get; private set; }
        public ScaleConstraint ScaleConstraint { get; private set; }
        public ParentConstraint JointConstraint { get; private set; }

        public ConfigurableJoint Joint;
        
        private Vector3 _localPosition;
        private Vector3 _localEulerAngles;
        private Vector3 _localScale;

        public HierarchyController(ObjectController selfController)
        {
            ObjectController = selfController;
        }

        ~HierarchyController()
        {
            Destroy();
        }

        public void Destroy()
        {
            DestroyGhostObject();
        }

        public Transform GetAffectedTransform()
        {
            return _ghostHierarchyTransform ? _ghostHierarchyTransform : _gameObject.transform;
        }

        private void SetLock(bool value)
        {
            _isLockChildren = value;

            foreach (HierarchyController child in Children)
            {
                child.SetLock(value);
            }
        }

        public void UpdateConstraintsForPlayMode()
        {
            SetHierarchyConstraintsActive(false);

            if (!JointConstraint)
            {
                JointConstraint = _gameObject.GetComponent<ParentConstraint>();
            }
            
            if (!JointConstraint)
            {
                JointConstraint = _gameObject.AddComponent<ParentConstraint>();
            }

            if (!LockChildren || LockParent == this || ObjectController.IsVirtualObject)
            {
                JointConstraint.constraintActive = false;
                return;
            }

            SetJointConstraint(Parent.ObjectController.RootGameObject.transform);

            if (_ghostHierarchyTransform)
            {
                _gameObject.transform.localPosition = _ghostHierarchyTransform.position;
                _gameObject.transform.localRotation = _ghostHierarchyTransform.rotation;
                _gameObject.transform.localScale = _ghostHierarchyTransform.lossyScale;
            }

            var parentRigidbody = LockParent._gameObject.GetComponent<Rigidbody>();
            if (parentRigidbody.isKinematic)
            {
                ObjectController.SetKinematicsOn();
            }
            else
            {
                ReplaceConstraintWithJoint(parentRigidbody);
            }
        }

        #region Parenting

        public void SetParent(HierarchyController parent, int index, bool keepOriginalScale = false)
        {
            SetParentWithoutNotify(parent, index, keepOriginalScale);

            ObjectController.InvokeParentChangedEvent();
        }

        public void SetParentWithoutNotify(HierarchyController parent, int index, bool keepOriginalScale)
        {
            RemoveParent();

            Parent = parent;
            if (Parent != null)
            {
                Parent.AddChild(this, index);
                Parent.InitGhostHierarchyObjects(this, keepOriginalScale);
                ObjectController.Entity.ReplaceIdParent(Parent.ObjectController.Id);
                if (Parent.LockChildren)
                {
                    LockChildren = Parent.LockChildren;
                }
            }
            else
            {
                LockParent = null;
                AddChildToRoot(this, index);

                if (ObjectController.Entity.hasIdParent)
                {
                    ObjectController.Entity.RemoveIdParent();
                }
            }
        }

        public HierarchyController GetRootParent()
        {
            HierarchyController parent = Parent ?? this;

            while (parent.Parent != null)
            {
                parent = parent.Parent;
            }

            return parent;
        }

        public void RemoveParent()
        {
            if (Parent != null)
            {
                Parent.RemoveChild(this);
            }
            else
            {
                RemoveChildFromRoot();
            }
        }

        private void RemoveChild(HierarchyController child)
        {
            if (!Children.Contains(child))
            {
                return;
            }

            RemoveGhostChild(child);
            _children.Remove(child);
            AdjustIndexes(_children);
        }

        private void RemoveChildFromRoot()
        {
            Index = -1;
            AdjustIndexes(ObjectController.GetRootObjectsInScene());
        }

        private static void AdjustIndexes(List<HierarchyController> controllers)
        {
            var children = controllers.Where(x => x.Index >= 0).ToList();
            for (int i = 0; i < children.Count; i++)
            {
                children[i].Index = i;
            }
        }

        private void AddChild(HierarchyController child, int index)
        {
            if (Children.Contains(child))
            {
                return;
            }

            InsertChild(_children, child, index);
            _children.Add(child);
        }

        private static void AddChildToRoot(HierarchyController child, int index)
        {
            InsertChild(ObjectController.GetRootObjectsInScene(), child, index);
        }

        private static void InsertChild(List<HierarchyController> children, HierarchyController childToInsert, int index)
        {
            int lastIndex = children.Count == 0 ? 1 : children.Max(x => x.Index) + 1;
            if (index == -1 || index == lastIndex)
            {
                childToInsert.Index = lastIndex;
                return;
            }

            bool found = false;
            foreach (HierarchyController child in children)
            {
                if (child.Index == index)
                {
                    found = true;
                }

                if (found)
                {
                    child.Index++;
                }
            }

            childToInsert.Index = index;
        }

        private void AllHierarchyChildren(HashSet<HierarchyController> result)
        {
            var uniqueChildren = _children.Where(result.Add);
            foreach (HierarchyController child in uniqueChildren)
            {
                child.AllHierarchyChildren(result);
            }
        }

        #endregion

        #region Ghost Hierarchy

        public void UpdateTransformManually()
        {
            if (!_ghostHierarchyTransform)
            {
                return;
            }

            _ghostHierarchyTransform.CopyToTransform(_gameObject.transform);

            if (Children == null)
            {
                return;
            }

            foreach (var child in Children)
            {
                child.UpdateTransformManually();
            }
        }

        private void RemoveGhostChild(HierarchyController child)
        {
            if (child._ghostHierarchyTransform)
            {
                child._ghostHierarchyTransform.SetParent(null, true);
                child.DestroyEmptyGhost();
            }

            DestroyEmptyGhost();
        }

        public void DestroyGhostObject()
        {
            if (!_ghostHierarchyTransform)
            {
                return;
            }

            if (PositionConstraint)
            {
                Object.DestroyImmediate(PositionConstraint);
                PositionConstraint = null;
            }

            if (RotationConstraint)
            {
                Object.DestroyImmediate(RotationConstraint);
                RotationConstraint = null;
            }

            if (ScaleConstraint)
            {
                Object.DestroyImmediate(ScaleConstraint);
                ScaleConstraint = null;
            }

            if (_gameObject)
            {
                _ghostHierarchyTransform.CopyToTransform(_gameObject.transform);
            }
            Object.DestroyImmediate(_ghostHierarchyTransform.gameObject);
            _ghostHierarchyTransform = null;
            _children.Clear();

            if (Parent)
            {
                Parent = null;
            }
        }

        private void DestroyEmptyGhost()
        {
            if (!_ghostHierarchyTransform)
            {
                return;
            }

            if (_ghostHierarchyTransform.childCount == 0 && !_ghostHierarchyTransform.parent)
            {
                DestroyGhostObject();
            }
        }

        private void InitGhostHierarchyObjects(HierarchyController child, bool keepOriginalScale = false)
        {
            Vector3 storedScale = child._gameObject.transform.localScale;
            
            if (ProjectData.GameMode == GameMode.Preview || ProjectData.GameMode == GameMode.View)
            {
                var oldParent = child._gameObject.transform.parent; 
                child._gameObject.transform.parent = _gameObject.transform;
                child._gameObject.transform.localScale = storedScale;
                child._gameObject.transform.parent = oldParent;
                return;
            }

            if (!_ghostHierarchyTransform)
            {
                CreateGhostHierarchyObject();
            }

            if (!child._ghostHierarchyTransform)
            {
                child.CreateGhostHierarchyObject();
            }

            child._ghostHierarchyTransform.SetParent(_ghostHierarchyTransform, true);
            child._ghostHierarchyTransform.SetSiblingIndex(child.Index);

            if (keepOriginalScale)
            {
                child._ghostHierarchyTransform.localScale = storedScale;
            }
            else
            {
                ScaleConstraintFixer.FixScaleConstraint(child._gameObject.transform, child._ghostHierarchyTransform);
            }

            child._gameObject.transform.localPosition = child._ghostHierarchyTransform.position;
            child._gameObject.transform.localRotation = child._ghostHierarchyTransform.rotation;
            child._gameObject.transform.localScale = child._ghostHierarchyTransform.lossyScale;
        }

        private void CreateGhostHierarchyObject()
        {
            var ghostHierarchyObject = new GameObject(_gameObject.name + GhostHierarchyObjectPostfix);

            _ghostHierarchyTransform = ghostHierarchyObject.transform;
            Transform ownerTransform = _gameObject.transform;

            ownerTransform.CopyToTransform(_ghostHierarchyTransform);

            PositionConstraint = CreateConstraintWithSource<PositionConstraint>(_ghostHierarchyTransform);
            RotationConstraint = CreateConstraintWithSource<RotationConstraint>(_ghostHierarchyTransform);
            ScaleConstraint = CreateConstraintWithSource<ScaleConstraint>(_ghostHierarchyTransform);

            SetHierarchyConstraintsActive(true);
        }

        private void SetHierarchyConstraintsActive(bool active)
        {
            SetConstraintActive(PositionConstraint, active);
            SetConstraintActive(RotationConstraint, active);
            SetConstraintActive(ScaleConstraint, active);
        }

        private T CreateConstraintWithSource<T>(Transform sourceTransform) where T : Behaviour, IConstraint
        {
            var constraint = _gameObject.AddComponent<T>();

            if (sourceTransform)
            {
                AddConstraintSource(constraint, sourceTransform);
            }

            return constraint;
        }

        #endregion

        #region Constraints

        private static void AddConstraintSource(IConstraint constraint, Transform sourceTransform)
        {
            if (!sourceTransform)
            {
                return;
            }

            constraint.AddSource(new ConstraintSource {weight = 1, sourceTransform = sourceTransform});
        }

        private static void ClearConstraintSources<T>(T constraint) where T : Behaviour, IConstraint
        {
            if (!constraint)
            {
                return;
            }

            while (constraint.sourceCount > 0)
            {
                constraint.RemoveSource(0);
            }
        }

        private static void SetConstraintActive<T>(T constraint, bool active) where T : Behaviour, IConstraint
        {
            if (!constraint)
            {
                return;
            }

            constraint.constraintActive = active;
        }

        #endregion

        #region Fixed Joints

        private void SetFixedJoint()
        {
            if (Parent == null)
            {
                return;
            }

            var parentRigidbody = Parent._gameObject.GetComponent<Rigidbody>();

            if (!parentRigidbody)
            {
                return;
            }

            var parentTransform = JointConstraint.GetSource(0).sourceTransform;
            var gameObjTransform = _gameObject.transform;
            
            var rotationOffset = JointConstraint.GetRotationOffset(0);
            var translationOffset = JointConstraint.GetTranslationOffset(0);

            gameObjTransform.rotation = parentTransform.rotation * Quaternion.Euler(rotationOffset);

            translationOffset.x /= parentTransform.lossyScale.x;
            translationOffset.y /= parentTransform.lossyScale.y;
            translationOffset.z /= parentTransform.lossyScale.z;
            gameObjTransform.position = parentTransform.TransformPoint(translationOffset);
            
            Joint = _gameObject.AddComponent<ConfigurableJoint>();
            
            Joint.xMotion = ConfigurableJointMotion.Locked;
            Joint.yMotion = ConfigurableJointMotion.Locked;
            Joint.zMotion = ConfigurableJointMotion.Locked;
            
            Joint.angularXMotion = ConfigurableJointMotion.Locked;
            Joint.angularYMotion = ConfigurableJointMotion.Locked;
            Joint.angularZMotion = ConfigurableJointMotion.Locked;
            
            Joint.projectionMode = JointProjectionMode.PositionAndRotation;
            Joint.projectionDistance = 0;
            Joint.projectionAngle = 0;

            Joint.connectedBody = parentRigidbody;
        }

        private void UpdateTransforms()
        {
            if (!JointConstraint)
            {
                return;
            }
            
            _gameObject.transform.rotation = Parent._gameObject.transform.rotation * Quaternion.Euler(JointConstraint.rotationOffsets[0]);
            _gameObject.transform.position = Parent._gameObject.transform.TransformPoint(JointConstraint.translationOffsets[0]);
        }

        private void DestroyFixedJoint()
        {
            var configurableJoint = _gameObject.GetComponent<ConfigurableJoint>();

            if (configurableJoint)
            {
                Object.Destroy(configurableJoint);
            }
        }

        private void ReplaceConstraintWithJoint(Rigidbody parentRigidbody)
        {
            SetConstraintActive(JointConstraint, false);
            ObjectController.CopyKinematicsFrom(parentRigidbody);
            SetFixedJoint();
        }

        private void ReplaceJointWithConstraint()
        {
            DestroyFixedJoint();
            SetConstraintActive(JointConstraint, true);
        }

        #endregion

        #region Joint Constraint

        private void RebuildJointConstraintTree()
        {
            SetConstraintActive(JointConstraint, false);
            LockParent.SetJointConstraint(_gameObject.transform);
        }

        private void RestoreJointConstraintTree()
        {
            ClearConstraintSources(LockParent.JointConstraint);
            SetConstraintActive(JointConstraint, true);
        }

        private void SetJointConstraint(Transform parent)
        {
            if (!parent || !JointConstraint)
            {
                return;
            }

            var selfTransform = _gameObject.transform;
            var inversedRotation = Quaternion.Inverse(parent.rotation);
            var positionOffset = inversedRotation * (selfTransform.position - parent.position);
            var rotationOffset = inversedRotation * selfTransform.rotation;

            if (JointConstraint.sourceCount > 0)
            {
                var sources = new List<ConstraintSource>();
                JointConstraint.GetSources(sources);
                var source = sources[0];
                source.weight = 1;
                source.sourceTransform = parent;
                JointConstraint.SetSource(0, source);
            }
            else
            {
                JointConstraint.AddSource(new ConstraintSource {weight = 1, sourceTransform = parent});
            }

            JointConstraint.SetTranslationOffset(0, positionOffset);
            JointConstraint.SetRotationOffset(0, rotationOffset.eulerAngles);
            JointConstraint.constraintActive = true;
            JointConstraint.locked = true;
        }

        #endregion

        #region Grab

        public void OnGrabStart()
        {
            if (!LockChildren)
            {
                return;
            }

            DropIfGrabbedWithOtherHand();

            foreach (HierarchyController child in LockParent.Descendants)
            {
                child.ReplaceJointWithConstraint();
            }

            if (Parent != null && Parent.LockChildren)
            {
                RebuildJointConstraintTree();
            }
        }

        public void OnGrabEnd()
        {
            if (!LockChildren)
            {
                return;
            }

            if (Parent != null && Parent.LockChildren)
            {
                RestoreJointConstraintTree();
            }

            var parentRigidbody = LockParent._gameObject.GetComponent<Rigidbody>();
            
            if (parentRigidbody.isKinematic)
            {
                return;
            }

            foreach (HierarchyController child in LockParent.Descendants)
            {
                child.ReplaceConstraintWithJoint(parentRigidbody);
                if (child._rigidBody)
                {
                    child._rigidBody.velocity = Vector3.zero;
                    child._rigidBody.angularVelocity = Vector3.zero;
                }
            }
        }

        private void DropIfGrabbedWithOtherHand()
        {
            var lockedObjects = LockParent.Descendants;
            lockedObjects.Add(LockParent);
            lockedObjects.Remove(this);

            foreach (HierarchyController o in lockedObjects)
            {
                ObjectInteraction.InteractObject interactObject = InputAdapter.Instance.ObjectInteraction.Object.GetFrom(o._gameObject);
                if (interactObject != null && interactObject.IsGrabbed())
                {
                    interactObject.ForceStopInteracting();
                }
            }
        }

        public void SetLocalPosition(Vector3 localPosition)
        {
            if (ProjectData.IsDesktopEditor)
            {
                if (Parent && !_ghostHierarchyTransform)
                {
                    _gameObject.transform.position = Parent._gameObject.transform.TransformPoint(localPosition);
                }
                else
                {
                    GetAffectedTransform().localPosition = localPosition;
                    _gameObject.transform.position = Parent
                        ? Parent._gameObject.transform.TransformPoint(localPosition)
                        : localPosition;
                }
            }
            else
            {
                if (Parent)
                {
                    _gameObject.transform.position = Parent._gameObject.transform.TransformPoint(localPosition);
                }
                else
                {
                    _gameObject.transform.position = localPosition;
                }
            }

            _localPosition = localPosition;
        }

        public void SetLocalEulerAngles(Vector3 localEulerAngles)
        {
            if (ProjectData.IsDesktopEditor)
            {
                if (Parent && !_ghostHierarchyTransform)
                {
                    _gameObject.transform.rotation = Parent._gameObject.transform.rotation * Quaternion.Euler(localEulerAngles);
                }
                else
                {
                    GetAffectedTransform().localEulerAngles = localEulerAngles;
                }
            }
            else
            {
                if (Parent)
                {
                    _gameObject.transform.rotation = Parent._gameObject.transform.rotation * Quaternion.Euler(localEulerAngles);
                }
                else
                {
                    _gameObject.transform.rotation = Quaternion.Euler(localEulerAngles);
                }
            }

            _localEulerAngles = localEulerAngles;
        }

        public void SetLocalScale(Vector3 localScale)
        {
            if (ProjectData.IsDesktopEditor)
            {
                if (Parent && !_ghostHierarchyTransform)
                {
                    var currentTRS = Matrix4x4.TRS(_localPosition, Quaternion.Euler(_localEulerAngles), localScale);
                    _gameObject.transform.localScale = (Parent._gameObject.transform.localToWorldMatrix * currentTRS).lossyScale;
                }
                else
                {
                    GetAffectedTransform().localScale = localScale;
                }
            }
            else
            {
                if (Parent)
                {
                    var parent = _gameObject.transform.parent;
                    _gameObject.transform.parent = Parent._gameObject.transform;
                    _gameObject.transform.localScale = localScale;
                    _gameObject.transform.parent = parent;
                }
                else
                {
                    _gameObject.transform.localScale = localScale;
                }
            }

            _localScale = localScale;
        }

        public Vector3 GetLocalPosition()
        {
            if (ProjectData.IsDesktopEditor)
            {
                var localPosition = GetAffectedTransform().localPosition;
                if ((_localPosition - localPosition).magnitude > Mathf.Epsilon)
                {
                    _localPosition = localPosition;
                }
            }

            return _localPosition;
        }

        public Vector3 GetLocalEulerAngles()
        {
            if (ProjectData.IsDesktopEditor)
            {
                var localRotation = GetAffectedTransform().localEulerAngles;
                if (Quaternion.Angle(GetAffectedTransform().localRotation, Quaternion.Euler(_localEulerAngles)) > 0.01f)
                {
                    _localEulerAngles = localRotation;
                }
            }

            return _localEulerAngles;
        }

        public Vector3 GetLocalScale()
        {
            if (ProjectData.IsDesktopEditor)
            {
                var localScale = GetAffectedTransform().localScale;
                if ((_localScale - localScale).magnitude > Mathf.Epsilon)
                {
                    _localScale = localScale;
                }
            }

            return _localScale;
        }

        #endregion

        public static implicit operator bool(HierarchyController hierarchyController) => hierarchyController != null;

        public override string ToString()
        {
            return ObjectController ? $"HierarchyController ({ObjectController.Name})" : base.ToString();
        }
    }
}
