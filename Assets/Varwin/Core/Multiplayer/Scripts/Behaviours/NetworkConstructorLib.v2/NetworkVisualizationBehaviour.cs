using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Varwin.Core.Behaviours;
using Varwin.Core.Behaviours.ConstructorLib;
using Varwin.Data.ServerData;
using Varwin.Multiplayer.Types;
using Varwin.WWW;

namespace Varwin.Multiplayer.NetworkBehaviours.v2
{
    public class NetworkVisualizationBehaviour : VarwinNetworkBehaviour
    {
        /// <summary>
        /// Ссылка на синхронизируемый VisualizationBehaviour
        /// </summary>
        private VisualizationBehaviour _visualizationBehaviour;

        /// <summary>
        /// Синхронизирумые переменные
        /// </summary>
        #region Syncronized Variables

        private readonly NetworkVariable<Color> _mainColor = new();
        private readonly NetworkVariableString _textureResourceName = new();
        private readonly NetworkVariable<bool> _receiveShadows = new();
        private readonly NetworkVariable<bool> _unlit = new();
        private readonly NetworkVariable<float> _metallic = new();
        private readonly NetworkVariable<float> _glossiness = new();
        private readonly NetworkVariable<float> _tilingX = new();
        private readonly NetworkVariable<float> _tilingY = new();
        private readonly NetworkVariable<float> _offsetX = new();
        private readonly NetworkVariable<float> _offsetY = new();
        private readonly NetworkVariable<int> _renderQueueOffset = new();

        #endregion

        /// <inheritdoc />
        public override void InitializeNetworkBehaviour(VarwinBehaviour varwinBehaviour)
        {
            _visualizationBehaviour = GetComponent<VisualizationBehaviour>();

            if (IsServer)
            {
                _mainColor.Value = _visualizationBehaviour.MainColor;
                _textureResourceName.Text = _visualizationBehaviour.MaterialTexture ? _visualizationBehaviour.MaterialTexture.name : "";
                _receiveShadows.Value = _visualizationBehaviour.ReceiveShadowsOtherObjects;
                _unlit.Value = _visualizationBehaviour.Unlit;
                _metallic.Value = _visualizationBehaviour.Metallic;
                _glossiness.Value = _visualizationBehaviour.Smoothness;
                _tilingX.Value = _visualizationBehaviour.TilingX;
                _tilingY.Value = _visualizationBehaviour.TilingY;
                _offsetX.Value = _visualizationBehaviour.OffsetX;
                _offsetY.Value = _visualizationBehaviour.OffsetY;
                _renderQueueOffset.Value = _visualizationBehaviour.RenderQueueOffset;
            }
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
            _textureResourceName.OnValueChanged += OnTextureNameChanged;
            _receiveShadows.OnValueChanged += OnReceiveShadowsChanged;
            _unlit.OnValueChanged += OnUnlitValueChanged;
            _metallic.OnValueChanged += OnMetallicValueChanged;
            _glossiness.OnValueChanged += OnGlossinessValueChanged;
            _tilingX.OnValueChanged += OnTilingXChanged;
            _tilingY.OnValueChanged += OnTilingYChanged;
            _offsetX.OnValueChanged += OnOffsetXChanged;
            _offsetY.OnValueChanged += OnOffsetYChanged;
            _renderQueueOffset.OnValueChanged += OnRenderQueueChanged;
        }

        /// <summary>
        /// Отписка коллбеков изменения значений у синхронизируемых свойств
        /// </summary>
        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                return;
            }

