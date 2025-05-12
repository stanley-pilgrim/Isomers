using System;
using UnityEngine;
using UnityEngine.UI;
using Varwin.PlatformAdapter;

namespace Varwin.UI.VRMessageManager
{
    // ReSharper disable once InconsistentNaming
    public class VRMessageManager : MonoBehaviour
    {
        public Vector3 LocalPositionOnHead;
        public GameObject Panel;
        public Text Message;
        public static VRMessageManager Instance;
        public Action<DialogResult> Result = delegate {  };

        public bool IsShowing => Panel.activeSelf;

        private void Awake()
        {
            Instance = this;
        }

        public void Show(string message, DialogButtons buttons = DialogButtons.YesNoCancel)
        {
            Message.text = message;
            Panel.SetActive(true);
        }

        public void Hide()
        {
            Panel.SetActive(false);
        }

        public void SetResult(DialogResult result)
        {
            Hide();
            Result(result);
        }
        

    }
    public enum DialogButtons
    {
        Ok, YesNo, YesNoCancel
    }

    public enum DialogResult
    {
        Yes, No, Cancel
    }
}
