using System;
using System.Collections.Generic;
using Entitas;
using UnityEngine;
using Varwin.Data;
using Varwin.Log;
using Varwin.Types;
using Varwin.UI;
using Varwin.WWW;

namespace Varwin.ECS.Systems.Compiler
{
    public sealed class LoadAssemblySystem : ReactiveSystem<GameEntity>
    {
        public LoadAssemblySystem(Contexts context) : base(context.game)
        {
        }

        protected override ICollector<GameEntity> GetTrigger(IContext<GameEntity> context) =>
            context.CreateCollector(GameMatcher.AssemblyBytes);

        protected override bool Filter(GameEntity entity) => entity.hasAssemblyBytes & entity.hasType;

        protected override void Execute(List<GameEntity> entities)
        {
            foreach (var entity in entities)
            {
                if (entity.assemblyBytes.IsError)
                {
                    PopupWindowManager.ShowPopup(ErrorHelper.GetErrorDescByCode(ErrorCode.CompileCodeError), true);
                }

                var request = new RequestLoadAssembly(entity.assemblyBytes.Value);
                Debug.Log($"Load assembly started... Entity: {entity}");

                request.OnFinish += response =>
                {
                    ResponseLoadAssembly responseLoadAssembly = (ResponseLoadAssembly) response;
                    Type compiledType = responseLoadAssembly.CompiledType;
                    entity.ReplaceType(compiledType);

                    if (compiledType == null)
                    {
                        
                    }
                    else if (compiledType.BaseType == typeof(SceneLogic))
                    {
                        GameStateData.ClearLogic();
                        SceneLogicManager.UpdateGroupLogic(compiledType);
                    }

                    Debug.Log($"<Color=Green><b>Loading assembly successful! Time = {responseLoadAssembly.Milliseconds} ms.</b></Color>");
                };
            }
        }
    }
}
