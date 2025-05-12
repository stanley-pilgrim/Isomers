using Unity.Netcode;
using UnityEngine;
using Varwin.Core.Behaviours;
using Varwin.Core.Behaviours.ConstructorLib;

namespace Varwin.Multiplayer.NetworkBehaviours.v2
{
    public class NetworkLightBehaviour : VarwinNetworkBehaviour
    {
        private LightBehaviour _lightBehaviour;

        /// <summary>
        /// Синхронизирумые переменные
        /// </summary>
        #region Syncronized Variables

        private readonly NetworkVariable<Color> _mainColor = new();
        private readonly NetworkVariable<float> _range = new();
        private readonly NetworkVariable<float> _intensity = new();
        private readonly NetworkVariable<LightBehaviour.ShadowType> _shadows = new();
        private readonly NetworkVariable<float> _shadowStrength = new();

        #endregion

        public override void InitializeNetworkBehaviour(VarwinBehaviour varwinBehaviour)
        {
            _lightBehaviour = varwinBehaviour as LightBehaviour;
        }

        /// <summary>
        /// Инициализация коллбеков изменения значений у синхронизируемых свойств
        /// </summary>
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                return;
            }

            _mainColor.OnValueChanged += OnMainColorChanged;
            _range.OnValueChanged += OnRangeValueChanged;
            _intensity.OnValueChanged += OnIntensityChanged;
            _shadows.OnValueChanged += OnShadowsChanged;
            _shadowStrength.OnValueChanged += OnShadowStrengthChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                return;
            }

            _mainColor.OnValueChanged -= OnMainColorChanged;
            _range.OnValueChanged -= OnRangeValueChanged;
            _intensity.OnValueChanged -= OnIntensityChanged;
            _shadows.OnValueChanged -= OnShadowsChanged;
            _shadowStrength.OnValueChanged -= OnShadowStrengthChanged;
        }

        private void FixedUpdate()
        {
            if (!IsServer)
            {
                return;
            }

            if (_lightBehaviour.MainColor != _mainColor.Value)
            {
                _mainColor.Value = _lightBehaviour.MainColor;
            }

            if (IsFloatDifferent(_range.Value, _lightBehaviour.Range))
            {
                _range.Value = _lightBehaviour.Range;
            }

            if (IsFloatDifferent(_intensity.Value, _lightBehaviour.Intensity))
            {
                _intensity.Value = _lightBehaviour.Intensity;
            }

            if (_lightBehaviour.Shadows != _shadows.Value)
            {
                _shadows.Value = _lightBehaviour.Shadows;
            }

            if (IsFloatDifferent(_shadowStrength.Value, _lightBehaviour.ShadowStrength))
            {
                _shadowStrength.Value = _lightBehaviour.ShadowStrength;
            }
        }

        #region Network Variables Callbacks

        private void OnShadowStrengthChanged(float oldValue, float newValue)
        {
            _lightBehaviour.ShadowStrength = newValue;
        }

        private void OnShadowsChanged(LightBehaviour.ShadowType oldValue, LightBehaviour.ShadowType newValue)
        {
            _lightBehaviour.Shadows = newValue;
        }

        private void OnIntensityChanged(float oldValue, float newValue)
        {
            _lightBehaviour.Intensity = newValue;
        }

        private void OnRangeValueChanged(float oldValue, float newValue)
        {
            _lightBehaviour.Range = newValue;
        }

        private void OnMainColorChanged(Color oldValue, Color newValue)
        {
            _lightBehaviour.MainColor = newValue;
        }

        #endregion
    }
}