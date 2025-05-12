using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Varwin.UI
{
    public class NotificationWindow : FloatWindow
    {
        [SerializeField] private Text _messageText;
        [SerializeField] private Animator _animator;
        
        private readonly int HideKey = Animator.StringToHash("Hide");
        private readonly int OpenedKey = Animator.StringToHash("Opened");
        
        private bool IsAnimCloseProcess;
        private bool IsAnimOpenProcess;

        private void OnEnable()
        {
            ProjectData.GameModeChanged += OnGameModeChanged;
        }

        private void OnDisable()
        {
            ProjectData.GameModeChanged -= OnGameModeChanged;
            StopAllCoroutines();
            ResetWindow();
        }

        private void OnGameModeChanged(GameMode mode)
        {
            ResetWindow();
        }

        private void ResetWindow()
        {
            IsAnimOpenProcess = false;
            IsAnimCloseProcess = false;
            _animator.SetBool(OpenedKey, false);
            _animator.SetTrigger(HideKey);
            UiUtils.RebuildLayouts(this);
        }

        private void OnAnimOpenFinish()
        {
            IsAnimOpenProcess = false;
        }

        private void OnAnimCloseFinish()
        {
            IsAnimCloseProcess = false;
        }

        public void Show(string message, float duration)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }
            
            StartCoroutine(DoShow(message, duration));
        }

        public void UpdateMessage(string message)
        {
            _messageText.text = message;
            UiUtils.RebuildLayouts(this);
        }

        private IEnumerator DoShow(string message, float duration)
        {
            _messageText.text = message;
            SetWindowPosition();

            IsAnimOpenProcess = true;
            _animator.ResetTrigger(HideKey);
            _animator.SetBool(OpenedKey, true);
            while (IsAnimOpenProcess)
            {
                UpdateWindowPosition();
                yield return null;
            }

            while (duration > 0)
            {
                UpdateWindowPosition();
                duration -= Time.deltaTime;
                yield return null;
            }

            IsAnimCloseProcess = true;
            _animator.SetBool(OpenedKey, false);
            while (IsAnimCloseProcess)
            {
                UpdateWindowPosition();
                yield return null;
            }

            _animator.SetTrigger(HideKey);
        }
    }
}