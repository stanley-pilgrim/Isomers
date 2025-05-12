using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Varwin.TextToSpeech;

namespace Varwin.Public
{
    [RequireComponent(typeof(VarwinBot))] 
    public class VarwinBotTextToSpeech : MonoBehaviour
    {

        public enum SpeechGender
        {
            [Item(English:"Male",Russian:"Мужской",Chinese:"男性",Kazakh:"Ер адам",Korean:"남자")]
            Male,

            [Item(English:"Female",Russian:"Женский",Chinese:"女性",Kazakh:"Әйел адам",Korean:"여성")]
            Female
        }

        public enum SpeechLanguage
        {
            [Item(English:"English",Russian:"Английский",Chinese:"英語",Kazakh:"Ағылшынша",Korean:"영어")]
            English,

            [Item(English:"Russian",Russian:"Русский",Chinese:"俄語",Kazakh:"Орысша",Korean:"러시아어")]
            Russian
        }
        
        public AudioSource VoiceAudioSource;
        public SkinnedMeshRenderer LipSyncMesh;
        public int [] Visemes = Enumerable.Repeat(-1, VisemeCount).ToArray();

        private const int VisemeCount = 15;
        
        private float[] blendShapeInitialValues;
        
        private OVRLipSyncContext _lipSyncContext;
        private OVRLipSyncContextMorphTarget _lipSyncContextMorphTarget;
        
        private SpeechLanguage _language;
        private SpeechGender _gender;
        private string _specificVoiceName;

        private Animator _animator;
        
        [VarwinInspector(English:"male voice",Russian:"мужской голос",Chinese:"男聲",Kazakh:"ер адам дауысы",Korean:"남성 음성")]
        public bool MaleGender
        {
            set => _gender = value ? SpeechGender.Male : SpeechGender.Female;
            get => _gender == SpeechGender.Male;
        }

        [VarwinInspector(English:"english language",Russian:"английский голос",Chinese:"英語",Kazakh:"ағылшынша дауыс",Korean:"영어")]
        public bool EnglishLanguage
        {
            set => _language = value ? SpeechLanguage.English : SpeechLanguage.Russian;
            get => _language == SpeechLanguage.English;
        }
        
        [VarwinInspector(English:"specified voice name",Russian:"точное название голоса",Chinese:"指定的聲音名稱",Kazakh:"дауыстың дәл атауы",Korean:"지정된 음성 이름")]
        public string VoiceName
        {
            set => _specificVoiceName = value;
            get => _specificVoiceName;
        }
        
        [VarwinInspector(English:"use google cloud",Russian:"использовать google cloud",Chinese:"使用谷歌云",Kazakh:"google cloud пайдалану",Korean:"구글 클라우드 사용")]
        public bool GoogleCloud
        {
            set => VarwinSpeechManager.SetOfflineMode(!value);
            get => VarwinSpeechManager.IsOfflineMode;
        }

        [VarwinInspector(English:"google cloud API key",Russian:"google cloud API key",Chinese:"谷歌云 API 密鑰",Kazakh:"google cloud API key",Korean:"google cloud API key")]
        public string GoogleCloudApiKey
        {
            get => VarwinSpeechManager.GoogleCloudApiKey;
            set => VarwinSpeechManager.GoogleCloudApiKey = value;
        }
        
        [Action(English:"turn Google Cloud speech on",Russian:"включить Google Cloud",Chinese:"開啟 Google Cloud 語音",Kazakh:"Google Cloud қосу",Korean:"Google 클라우드 음성 켜기")]
        [ArgsFormat(English:"with API Key {%}",Russian:"с ключом API {%}",Chinese:"使用 API 密鑰 {%}",Kazakh:"{%} кілтімен",Korean:"API 키 {%} 사용")]
        public void TurnOnGoogleCloud(string ApiKey)
        {
            VarwinSpeechManager.GoogleCloudApiKey = ApiKey;
            VarwinSpeechManager.SetOfflineMode(false);
        }
        
        [Action(English:"turn Google Cloud speech off",Russian:"выключить Google Cloud",Chinese:"關閉 Google Cloud 語音",Kazakh:"Google Cloud ағыту",Korean:"Google 클라우드 음성 끄기")]
        public void TurnOffGoogleCloud()
        {
            VarwinSpeechManager.GoogleCloudApiKey = "";
            VarwinSpeechManager.SetOfflineMode(true);
        }

        [Checker(English:"is google cloud on",Russian:"google cloud включен",Chinese:"谷歌云在嗎",Kazakh:"google cloud қосылды",Korean:"is google cloud on")]
        public bool IsOnline()
        {
            return !VarwinSpeechManager.IsOfflineMode;
        }
        
