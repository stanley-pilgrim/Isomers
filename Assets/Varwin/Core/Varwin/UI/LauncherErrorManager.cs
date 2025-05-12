using System;
using System.Diagnostics;
using SmartLocalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Varwin.Data;
using Varwin.PUN;

namespace Varwin.UI
{
    // ReSharper disable once InconsistentNaming
    public class LauncherErrorManager : MonoBehaviour
    {
        public static LauncherErrorManager Instance;
        
        public GameObject Panel;
        public GameObject LoadAnim;
        public TMP_Text Feedback;
        public Button CopyErrorButton;
        private bool fatalError;
        public TMP_Text LicensedTo;

        public UnityEvent showedError;

        [SerializeField] private Button _exitButton;
        [SerializeField] private TextMeshProUGUI _errorDetails;
        
        [Space, Header("Localized Title")] 
        [SerializeField] private GameObject _localizedTextRoot;
        [SerializeField] private TextMeshProUGUI _localizedText;

        [Space, Header("Documentation")]
        [SerializeField] private GameObject _documentationButtonRoot;
        [SerializeField] private Button _documentationButton;
        
        [Space, Header("Console")]
        [SerializeField] private GameObject _consoleButtonRoot;
        [SerializeField] private Button _consoleButton;

        private string _lastErrorLocalizedKey;

        private string LocalizedHeader
        {
            get => _localizedHeader;
            set
            {
                _localizedHeader = value;
                _localizedTextRoot.SetActive(!string.IsNullOrEmpty(_localizedHeader));
                _localizedText.text = _localizedHeader;
            }
        }
        private string DocumentationUrl
        {
            get => _documentationUrl;
            set
            {
                _documentationUrl = value;
                _documentationButtonRoot.SetActive(!string.IsNullOrEmpty(_documentationUrl));
            }
        }

        private string _localizedHeader;
        private string _documentationUrl;

        private void Awake()
        {
            Instance = this;
            Panel.SetActive(false);

            LanguageManager.Instance.OnChangeLanguage += OnChangeLanguage;
            CopyErrorButton.onClick.AddListener(CopyError);

            _exitButton.onClick.AddListener(Exit);
            _documentationButton.onClick.AddListener(OpenDocumentation);
        }

        private void OnDestroy()
        {
            if (LanguageManager.Instance)
            {
                LanguageManager.Instance.OnChangeLanguage -= OnChangeLanguage;
            }

            if (CopyErrorButton)
            {
                CopyErrorButton.onClick.RemoveAllListeners();
            }
        }

        /// <summary>
        /// Открыть панель с ошибкой запуска с возможностью перехода в документацию.
        /// </summary>
        /// <param name="localizedErrorType">Локализованный хэдер ошибки.</param>
        /// <param name="errorDetails">Детали ошибки (stack trace etc...)</param>
        /// <param name="documentationUrl">Ссылка на документацию.</param>
        public void ShowDocumentedError(string localizedErrorType, string errorDetails, string documentationUrl)
        {
            _localizedText.text = localizedErrorType;
            _errorDetails.text = errorDetails;

            fatalError = true;

            Panel.SetActive(true);
            LoadAnim.SetActive(false);

            DocumentationUrl = documentationUrl;
        }

        public void Show(string message, string details = null)
        {
#if !UNITY_ANDROID
            Feedback.text = "";
#endif
            LocalizedHeader = message;
            DocumentationUrl = null;
            _errorDetails.text = details;
            _consoleButtonRoot.gameObject.SetActive(!string.IsNullOrWhiteSpace(details));

            if (!string.IsNullOrWhiteSpace(details))
            {
                LocalizedHeader += $"\n{LanguageManager.Instance.GetTextValue("OPEN_CONSOLE_FOR_MORE_INFO")}";
            }

            Panel.SetActive(true);
            LoadAnim.SetActive(false);
            
            showedError?.Invoke();
        }

        public void ShowFatal(string message, string details)
        {
#if !UNITY_ANDROID
            Feedback.text = "";
#endif
            LocalizedHeader = message;
            _errorDetails.text = details;
            _consoleButtonRoot.gameObject.SetActive(!string.IsNullOrWhiteSpace(details));
            
            if (!string.IsNullOrWhiteSpace(details))
            {
                LocalizedHeader += $"\n{LanguageManager.Instance.GetTextValue("OPEN_CONSOLE_FOR_MORE_INFO")}";
            }
            
            fatalError = true;
            //ToDo retry rename to send and action change to send
            Panel.SetActive(true);
            LoadAnim.SetActive(false);
            DocumentationUrl = null;
            
            showedError?.Invoke();
        }

        public void ShowFatalErrorKey(string errorLocalizedKey, string details)
        {
            _lastErrorLocalizedKey = errorLocalizedKey;
            ShowFatal(LanguageManager.Instance.GetTextValue(_lastErrorLocalizedKey), details);
        }

        private void Hide()
        {
            Panel.SetActive(false);
            LoadAnim.SetActive(true);
            _lastErrorLocalizedKey = null;
        }

        public void ReTryOrSendReport()
        {
            if (fatalError)
            {
                Hide();
                ProjectDataListener.Instance.ForceReload();
                Launcher.Instance?.Init();
            }
        }

        public void License(License license)
        {
            string user;

            if (string.IsNullOrEmpty(license.Company))
            {
                user = license.FirstName + " " + license.LastName;
            }
            else
            {
                user = license.Company;
            }
            
            LicensedTo.gameObject.SetActive(true);
            LicensedTo.text = $"Licensed to {user}\n<size=19><color=#000b>{license.Code} Edition</color></size>";
        }

        public void Exit()
        {
            if (Application.isEditor)
            {
                Panel.SetActive(false);
                return;
            }

            Application.Quit();
        }

        private void OnChangeLanguage(LanguageManager languageManager)
        {
            if (!string.IsNullOrEmpty(_lastErrorLocalizedKey))
            {
                LocalizedHeader = LanguageManager.Instance.GetTextValue(_lastErrorLocalizedKey);
            }
        }

        private void OpenDocumentation()
        {
            if (!string.IsNullOrEmpty(DocumentationUrl) && Uri.IsWellFormedUriString(DocumentationUrl, UriKind.Absolute))
            {
                Process.Start(DocumentationUrl);
            }
        }

        private void CopyError()
        {
            var textEditor = new TextEditor();
            textEditor.text = _errorDetails.text;
            textEditor.SelectAll();
            textEditor.Copy();
        }
    }
}
