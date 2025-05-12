using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Varwin.Multiplayer;
using Varwin.Public;

namespace Varwin
{
    public abstract class SceneLogic : MonoBehaviour
    {
        public struct LogicVariable
        {
            private dynamic _value;

            
            public delegate IEnumerator ValueChangedHandler(dynamic oldValue, dynamic newValue);
            public event ValueChangedHandler ValueChanged;

            public dynamic Value
            {
                get => _value;
                set
                {
                    dynamic oldValue = _value;
                    _value = value;
                    if (VCompare.Equals(oldValue, _value))
                    {
                        return;
                    }

                    if (ValueChanged != null)
                    {
                        SceneLogic.Instance.StartCoroutine(ValueChanged.Invoke(oldValue, _value));
                    }
                }
            }

            public dynamic Get()
            {
                return Value;
            }

            public void Set(dynamic value)
            {
                Value = value;
            }

            public override string ToString()
            {
                return _value.ToString();
            }

            public static bool operator ==(LogicVariable left, LogicVariable right)
            {
                return VCompare.Equals(left.Value, right.Value);
            }

            public static bool operator !=(LogicVariable left, LogicVariable right)
            {
                return VCompare.NotEquals(left.Value, right.Value);
            }

            public static bool operator >(LogicVariable left, LogicVariable right)
            {
                return VCompare.Greater(left.Value, right.Value);
            }

            public static bool operator <(LogicVariable left, LogicVariable right)
            {
                return VCompare.Less(left.Value, right.Value);
            }
            
            public static bool operator >=(LogicVariable left, LogicVariable right)
            {
                return VCompare.GreaterOrEquals(left.Value, right.Value);
            }

            public static bool operator <=(LogicVariable left, LogicVariable right)
            {
                return VCompare.LessOrEquals(left.Value, right.Value);
            }
        }

        public static SceneLogic Instance { get; private set; }

        public static bool ScenePreparing { get; private set; }
        public static string ScenePreparingText { get; set; }
        public static DateTime ScenePreparingUtcStartTime { get; set; }

        protected WrappersCollection Collection;

        private void Awake()
        {
            Instance = this;

            Collection = GameStateData.GetWrapperCollection();
            UIFadeInOutController.IsBlocked = true;

            OnGameModeChanging(ProjectData.GameMode);
            ProjectData.GameModeChanging += OnGameModeChanging;
            ProjectData.PlatformModeChanged += OnPlatformModeChanged;

            Scene.LoadingStarted += OnVarwinSceneLoadingStarted;
            Configuration.LoadingStarted += OnVarwinConfigurationLoadingStarted;
        }

        private IEnumerator Start()
        {
            Collection.Init();
            Events();

            ScenePreparing = true;
            yield return AwaitOtherPlayers();
            ScenePreparingUtcStartTime = DateTime.UtcNow;
            yield return StartCoroutine(PrepareScene());
            ScenePreparing = false;

            UIFadeInOutController.IsBlocked = false;

            yield return StartCoroutine(Init());

            Setup();
            InitUpdate();

            yield return null;
        }

        protected virtual void Events()
        {
        }

        private IEnumerator AwaitOtherPlayers()
        {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
            {
                yield break;
            }

            while (((VarwinNetworkManager)NetworkManager.Singleton).StartProjectPlayerCount > NetworkManager.Singleton.ConnectedClients.Count)
            {
                yield return null;
            }
        }
        
        protected virtual IEnumerator PrepareScene()
        {
            int runningCoroutinesCounter = 0;

            // generated code...

            while (runningCoroutinesCounter > 0)
            {
                yield return null;
            }

            yield return true;
        }

        protected virtual void Setup()
        {
            // generated code...
        }

        protected virtual void OnPlatformModeChangedToVr()
        {
            // generated code...
        }

        protected virtual void OnPlatformModeChangedToDesktop()
        {
            // generated code...
        }
        
        protected virtual void OnPlatformModeChangedToNettleDesk()
        {
            // generated code...
        }
        

        [Obsolete]
        protected virtual IEnumerator Init()
        {
            yield return true;
        }

        [Obsolete]
        protected virtual void InitUpdate()
        {
        }

        private void OnPlatformModeChanged(PlatformMode platformMode)
        {
            if (platformMode == PlatformMode.Desktop)
            {
                OnPlatformModeChangedToDesktop();
            }
            else if (platformMode == PlatformMode.Vr)
            {
                OnPlatformModeChangedToVr();
            }
            else if (platformMode == PlatformMode.NettleDesk)
            {
                OnPlatformModeChangedToNettleDesk();
            }
        }

        private void OnGameModeChanging(GameMode newMode)
        {
            if (gameObject)
            {
                gameObject.SetActive(ProjectData.IsPlayMode);
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Clear()
        {
            Collection = null;
            LogicUtils.RemoveAllEventHandlers();
            CollisionDispatcher.RemoveAllGlobalEventHandlers();
            StopAllCoroutines();
            Collection = null;
        }

        public void Destroy()
        {
            Clear();
            
            ProjectData.GameModeChanging -= OnGameModeChanging;
            ProjectData.PlatformModeChanged -= OnPlatformModeChanged;

            Scene.LoadingStarted -= OnVarwinSceneLoadingStarted;
            Configuration.LoadingStarted -= OnVarwinConfigurationLoadingStarted;

            Destroy(gameObject);
        }

        protected Coroutine RestartCoroutine(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }

        private void OnVarwinSceneLoadingStarted(string sid)
        {
            StopAllCoroutines();
            Destroy();
        }

        private void OnVarwinConfigurationLoadingStarted(string sid)
        {
            StopAllCoroutines();
            Destroy();
        }

        public static void SetScenePreparationText(string text)
        {
            ScenePreparingText = text;
        }
    }
}