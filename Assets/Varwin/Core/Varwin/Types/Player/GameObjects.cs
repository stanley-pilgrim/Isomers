using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Varwin.UI;
using Varwin.PlatformAdapter;

namespace Varwin
{
    public class GameObjects : MonoBehaviour
    {
        public static GameObjects Instance;

        #region Player Transforms

        public Transform PlayerRig;
        public Transform EditPoint;
        public Transform SpawnPoint;
        public Transform Head;
        public Transform LeftHand;
        public Transform RightHand;
        public Transform TipAttach;

        #endregion

        #region UI prefabs

        public GameObject UIID;
        public GameObject UIObject;
        public GameObject UIToolTip;

        public GameObject Load;

        #endregion

        public Dictionary<string, GameObject> MagnetObjects = new Dictionary<string, GameObject>();

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            StartCoroutine(WaitPointer());
        }

        private IEnumerator WaitPointer()
        {
            GameObject pointer = null;

            while (pointer == null)
            {
                yield return new WaitForEndOfFrame();
                pointer = InputAdapter.Instance.PlayerController.Nodes.RightHand.GameObject;
            }

            yield return true;
        }
    }
}