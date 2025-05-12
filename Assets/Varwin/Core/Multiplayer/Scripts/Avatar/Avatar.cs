using Unity.Netcode;
using UnityEngine;
using Varwin.PlatformAdapter;

namespace Varwin.Multiplayer
{
    /// <summary>
    /// Аватар клиента.
    /// </summary>
    public class Avatar : NetworkBehaviour
    {
        /// <summary>
        /// Список рендереров аватара.
        /// </summary>
        private Renderer[] _renderers;

        /// <summary>
        /// Пользовательский режим отображения.
        /// </summary>
        public NetworkVariable<PlatformMode> PlatformMode = new(writePerm: NetworkVariableWritePermission.Owner);
        
        /// <summary>
        /// Позиция головы.
        /// </summary>
        public NetworkVariable<Vector3> HeadPosition = new(writePerm: NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Поворот головы.
        /// </summary>
        public NetworkVariable<Quaternion> HeadRotation = new(writePerm: NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Позиция левой руки.
        /// </summary>
        public NetworkVariable<Vector3> LeftHandPosition = new(writePerm: NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Поворот левой руки.
        /// </summary>
        public NetworkVariable<Quaternion> LeftHandRotation = new(writePerm: NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Позиция правой руки.
        /// </summary>
        public NetworkVariable<Vector3> RightHandPosition = new(writePerm: NetworkVariableWritePermission.Owner);
        
        /// <summary>
        /// Поворот правой руки.
        /// </summary>
        public NetworkVariable<Quaternion> RightHandRotation = new(writePerm: NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Позиция рига.
        /// </summary>
        public NetworkVariable<Vector3> RigPosition = new(writePerm: NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Поворот рига.
        /// </summary>
        public NetworkVariable<Quaternion> RigRotation = new(writePerm: NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Радиус коллайдера руки.
        /// </summary>
        public NetworkVariable<float> HandColliderRadius = new();

        /// <summary>
        /// Голова.
        /// </summary>
        public TransformPose Head;

        /// <summary>
        /// Тело.
        /// </summary>
        public TransformPose Body;
        
        /// <summary>
        /// Левая рука.
        /// </summary>
        public TransformPose LeftHand;
        
        /// <summary>
        /// Правая рука.
        /// </summary>
        public TransformPose RightHand;
        
        /// <summary>
        /// Риг.
        /// </summary>
        public TransformPose Rig;

        /// <summary>
        /// Инициализация и определение владельца и других клиентов, а также подписка на изменение параметров.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in _renderers)
            {
                renderer.enabled = !IsOwner;
            }

            if (IsServer)
            {
                HandColliderRadius.Value = 0.1f;
            }
            
            if (IsClient)
            {
                HandColliderRadius.OnValueChanged += OnHandColliderRadiusChanged;
            }
            
            if (IsOwner)
            {
#if UNITY_ANDROID         
                PlatformMode.Value = Varwin.PlatformMode.Vr;
#else
                PlatformMode.Value = ProjectData.PlatformMode;
#endif
            }
            else
            {
                PlatformMode.OnValueChanged += OnPlatformModeChanged;
                OnPlatformModeChanged(PlatformMode.Value, PlatformMode.Value);
            }
        }

        /// <summary>
        /// При изменении радиуса коллайдеров руки.
        /// </summary>
        /// <param name="previousValue">Предыдущее значение.</param>
        /// <param name="newValue">Новое значение.</param>
        private void OnHandColliderRadiusChanged(float previousValue, float newValue)
        {
            LeftHand.GetComponent<SphereCollider>().radius = newValue;
            RightHand.GetComponent<SphereCollider>().radius = newValue;
        }

        /// <summary>
        /// При смене режима отображения на клиенте.
        /// </summary>
        /// <param name="oldPlatformMode">Предыдущий режим предпросмотра.</param>
        /// <param name="newPlatformMode">Новый режим предпросмотра.</param>
        private void OnPlatformModeChanged(PlatformMode oldPlatformMode, PlatformMode newPlatformMode)
        {
            foreach (var renderer in _renderers)
            {
                renderer.enabled = newPlatformMode != Varwin.PlatformMode.Spectator;
            }

            var useHands = newPlatformMode != Varwin.PlatformMode.Spectator 
                           && newPlatformMode != Varwin.PlatformMode.Desktop && newPlatformMode != Varwin.PlatformMode.NettleDesk;

            LeftHand.GetComponent<Renderer>().enabled = useHands;
            RightHand.GetComponent<Renderer>().enabled = useHands;
        }

        /// <summary>
        /// Обновление аватара на других клиентах.
        /// </summary>
        private void Update()
        {
            UpdateInfo();
            UpdateAvatarTransforms();
        }

        /// <summary>
        /// Обновление информации на клиенте.
        /// </summary>
        private void UpdateInfo()
        {
            if (!NetworkObject.IsOwner)
            {
                return;
            }

#if !UNITY_ANDROID
            if (PlatformMode.Value != ProjectData.PlatformMode)
            {
                PlatformMode.Value = ProjectData.PlatformMode;
            }
#endif
        }

        /// <summary>
        /// Обновление информации на других клиентах.
        /// </summary>
        private void UpdateAvatarTransforms()
        {
            Rig.SetPosition(RigPosition.Value);
            Rig.SetRotation(RigRotation.Value);

            LeftHand.SetPosition(LeftHandPosition.Value);
            LeftHand.SetRotation(LeftHandRotation.Value);

            RightHand.SetPosition(RightHandPosition.Value);
            RightHand.SetRotation(RightHandRotation.Value);

            Body.SetPosition(HeadPosition.Value);
            Body.SetRotation(RigRotation.Value);
            
            Head.SetPosition(HeadPosition.Value);
            Head.SetRotation(HeadRotation.Value);
        }

        /// <summary>
        /// Обновление параметров аватара владельцем.
        /// </summary>
        private void FixedUpdate()
        {
            if (!NetworkObject.IsOwner)
            {
                return;
            }

            var nodes = InputAdapter.Instance.PlayerController.Nodes;

            SetValue(HeadPosition, nodes.Head.Transform.position);
            SetValue(HeadRotation, nodes.Head.Transform.rotation);

            if (nodes.LeftHand != null && nodes.LeftHand.Transform)
            {
                SetValue(LeftHandPosition, nodes.LeftHand.Transform.position);
                SetValue(LeftHandRotation, nodes.LeftHand.Transform.rotation);
            }

            if (nodes.RightHand != null && nodes.RightHand.Transform)
            {
                SetValue(RightHandPosition, nodes.RightHand.Transform.position);
                SetValue(RightHandRotation, nodes.RightHand.Transform.rotation);
            }

            SetValue(RigPosition, nodes.Rig.Transform.position);
            SetValue(RigRotation, nodes.Rig.Transform.rotation);
        }

        /// <summary>
        /// Задать значение переменной.
        /// </summary>
        /// <param name="variable">Переменная.</param>
        /// <param name="value">Значение.</param>
        /// <param name="threshold">Дельта значения.</param>
        private void SetValue(NetworkVariable<Vector3> variable, Vector3 value, float threshold = 0.01f)
        {
            if ((variable.Value - value).magnitude > threshold)
            {
                variable.Value = value;
            }
        }

        /// <summary>
        /// Задать значение переменной.
        /// </summary>
        /// <param name="variable">Переменная.</param>
        /// <param name="value">Значение.</param>
        /// <param name="threshold">Дельта значения.</param>
        private void SetValue(NetworkVariable<Quaternion> variable, Quaternion value, float threshold = 0.01f)
        {
            var firstAngle = variable.Value.eulerAngles;
            var secondAngle = value.eulerAngles;
            var xAngleDelta = Mathf.Abs(Mathf.DeltaAngle(firstAngle.x, secondAngle.x));
            var yAngleDelta = Mathf.Abs(Mathf.DeltaAngle(firstAngle.y, secondAngle.y));
            var zAngleDelta = Mathf.Abs(Mathf.DeltaAngle(firstAngle.z, secondAngle.z));

            if (xAngleDelta > threshold || yAngleDelta > threshold || zAngleDelta > threshold)
            {
                variable.Value = value;
            }
        }
    }
}