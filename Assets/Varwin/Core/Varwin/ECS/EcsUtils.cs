using Entitas;

namespace Varwin
{
    public static class EcsUtils
    {
        public static void Destroy(GameEntity entity)
        {
            if (entity.hasWrapper)
            {
                entity.wrapper.Value = null;
            }
                
            entity.Destroy();
        }
    }
}