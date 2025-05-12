using System;
using System.Collections;
using SmartLocalization;
using TMPro;
using UnityEngine;
using Varwin.PlatformAdapter;

namespace Varwin
{
    public class UIFadeInOutController : MonoBehaviour
    {
        public static UIFadeInOutController Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = FindObjectOfType<UIFadeInOutController>();
                }

                if (!_instance)
                {
                    Debug.LogError($"{nameof(UIFadeInOutController)} not found");
                }

                return _instance;
            }
        }

        public static bool IsBlocked;

        private static UIFadeInOutController _instance;
        
        [Header("Fade Durations")]
        public float FadeInDuration = 0.66f;
        public float FadeOutDuration = 0.33f;

        public FadeInOutStatus FadeStatus { get; protected set; }

        public CanvasGroup CanvasGroup
        {
            get 
            {
                if (!_canvasGroup)
                {
                    _canvasGroup = GetComponentInChildren<CanvasGroup>(true);
                }
                
                return _canvasGroup;
            }           
        }

        public float Alpha
        {
            get
            {
                return CanvasGroup.alpha;
            }
        }

        public bool IsComplete => FadeStatus is FadeInOutStatus.FadingInComplete or FadeInOutStatus.FadingOutComplete;

        private CanvasGroup _canvasGroup;
        private Canvas _canvas;

        protected float _fadeInSpeed;
        protected float _fadeOutSpeed;

        [Header("Fade Elements")]
        [SerializeField] protected TMP_Text ScenePreparationDesktopText;
        [SerializeField] protected TMP_Text ScenePreparationVRText;
        [SerializeField] protected Transform ScenePreparationVRRoot;

        private GameMode _prevGameMode = GameMode.Undefined;

        protected virtual void Awake()
        {
            if (_instance && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            _instance = this;

            if (!_canvasGroup)
            {
                _canvasGroup = GetComponentInChildren<CanvasGroup>(true);
            }

            if (!_canvas)
            {
                _canvas = GetComponentInChildren<Canvas>(true);
            }

            _canvas.gameObject.SetActive(true);
            
            InstantFadeIn();
            StartCoroutine(SetFadeStatus(FadeInOutStatus.None));

            CalculateSpeeds();
        }

        protected virtual void Reset()
        {
            _canvasGroup = GetComponentInChildren<CanvasGroup>(true);
            _canvas = GetComponentInChildren<Canvas>(true);
        }

        protected virtual void LateUpdate()
        {
            UpdateScenePreparationText();
            
            if (FadeStatus is FadeInOutStatus.None or FadeInOutStatus.FadingInComplete or FadeInOutStatus.FadingOutComplete)
            {
                return;
            }

            if (!_canvas || !_canvasGroup)
            {
                return;
            }


            if (FadeStatus == FadeInOutStatus.FadingIn)
            {
                _canvasGroup.alpha = Mathf.Clamp01(_canvasGroup.alpha + _fadeOutSpeed * Time.smoothDeltaTime);
                if (_canvasGroup.alpha >= 1f - Mathf.Epsilon)
                {
                    FadeStatus = FadeInOutStatus.FadingInComplete;
                }
            }
            else if (FadeStatus == FadeInOutStatus.FadingOut)
            {
                _canvasGroup.alpha = Mathf.Clamp01(_canvasGroup.alpha - _fadeOutSpeed * Time.smoothDeltaTime);
                if (_canvasGroup.alpha <= Mathf.Epsilon)
                {
                    FadeStatus = FadeInOutStatus.FadingOutComplete;
                }
            }

            UpdateCanvas();
        }

        protected virtual void UpdateScenePreparationText()
        {
            if (SceneLogic.ScenePreparing)
            {
                var text = string.IsNullOrEmpty(SceneLogic.ScenePreparingText) ? LanguageManager.Instance.GetTextValue("SCENE_PREPARATION") : SceneLogic.ScenePreparingText;
                
                if (ScenePreparationDesktopText)
                {
                    ScenePreparationDesktopText.text = text;
                }
                
                if (ScenePreparationVRText)
                {
                    ScenePreparationVRText.text = text;
                }
            }

            var transparentWhite = new Color(1f, 1f, 1f, 0f);
            var dt = 8f * Time.unscaledDeltaTime;
            var showCondition = FadeStatus == FadeInOutStatus.FadingInComplete && SceneLogic.ScenePreparing && (DateTime.UtcNow - SceneLogic.ScenePreparingUtcStartTime).TotalSeconds > 1f;

            if (ScenePreparationDesktopText)
            {
                ScenePreparationDesktopText.color = Color.Lerp(ScenePreparationDesktopText.color, showCondition ? Color.white : transparentWhite, dt);
                ScenePreparationDesktopText.enabled = ScenePreparationDesktopText.color.a > Mathf.Epsilon;
            }
            
            if (ScenePreparationVRText)
            {
                ScenePreparationVRText.color = Color.Lerp(ScenePreparationVRText.color, showCondition ? Color.white : transparentWhite, dt);
                ScenePreparationVRText.enabled = ProjectData.PlatformMode == PlatformMode.Vr && ScenePreparationVRText.color.a > Mathf.Epsilon;
            }

            var playerRig = InputAdapter.Instance?.PlayerController?.Nodes?.Rig?.Transform;
            if (ScenePreparationVRRoot && playerRig)
            {
                ScenePreparationVRRoot.transform.position = Vector3.Lerp(ScenePreparationVRRoot.transform.position, playerRig.position, dt);
                
                var rotation = ScenePreparationVRRoot.transform.rotation;
                rotation = Quaternion.Slerp(rotation, playerRig.rotation, dt);
                ScenePreparationVRRoot.transform.rotation = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
            }
        }

        public virtual void FadeIn()
        {
            FadeStatus = FadeInOutStatus.None;
            StartCoroutine(SetFadeStatus(FadeInOutStatus.FadingIn));
            CalculateSpeeds();
        }

        public virtual void FadeOut()
        {
            FadeStatus = FadeInOutStatus.None;
            StartCoroutine(SetFadeStatus(FadeInOutStatus.FadingOut));
            CalculateSpeeds();
        }

        private IEnumerator SetFadeStatus(FadeInOutStatus status)
        {
            yield return null;
            FadeStatus = status;
        }

        public virtual void InstantFadeIn()
        {
            CanvasGroup.alpha = 1f;
            UpdateCanvas();
            FadeStatus = FadeInOutStatus.FadingInComplete;
        }

        public virtual void InstantFadeOut()
        {
            CanvasGroup.alpha = 0f;
            UpdateCanvas();
            FadeStatus = FadeInOutStatus.FadingOutComplete;
        }

        protected virtual void CalculateSpeeds()
        {
            _fadeInSpeed = Mathf.Abs(FadeInDuration) > Mathf.Epsilon ? 1f / FadeInDuration : float.MaxValue;
            _fadeOutSpeed = Mathf.Abs(FadeOutDuration) > Mathf.Epsilon ? 1f / FadeOutDuration : float.MaxValue;
        }

        protected virtual void UpdateCanvas()
        {
            var showCondition = _canvasGroup.alpha > Mathf.Epsilon;
            _canvas.gameObject.SetActive(showCondition);
            _canvas.enabled = showCondition;
        }
    }

    public enum FadeInOutStatus
    {
        None = 0,
        FadingIn,
        FadingInComplete,
        FadingOut,
        FadingOutComplete
    }
}