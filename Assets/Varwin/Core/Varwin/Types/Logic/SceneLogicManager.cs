using System;
using Core.Varwin;
using Varwin.Log;
using Varwin.UI;
using UnityEngine;

namespace Varwin.Types
{
    public static class SceneLogicManager
    {
        private static int _sceneId;

        private static SceneLogic SceneLogic { get; set; }

        public static void SetSceneId(int sceneId)
        {
            _sceneId = sceneId;
        }

        public static void UpdateGroupLogic(Type newLogic)
        {
            Clear();

            if (newLogic == null)
            {
                Debug.Log($"Scene {_sceneId} logic is empty!");
                return;
            }

            try
            {
                SceneLogic = new GameObject("[Varwin Scene Logic]").AddComponent(newLogic) as SceneLogic;
                Debug.Log($"Scene {_sceneId} logic initialize successful");
            }
            catch (Exception e)
            {
                Clear();
                CoreErrorManager.Error(e);
                ShowLogicExceptionError(ErrorCode.LogicInitError, "Initialize scene logic error!", e);
            }
        }

        public static void Clear()
        {
            LogicUtils.RemoveAllEventHandlers();
            BlocklyLogger.RefreshLogic();
            
            if (SceneLogic)
            {
                SceneLogic.Destroy();
                SceneLogic = null;
            }
        }
        
        private static void ShowLogicExceptionError(int errorCode, string errorMessage, Exception exception)
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
    }
}
