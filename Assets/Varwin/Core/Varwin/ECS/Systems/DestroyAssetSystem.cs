using Entitas;
using UnityEngine;

namespace Varwin.ECS.Systems
{
    public sealed class DestroyAssetSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _prefabEntities;
        private readonly IGroup<GameEntity> _gameObjectEntities;

        public DestroyAssetSystem(Contexts contexts)
        {
            _prefabEntities = contexts.game.GetGroup(GameMatcher.AllOf(GameMatcher.ServerObject));
            _gameObjectEntities = contexts.game.GetGroup(GameMatcher.AllOf(GameMatcher.GameObject));
        }

        public void Execute()
        {
            foreach (GameEntity entity in _prefabEntities.GetEntities())
            {
                EcsUtils.Destroy(entity);
            }

            foreach (GameEntity gameObjectEntity in _gameObjectEntities.GetEntities())
            {
                Object.Destroy(gameObjectEntity.gameObject.Value);
                gameObjectEntity.Destroy();
            }
        }
    }
}