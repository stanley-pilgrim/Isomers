using System;
using System.Collections;
using SmartLocalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Varwin.Log;

namespace Varwin.UI.VRErrorManager
{
    public class VRErrorManager : MonoBehaviour
    {
        private const float FadeTime = 1f;
        private const float Duration = 5f;
        private bool fatalError;
        
        public GameObject Panel;
        public static VRErrorManager Instance;
        public bool IsShowing => Panel.activeSelf;

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private GameObject _notificator;
        [SerializeField] private TMP_Text _text;
        
        private WaitForEndOfFrame _waitForEndOfFrame;
        private Coroutine _workCoroutine;
        private float _hideMessageTime;

        private void Awake()
        {
            Instance = this;

            if (!ProjectData.IsMobileClient)
            {
                Application.logMessageReceived += OnMessageReceived;    
            }

            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void OnSceneChanged(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
        {
            if (newScene.buildIndex == -1)
            {
                return;
            }
            
            Panel.SetActive(false);
            if (_notificator)
            {
                _notificator.SetActive(false);
            }
        }

        private void OnMessageReceived(string condition, string stacktrace, LogType type)
        {
            if (ErrorHelper.NeedIgnoreMessage(condition))
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
            {
                ShowFatal(condition, stacktrace);
            }
        }

        public void Show(string message, Action retry = null)
        {
            if (SceneManager.GetActiveScene().buildIndex != -1)
            {
                return;
            }
            
            if (Panel.activeSelf)
            {
                return;
            }

            fatalError = true;

            Panel.SetActive(true);
            StopActiveCoroutine();

            _workCoroutine = StartCoroutine(SetFade(endAlpha: 1, onFadeEndAction: () =>
            {
                Helper.HideUi();
                _hideMessageTime = Time.time + Duration;
                _workCoroutine = StartCoroutine(WaitAndHideErrorMessage());
            }));

            if (!_notificator.activeSelf)
            {
                _notificator.SetActive(true);
            }
            
        }

        public void ShowFatal(string message, string stackTrace = "")
        {
            Show(message);
        }

        public void Hide()
        {
            StopActiveCoroutine();
            _workCoroutine = StartCoroutine(SetFade(endAlpha: 0, onFadeEndAction: () =>
            {
                Panel.SetActive(false);
            }));
        }

        private IEnumerator SetFade(float endAlpha, Action onFadeEndAction)
        {
            _text.text = LanguageManager.Instance.GetTextValue("ERROR_DESKTOP_POPUP_MESSAGE");
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) _text.transform);
            
            var startTime = Time.time;
            var endTime = startTime + FadeTime;
            var startValue = _canvasGroup.alpha;
            var endValue = endAlpha;

            while (Time.time < endTime)
            {
                var lerp = (Time.time - startTime) / FadeTime;
                _canvasGroup.alpha = Mathf.Lerp(startValue, endValue, lerp);

                yield return _waitForEndOfFrame;
            }

            _workCoroutine = null;
            onFadeEndAction?.Invoke();
        }

        private IEnumerator WaitAndHideErrorMessage()
        {
            while (Time.time < _hideMessageTime)
            {
                yield return _waitForEndOfFrame;
            }

            Hide();
            _workCoroutine = null;
        }

        private void StopActiveCoroutine()
        {
            if (_workCoroutine != null)
            {
                StopCoroutine(_workCoroutine);
            }

            _workCoroutine = null;
        }

        private void OnDestroy()
        {
            if (!ProjectData.IsMobileClient)
            {
                Application.logMessageReceived -= OnMessageReceived;
            }

            Application.logMessageReceived -= OnMessageReceived;
            SceneManager.activeSceneChanged -= OnSceneChanged;
        }
    }
}