            _mainColor.OnValueChanged -= OnMainColorChanged;
            _textureResourceName.OnValueChanged -= OnTextureNameChanged;
            _receiveShadows.OnValueChanged -= OnReceiveShadowsChanged;
            _unlit.OnValueChanged -= OnUnlitValueChanged;
            _metallic.OnValueChanged -= OnMetallicValueChanged;
            _glossiness.OnValueChanged -= OnGlossinessValueChanged;
            _tilingX.OnValueChanged -= OnTilingXChanged;
            _tilingY.OnValueChanged -= OnTilingYChanged;
            _offsetX.OnValueChanged -= OnOffsetXChanged;
            _offsetY.OnValueChanged -= OnOffsetYChanged;
            _renderQueueOffset.OnValueChanged -= OnRenderQueueChanged;
        }

        /// <summary>
        /// Обновление значений у синхронизируеммых свойств
        /// </summary>
        private void FixedUpdate()
        {
            if (!IsServer)
            {
                return;
            }

            if (_visualizationBehaviour.MainColor != _mainColor.Value)
            {
                _mainColor.Value = _visualizationBehaviour.MainColor;
            }

            if (_visualizationBehaviour.MaterialTexture && _visualizationBehaviour.MaterialTexture.name != _textureResourceName.Text)
            {
                _textureResourceName.Text = _visualizationBehaviour.MaterialTexture.name;
            }

            if (_visualizationBehaviour.ReceiveShadowsOtherObjects != _receiveShadows.Value)
            {
                _receiveShadows.Value = _visualizationBehaviour.ReceiveShadowsOtherObjects;
            }

            if (_visualizationBehaviour.Unlit != _unlit.Value)
            {
                _unlit.Value = _visualizationBehaviour.Unlit;
            }

            if (IsFloatDifferent(_visualizationBehaviour.Metallic, _metallic.Value))
            {
                _metallic.Value = _visualizationBehaviour.Metallic;
            }

            if (IsFloatDifferent(_visualizationBehaviour.Smoothness, _glossiness.Value))
            {
                _glossiness.Value = _visualizationBehaviour.Smoothness;
            }

            if (IsFloatDifferent(_visualizationBehaviour.TilingX, _tilingX.Value))
            {
                _tilingX.Value = _visualizationBehaviour.TilingX;
            }

            if (IsFloatDifferent(_visualizationBehaviour.TilingY, _tilingY.Value))
            {
                _tilingY.Value = _visualizationBehaviour.TilingY;
            }

            if (IsFloatDifferent(_visualizationBehaviour.OffsetX, _offsetX.Value))
            {
                _offsetX.Value = _visualizationBehaviour.OffsetX;
            }

            if (IsFloatDifferent(_visualizationBehaviour.OffsetY, _offsetY.Value))
            {
                _offsetY.Value = _visualizationBehaviour.OffsetY;
            }

            if (_visualizationBehaviour.RenderQueueOffset != _renderQueueOffset.Value)
            {
                _renderQueueOffset.Value = _visualizationBehaviour.RenderQueueOffset;
            }
        }

        #region Network Variables Callbacks

        //TODO: потестить в клиенте
        private void OnTextureNameChanged(string newTextureName)
        {
            if (string.IsNullOrEmpty(newTextureName))
            {
                return;                
            }

            API.GetResources(_textureResourceName.Text, 1, null, ResourceRequestType.Image, OnTextureResourceLoadedCallback);
        }

        private void OnTextureResourceLoadedCallback(List<ResourceDto> loadedResources)
        {
            if (loadedResources == null || loadedResources.Count == 0)
            {
                Debug.LogError("Texture not loaded");
                return;
            }

            var resourceGuid = loadedResources[0].Guid;
            LoaderAdapter.LoadResources(loadedResources[0]);
            ProjectData.ResourcesLoaded += SetTexture;

            void SetTexture()
            {
                ProjectData.ResourcesLoaded -= SetTexture;
                var textureResourceObject = GameStateData.GetResource(resourceGuid);
                _visualizationBehaviour.MaterialTexture = (Texture)textureResourceObject.Value;
            }
        }

        private void OnMainColorChanged(Color oldValue, Color newValue) => _visualizationBehaviour.MainColor = newValue;

        private void OnReceiveShadowsChanged(bool oldValue, bool newValue) => _visualizationBehaviour.ReceiveShadowsOtherObjects = newValue;

        private void OnUnlitValueChanged(bool oldValue, bool newValue) => _visualizationBehaviour.Unlit = newValue;

        private void OnMetallicValueChanged(float oldValue, float newValue) => _visualizationBehaviour.Metallic = newValue;

        private void OnRenderQueueChanged(int oldValue, int newValue) => _visualizationBehaviour.RenderQueueOffset = newValue;

        private void OnOffsetYChanged(float oldValue, float newValue) => _visualizationBehaviour.OffsetY = newValue;

        private void OnOffsetXChanged(float oldValue, float newValue) => _visualizationBehaviour.OffsetX = newValue;

        private void OnTilingYChanged(float oldValue, float newValue) => _visualizationBehaviour.TilingY = newValue;

        private void OnTilingXChanged(float oldValue, float newValue) => _visualizationBehaviour.TilingX = newValue;

        private void OnGlossinessValueChanged(float oldValue, float newValue) => _visualizationBehaviour.Smoothness = newValue;

        #endregion
    }
}
