using Entitas;

namespace Varwin.ECS.Systems.Loader
{
    /// <summary>
    /// Calculate when objects loaded
    /// </summary>
    public sealed class LoadCounterSystem : IExecuteSystem 
    {
        private readonly IGroup<GameEntity> _loadObjectsCounter;
        private readonly IGroup<GameEntity> _loadResourcesCounter;

        public LoadCounterSystem(Contexts contexts)
        {
            _loadObjectsCounter = contexts.game.GetGroup(GameMatcher.LoadObjectsCounter);
            _loadResourcesCounter = contexts.game.GetGroup(GameMatcher.LoadResourcesCounter);
        }

        public void Execute()
        {
            foreach (GameEntity entity in _loadObjectsCounter)
            {
                if (entity.loadObjectsCounter.PrefabsCount != entity.loadObjectsCounter.PrefabsLoaded || ProjectData.ObjectsAreLoaded)
                {
                    continue;
                }

                ProjectData.ObjectsAreLoaded = true;
                entity.loadObjectsCounter.LoadComplete = true;
                ProjectData.ObjectsWasLoaded();
            }
            
            foreach (GameEntity entity in _loadResourcesCounter)
            {
                if (entity.loadResourcesCounter.ResoursesCount > entity.loadResourcesCounter.ResoursesLoaded || ProjectData.ResourcesAreLoaded)
                {
                    continue;
                }

                ProjectData.ResourcesAreLoaded = true;
                entity.loadResourcesCounter.LoadComplete = true;
                ProjectData.ResourcesWasLoaded();
            }
        }

       
    }
}
