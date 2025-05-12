using Entitas;
using Varwin.Data.ServerData;

namespace Varwin.ECS.Systems.Group
{
    public sealed class LogicUpdateSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _entities;
        private readonly byte[] _assemblyBytes;
        private readonly int _sceneId;
        private readonly bool _isError;
        public LogicUpdateSystem(Contexts contexts, byte[] assemblyBytes, int sceneId, bool isError)
        {
            _entities = contexts.game.GetGroup(GameMatcher.Logic);
            _assemblyBytes = assemblyBytes;
            _sceneId = sceneId;
            _isError = isError;
        }

        public void Execute()
        {
            var projectScene = ProjectData.ProjectStructure.Scenes.GetProjectScene(_sceneId);
            projectScene.AssemblyBytes = _assemblyBytes;

            if (_sceneId != ProjectData.SceneId)
            {
                return;
            }

            foreach (GameEntity entity in _entities)
            {
                entity.ReplaceAssemblyBytes(_assemblyBytes, _isError);
            }
        }
    }
}
