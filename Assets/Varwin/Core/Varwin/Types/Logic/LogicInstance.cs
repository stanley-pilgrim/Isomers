using System;
using Core.Varwin;
using UnityEngine;
using Varwin.Log;
using Varwin.UI;

namespace Varwin.Types
{
    [Obsolete]
    public class LogicInstance
    {
        public ILogic Logic { get; private set; }

        private readonly int _sceneId;
        private WrappersCollection _myItems;
        private bool _initialized;

        public LogicInstance(int sceneId)
        {
            _sceneId = sceneId;
            GameStateData.SetLogic(this);
        }

        public void UpdateGroupLogic(Type newLogic)
        {
            LogicUtils.RemoveAllEventHandlers();

            if (newLogic == null)
            {
                Debug.Log($"Scene {_sceneId} logic is empty!");
                Clear();

                return;
            }

            _myItems = GameStateData.GetWrapperCollection();

            var logic = Activator.CreateInstance(newLogic) as ILogic;

            if (logic == null)
            {
                string msg = ErrorHelper.GetErrorDescByCode(ErrorCode.LogicInitError);
                PopupWindowManager.ShowPopup(msg, true);

                Debug.LogError($"Initialize scene logic error! SceneId = {_sceneId}. Message: Logic is null!");

                CoreErrorManager.Error(new Exception(msg));

                return;
            }

            Logic = logic;

            try
            {
                Debug.Log($"Scene {_sceneId} logic initialize started...");
                InitializeLogic();
                Debug.Log($"Scene {_sceneId} logic initialize successful");
            }
            catch (Exception e)
            {
                CoreErrorManager.Error(e);
                ShowLogicExceptionError(ErrorCode.LogicInitError, "Initialize scene logic error!", e);
                Logic = null;
            }
        }

        public void InitializeLogic()
        {
            _initialized = false;
            Logic.SetCollection(_myItems);
            Logic.Events();
            Logic.Initialize();
            _initialized = true;
        }

        public void ExecuteLogic()
        {
            if (Logic == null || !_initialized)
            {
                return;
            }

            try
            {
                Logic.Update();
            }
            catch (Exception e)
            {
                ShowLogicExceptionError(ErrorCode.LogicExecuteError, "Execute scene logic error!", e);
                Logic = null;
            }
        }

        private void ShowLogicExceptionError(int errorCode, string errorMessage, Exception exception)
        {
            var logicException = new LogicException(_sceneId, errorMessage, exception);
            Debug.LogError($"{errorMessage} {logicException.GetStackFrameString()}");
            CoreErrorManager.Error(logicException);
            PopupWindowManager.ShowPopup(ErrorHelper.GetErrorDescByCode(errorCode), true);
            
            object command = new
            {
                command = PipeCommandType.RuntimeError,
                sceneId = logicException.SceneId,
                line = logicException.Line,
                column = logicException.Column,
                errorMessage = logicException.Message,
                projectId = ProjectData.ProjectId
            };

            if (CommandPipe.Instance)
            {
                CommandPipe.Instance.SendPipeCommand(command);
            }
        }

        public void Clear()
        {
            LogicUtils.RemoveAllEventHandlers();
            Logic = null;
        }
    }
}