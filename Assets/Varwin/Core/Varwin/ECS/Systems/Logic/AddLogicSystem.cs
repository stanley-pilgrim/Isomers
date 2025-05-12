using Entitas;

namespace Varwin.ECS.Systems
{
    /// <summary>
    /// Load group? Add Group logic
    /// </summary>
    public sealed class AddLogicSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _logics;
        public AddLogicSystem(Contexts contexts)
        {
            _logics = contexts.game.GetGroup(GameMatcher.AllOf( GameMatcher.Type, GameMatcher.ChangeGroupLogic, GameMatcher.Logic));
        }

        public void Execute()
        {
            if (!ProjectData.ObjectsAreLoaded)
            {
                return;
            }

            if (ProjectData.GameMode == GameMode.Edit)
            {
                return;
            }

            foreach (GameEntity gameEntity in _logics.GetEntities())
            {
                if (gameEntity.type.Value != null)
                {
                    gameEntity.logic.Value.UpdateGroupLogic(gameEntity.type.Value);
                    gameEntity.RemoveChangeGroupLogic();
                }
            }
        }
    }
}