        [Action(English:"use",Russian:"использовать",Chinese:"利用",Kazakh:"пайдалану",Korean:"사용")]
        [ArgsFormat(English:"{%} {%} voice",Russian:"{%} {%} голос",Chinese:"{％} {％} 嗓音",Kazakh:"{%} {%} дауыс",Korean:"{%} {%} 음성")]
        public void SetVoiceWith(SpeechGender gender, SpeechLanguage language)
        {
            _gender = gender;
            _language = language;
        }
        
        [Action(English:"use voice with specific name",Russian:"использовать голос с заданным именем",Chinese:"使用具有特定名稱的語音",Kazakh:"есім берілген дауысты пайдалану",Korean:"특정 이름의 음성 사용")]
        public void SetVoice(string voiceName)
        {
            _specificVoiceName = voiceName;
        }

        public event Action<string> SpeechRequest;
        public event Action SpeechCompleted;

        public void SayText(string text)
        {
            SpeechRequest?.Invoke(text);
            if (string.IsNullOrEmpty(_specificVoiceName))
            {
                VarwinSpeechManager.SayText(text, VoiceAudioSource, () =>
                    {
                        SpeechCompleted?.Invoke();
                    }, 
                    _gender == SpeechGender.Male,
                    GetCultureFromLanguage(_language));
            }
            else
            {
                VarwinSpeechManager.SayText(text, VoiceAudioSource, _specificVoiceName,
                    () =>
                    {
                        SpeechCompleted?.Invoke();
                    });
            }
        }

        public void StopSpeaking()
        {
            VarwinSpeechManager.StopSpeaking();
            
            SetSpeechState(false);
            SetSpeechState(true);
        }
        
        private void Start()
        {
            _animator = GetComponent<Animator>();
            
            if (!VoiceAudioSource)
            {
                VoiceAudioSource = GetComponentInChildren<AudioSource>();
            }

            if (!VoiceAudioSource)
            {
                Transform head = _animator.GetBoneTransform(HumanBodyBones.Head);

                if (!head)
                {
                    head = transform;
                }
                    
                VoiceAudioSource = head.gameObject.AddComponent<AudioSource>();

                VoiceAudioSource.spatialBlend = 1;
                VoiceAudioSource.minDistance = 1;
                VoiceAudioSource.maxDistance = 30;
            }
            
            if (LipSyncMesh)
            {
                int shapeCount = LipSyncMesh.sharedMesh.blendShapeCount;
                
                blendShapeInitialValues = new float[shapeCount];
                
                for (int i = 0; i < shapeCount; i++)
                {
                    blendShapeInitialValues[i] = LipSyncMesh.GetBlendShapeWeight(i);
                }

                GameObject lipSyncContextGameObject = VoiceAudioSource.gameObject;
                
                _lipSyncContext = lipSyncContextGameObject.AddComponent<OVRLipSyncContext>();

                _lipSyncContext.audioLoopback = true;
                _lipSyncContext.loopbackKey = KeyCode.None;
                _lipSyncContext.debugVisemesKey = KeyCode.None;
                _lipSyncContext.debugLaughterKey = KeyCode.None;

                _lipSyncContextMorphTarget = VoiceAudioSource.gameObject.AddComponent<OVRLipSyncContextMorphTarget>();

                _lipSyncContextMorphTarget.skinnedMeshRenderer = LipSyncMesh;
                _lipSyncContextMorphTarget.visemeToBlendTargets = Visemes;
                _lipSyncContextMorphTarget.laughterBlendTarget = -1;
            }
        }

        public void SetSpeechState(bool state)
        {
            if (VoiceAudioSource)
            {
                VoiceAudioSource.enabled = state;
            }
            
            if (!LipSyncMesh)
            {
                return;
            }
            
            _lipSyncContext.enabled = state;
            _lipSyncContextMorphTarget.enabled = state;

            if (!state)
            {
                VoiceAudioSource.clip = null;
                RestoreBlendShapeValues();
            }
        }

        private void RestoreBlendShapeValues()
        {
            if (!LipSyncMesh)
            {
                return;
            }
            
            int shapeCount = LipSyncMesh.sharedMesh.blendShapeCount;
                
            for (int i = 0; i < shapeCount; i++)
            {
                LipSyncMesh.SetBlendShapeWeight(i, blendShapeInitialValues[i]);
            }
        }
        
        private string GetCultureFromLanguage(SpeechLanguage language)
        {
            switch (language)
            {
                case SpeechLanguage.English: return "en_US";
                case SpeechLanguage.Russian: return "ru_RU";
            }

            return "en";
        }
    }
}