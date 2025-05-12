using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Varwin.Public;

namespace Varwin.UI
{
    public class PlatformModeUsageErrorWindow : MonoBehaviour
    {
        private static PlatformModeUsageErrorWindow _instance;
        
        public TMP_Text MessageField;
        public TMP_Text TitleField;
        public GameObject RootObject;
        public Image IconField; 
        public List<IconContainer> IconContainers;
        
        private void Awake()
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void OnButtonClick()
        {
            Application.Quit();
        }

        public static void ShowError(string title, string message, PlatformMode mode)
        {
            if (!_instance)
            {
                return;
            }

            _instance.TitleField.text = title;
            _instance.MessageField.text = message;
            var targetContainer = _instance.IconContainers?.Find(a => a.Platform == mode);
            if (targetContainer != null)
            {
                _instance.IconField.sprite = targetContainer.Icons.GetValue(Settings.Instance.Language);
                _instance.IconField.color = targetContainer.ColorTint;
            }
             
            _instance.RootObject.gameObject.SetActive(true);
        }

        [Serializable]
        public class IconContainer
        {
            public LocalizedDictionary<Sprite> Icons;
            public PlatformMode Platform;
            public Color ColorTint;
        }
    }
}