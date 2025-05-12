using System;
using System.Globalization;
using System.IO;
using Varwin.Log;
using Newtonsoft.Json;
using SmartLocalization;
using UnityEngine;
using Varwin.Data;
using Varwin.Data.ServerData;
using Varwin.PUN;
using Varwin.UI;

namespace Varwin
{
    public class Settings
    {
        public string ApiHost { get; set; }

        // For test purposes in Unity Editor. Change in settings.txt file. Values: {debug - run with ctrl + ~, debug1 - run with ctrl + 1}
        public string Language { get; set; }
        public string StoragePath { get; set; }
        public string DebugFolder { get; set; }
        public string WebHost { get; set; }
        public string RemoteAddress { get; set; }
        public string RemoteAddressPort { get; set; }
        public string RemoteWebHost { get; set; }
        public bool Multiplayer { get; set; }
        public bool Spectator { get; set; }
        public bool Education { get; set; }
        public string PhotonHost { get; set; }
        public string PhotonPort { get; set; }
        public string PhotonAppId { get; set; } = "85431403-f90b-40ac-973a-ac150ca6961e";//"1a198491-9d70-4f23-a274-cfbd2ce068a2";
        public bool HighlightEnabled { get; set; }
        public bool TouchHapticsEnabled { get; set; }
        public bool GrabHapticsEnabled { get; set; }
        public bool UseHapticsEnabled { get; set; }
        [Obsolete] public bool OnboardingMode { get; set; }

        private static Settings _instance;

        public static Settings Instance
        {
            get => _instance ??= new Settings
            {
                HighlightEnabled = true,
                TouchHapticsEnabled = false,
                GrabHapticsEnabled = false,
                UseHapticsEnabled = false,
                Multiplayer = false,
                Education = true,
                OnboardingMode = false,
                Language = LanguageManager.DefaultLanguage
            };
            set => _instance = value;
        }

        public Settings()
        {
            if (LanguageManager.Instance)
            {
                LanguageManager.Instance.OnChangeLanguage += UpdateLanguage;
            }
        }

        private void UpdateLanguage(LanguageManager languageManager)
        {
            Language = languageManager.CurrentlyLoadedCulture.languageCode;
        }

        // unity editor
        public static void ReadTestSettings()
        {
            string json = File.ReadAllText($"{Application.dataPath}/StreamingAssets/settings.txt");
            Instance = JsonConvert.DeserializeObject<Settings>(json, new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.None});
            if (!Launcher.Instance.LoadProjectFromStorage)
            {
                ProjectData.ExecutionMode = ExecutionMode.RMS;
                LoaderAdapter.Init(new ApiLoader());
            }
            else
            {
                ProjectData.ExecutionMode = ExecutionMode.EXE;
            }
        }

        // build
        public static void CreateStorageSettings(string folder)
        {
            //string outFolder = Application.dataPath + "/tempUnpacked";
            Debug.Log($"Create storage settings. Path = {folder}");
            Instance = new Settings
            {
                StoragePath = folder,
                HighlightEnabled = true,
                Language = CultureInfo.CurrentCulture.TwoLetterISOLanguageName,
#if WAVEVR
                Education = true,
                Multiplayer = true
#endif
            };

            LoaderAdapter.Init(new StorageLoader());
        }

        // debug laucher (sdk)
        public static void CreateDebugSettings(string path)
        {
            if (Instance != null)
            {
                return;
            }
            
            Instance = new Settings
            {
                DebugFolder = path,
                HighlightEnabled = true,
                TouchHapticsEnabled = false,
                GrabHapticsEnabled = false,
                UseHapticsEnabled = false,
                Language = LanguageManager.DefaultLanguage
            };

            LoaderAdapter.Init(new ApiLoader());
        }

        public static void SetupLanguageFromLaunchArguments(LaunchArguments launchArguments)
        {
            try
            {
                Instance.Language = launchArguments.lang ?? _instance.Language ?? LanguageManager.DefaultLanguage;

                LanguageManager.Instance.ChangeLanguage(launchArguments.lang);
            }
            catch (Exception e)
            {
                LauncherErrorManager.Instance.ShowFatal(ErrorHelper.GetErrorDescByCode(ErrorCode.ReadLaunchArgsError), e.ToString());
            }
        }

        public static void ReadServerConfig(ServerConfig serverConfig)
        {
            var uri = new Uri(Instance.ApiHost);

            Instance.WebHost = $@"{Instance.ApiHost}/widgets";
            Instance.RemoteAddress = serverConfig.remoteAddr;
            Instance.RemoteAddressPort = serverConfig.remoteAddrPort;
            LicenseFeatureManager.ActivateLicenseFeatures(serverConfig.appLicenseInfo.Edition ?? Edition.None);

            Instance.RemoteWebHost = $@"{uri.Scheme}://{Instance.RemoteAddress}:{Instance.RemoteAddressPort}";

            Instance.Language = Instance.Language ?? LanguageManager.DefaultLanguage;
        }

        public static void SetApiUrl(string url)
        {
            Instance.ApiHost = url;
        }
    }
}