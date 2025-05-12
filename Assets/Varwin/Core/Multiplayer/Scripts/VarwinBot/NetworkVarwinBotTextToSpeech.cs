using Unity.Netcode;
using Varwin.Public;

namespace Varwin.Multiplayer
{
    public class NetworkVarwinBotTextToSpeech : NetworkBehaviour
    {
        private VarwinBotTextToSpeech _targetBehaviour;
        private string _apiKey;
        private string _voiceName;

        public NetworkVariable<bool> IsMaleGender = new();
        public NetworkVariable<bool> IsEnglishLanguage = new(); 
        
        public override void OnNetworkSpawn()
        {
            _targetBehaviour = GetComponent<VarwinBotTextToSpeech>();
            if (IsServer)
            {
                _targetBehaviour.SpeechRequest += OnSpeechRequest;
            }
            else
            {
                IsMaleGender.OnValueChanged += OnGenderValueChanged;
                IsEnglishLanguage.OnValueChanged += OnEnglishLanguageChanged;
            }
        }

        private void OnGenderValueChanged(bool previousValue, bool newValue)
        {
            _targetBehaviour.MaleGender = newValue;
        }

        private void OnEnglishLanguageChanged(bool previousValue, bool newValue)
        {
            _targetBehaviour.EnglishLanguage = newValue;
        }

        private void OnSpeechRequest(string text)
        {
            SpeechTextClientRpc(text);
        }

        [ClientRpc]
        private void SpeechTextClientRpc(string text)
        {
            _targetBehaviour.SayText(text);
        }

        private void FixedUpdate()
        {
            if (!IsServer)
            {
                return;
            }

            if (_apiKey != _targetBehaviour.GoogleCloudApiKey)
            {
                _apiKey = _targetBehaviour.GoogleCloudApiKey;
                SetApiKeyClientRpc(_apiKey);
            }

            if (IsEnglishLanguage.Value != _targetBehaviour.EnglishLanguage)
            {
                IsEnglishLanguage.Value = _targetBehaviour.EnglishLanguage;
            }

            if (IsMaleGender.Value != _targetBehaviour.MaleGender)
            {
                IsMaleGender.Value = _targetBehaviour.MaleGender;
            }
        }

        [ClientRpc]
        private void SetApiKeyClientRpc(string apiKey)
        {
            _targetBehaviour.GoogleCloudApiKey = apiKey;
        }

        public override void OnDestroy()
        {
            if (IsServer)
            {
                _targetBehaviour.SpeechRequest -= OnSpeechRequest;
            }
            else
            {
                IsMaleGender.OnValueChanged -= OnGenderValueChanged;
                IsEnglishLanguage.OnValueChanged -= OnEnglishLanguageChanged;
            }
        }
    }
}