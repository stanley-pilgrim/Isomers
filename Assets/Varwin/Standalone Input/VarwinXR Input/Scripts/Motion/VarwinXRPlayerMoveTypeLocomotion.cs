using UnityEngine;
using UnityEngine.SceneManagement;
using Varwin.PlatformAdapter;

namespace Varwin.XR
{
    /// <summary>
    /// Контроллер locomotion.
    /// </summary>
    public class VarwinXRPlayerMoveTypeLocomotion : VarwinXRPlayerMoveBase
    {
        /// <summary>
        /// Ключ хранения настроек типа.
        /// </summary>
        private const string SettingsKey = "vw.player_locomotion_vignette_type";
        
        /// <summary>
        /// Ключ локализации названия.
        /// </summary>
        public override string LocalizationNameKey => "PLAYER_MOVE_TYPE_LOCOMOTION";

        /// <summary>
        /// Нажата ли кнопка прыжка.
        /// </summary>
        private bool _isJumpPressed;

        /// <summary>
        /// Виньетка.
        /// </summary>
        public VarwinXRVignette Vignette;
        
        /// <summary>
        /// Тип виньетки.
        /// </summary>
        private VignetteType _vignetteType;

        /// <summary>
        /// Тип виньетки.
        /// </summary>
        public VignetteType VignetteType
        {
            get => GetVignetteType();
            set => SetVignetteType(value);
        }

        /// <summary>
        /// Деактивация телепорта на контроллере.
        /// </summary>
        protected override void OnEnable()
        {
            _leftController.InvokeRotate = false;
            _leftController.InvokeTeleport = false;
            _rightController.InvokeTeleport = false;
            
            Vignette.enabled = VignetteType != VignetteType.Off;
        }

        /// <summary>
        /// Скрытие виньетки.
        /// </summary>
        protected override void OnDisable()
        {
            Vignette.enabled = false;
            Vignette.Renderer.enabled = false;
            
            _player.SetMoveVelocity(Vector3.zero);
        }

        /// <summary>
        /// Обновление перемещения.
        /// </summary>
        private void Update()
        {
            UpdateMovement();

            var isJumpPressed = !_rightController.Is3Dof && (_rightController.TeleportOnThumbstick ? _rightController.IsPrimaryButtonPressed : !_rightController.IsTurnPressed && _rightController.IsPrimary2DAxisPressed);
            if (isJumpPressed && !_isJumpPressed)
            {
                _player.TryJump();
            }

            _isJumpPressed = isJumpPressed;
        }

        /// <summary>
        /// Обновление перемещения.
        /// </summary>
        private void UpdateMovement()
        {
            Vector3 velocity = default;

            if (TeleportPointer.TeleportEnabled)
            {
                var primaryAxis = _leftController.Is3Dof ? Vector2.ClampMagnitude(_leftController.Primary2DAxisValue + _rightController.Primary2DAxisValue, 1f) : _leftController.Primary2DAxisValue;
                var primaryAxisPressed = _leftController.Is3Dof ? _leftController.IsPrimary2DAxisPressed | _rightController.IsPrimary2DAxisPressed : _leftController.IsPrimary2DAxisPressed;
            
                var forwardVector = Vector3.ProjectOnPlane(_player.Head.forward, Vector3.up).normalized * primaryAxis.y;
                var rightVector = Vector3.ProjectOnPlane(_player.Head.right, Vector3.up).normalized * primaryAxis.x;

                velocity = primaryAxisPressed ? Vector3.zero : (forwardVector + rightVector) * PlayerManager.SprintSpeed;
            }
            
            _player.SetMoveVelocity(Vector3.Lerp(_player.Velocity, velocity, Time.deltaTime * 10f));

            if (VignetteType == VignetteType.Off)
            {
                return;
            }
            
            Vignette.SetForce(SceneManager.GetActiveScene().buildIndex != -1 ? 0 : Mathf.Clamp01(_player.Velocity.magnitude / PlayerManager.SprintSpeed));
            Vignette.SetType(VignetteType);
        }
        
        /// <summary>
        /// Задать тип виньетки.
        /// </summary>
        /// <param name="value">Тип виньетки.</param>
        private void SetVignetteType(VignetteType value)
        {
            if (!enabled)
            {
                return;
            }
            
            Vignette.enabled = value != VignetteType.Off;
            _vignetteType = value;
            PlayerPrefs.SetInt(SettingsKey, (int) _vignetteType);
        }

        /// <summary>
        /// Возвращает тип виньетки. Если сохраненного нет, то fallback на Strong.
        /// </summary>
        /// <returns>Тип виньетки.</returns>
        private VignetteType GetVignetteType()
        {
            if (!PlayerPrefs.HasKey(SettingsKey))
            {
                SetVignetteType(VignetteType.Strong);
                return VignetteType.Strong;
            }

            _vignetteType = (VignetteType) PlayerPrefs.GetInt(SettingsKey);
            return _vignetteType;
        }
    }
}