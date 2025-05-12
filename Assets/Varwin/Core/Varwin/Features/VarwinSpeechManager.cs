using System;
using System.Collections.Generic;
using Crosstales.RTVoice;
using Crosstales.RTVoice.Model.Enum;
using FrostweepGames.Plugins.GoogleCloud.TextToSpeech;
using UnityEngine;
using Voice = Crosstales.RTVoice.Model.Voice;

namespace Varwin.TextToSpeech
{

    public class VarwinSpeechManager : MonoBehaviour
    {
        private static Dictionary<string, Action> _speakCompleteDelegates;
        
        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            
            _speakCompleteDelegates = new Dictionary<string, Action>();

            Speaker.OnSpeakComplete += OnSpeakComplete;
        }

        public static bool IsOfflineMode => Speaker.isCustomMode;

        //TODO: add google cloud key
        public static void SetOfflineMode (bool offline)
        {
            Speaker.isCustomMode = !offline;
        }

        public static string GoogleCloudApiKey
        {
            get => GCTextToSpeech.Instance.apiKey;
            set => GCTextToSpeech.Instance.apiKey = value;
        }

        public static void SayText (
            string text,
            AudioSource audioSource,
            Action onSpeakComplete,
            bool male = true,
            string culture = "en_US")
        {

            if (Speaker.Voices.Count == 0)
            {
                Debug.LogError("Can't read text: no voices avaliable on device!");
                return;
            }
            
            if (!string.IsNullOrEmpty(text))
            {
                Voice voiceToSpeak = Speaker.VoiceForGender(male ? Gender.MALE : Gender.FEMALE, culture);

                if (voiceToSpeak == null)
                {
                    Debug.LogWarning("Can't find required voice! Using avaliable voice.");
                    voiceToSpeak = Speaker.VoiceForCulture(culture);
                }

                if (voiceToSpeak == null)
                {
                    voiceToSpeak = Speaker.Voices[0];
                } 
                
                
                string uid = Speaker.Speak(text, audioSource, voiceToSpeak);
                _speakCompleteDelegates.Add(uid, onSpeakComplete);
            }
            else
            {
                Debug.LogError("Can't read empty text!");
            }
        }

        public static void SayText(string text, AudioSource audioSource, string voiceName, Action onSpeakComplete)
        {
            if (!string.IsNullOrEmpty(text))
            {
                Voice voiceToSpeak = Speaker.VoiceForName(voiceName);

                if (voiceToSpeak == null)
                {
                    Debug.LogError("Speech aborted: Can't find voice named " + voiceName);
                    return;
                }
                
                string uid = Speaker.Speak(text, audioSource, voiceToSpeak);
                _speakCompleteDelegates.Add(uid, onSpeakComplete);
            }
            else
            {
                Debug.LogError("Can't read empty text!");
            }
        }

        public static void StopSpeaking()
        {
            Speaker.Silence();
        }

        private void OnSpeakComplete(Crosstales.RTVoice.Model.Wrapper wrapper)
        {
            if (_speakCompleteDelegates.ContainsKey(wrapper.Uid))
            {
                _speakCompleteDelegates[wrapper.Uid]();
                _speakCompleteDelegates.Remove(wrapper.Uid);
            }
        }
       
    }
}