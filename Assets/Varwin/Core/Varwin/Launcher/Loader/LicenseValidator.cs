using System;
using UnityEngine;
using Varwin.Data;
using Varwin.Data.ServerData;
using Varwin.Log;
using Varwin.UI;

namespace Varwin
{
    public static class LicenseValidator
    {
        public static bool LicenseKeyProvided(ProjectStructure projectStructure)
        {
            if (projectStructure.LicenseKey == null)
            {
                LauncherErrorManager.Instance.ShowFatal(ErrorHelper.GetErrorDescByCode(ErrorCode.LicenseKeyError), Environment.StackTrace);
                Debug.LogError("License information error");

                return false;
            }

            Debug.Log($"License key provided: {projectStructure.LicenseKey}");
            return true;
        }

        public static bool FillLicenseInfo(string licenseKey)
        {
            string decodedKey = ADec.GetProjectDecJson(licenseKey);
            var license = decodedKey.JsonDeserialize<License>();

            if (license == null)
            {
                return false;
            }

            if (license.ExpiresAt.HasValue && DateTime.Compare(DateTime.Now, license.ExpiresAt.Value) >= 0)
            {
                license.Code = Edition.Starter;
            }

            LicenseInfo.Value = license;

            Debug.Log($"License is ok! Edition: {LicenseInfo.Value.Code}");

            return true;
        }
    }
}