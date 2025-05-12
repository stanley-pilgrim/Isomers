﻿using System.Collections;
using TMPro;
using UnityEngine;

namespace Varwin
{
    public class AuthorAttributionText : MonoBehaviour
    {
        public TextMeshProUGUI Text;

        private void Awake()
        {
            AuthorAttribution.AuthorLabel = this;
            gameObject.SetActive(false);
            transform.parent.gameObject.SetActive(false);
        }

        public void UpdateText(AuthorAttribution.Attribution attribution)
        {
            transform.parent.gameObject.SetActive(true);
            string user = string.IsNullOrEmpty(attribution.CompanyName) ? attribution.AuthorName : attribution.CompanyName;
            Text.text = $"{attribution.ProjectName} by {user}\n<size=19><color=#000b>Licensed under {attribution.LicenseCode}</color></size>";

            if (!string.IsNullOrEmpty(attribution.Url))
            {
                Text.text += $"\n{attribution.Url}";
            }
            UiUtils.RebuildLayouts(transform.parent.gameObject);
        }
    }
}
