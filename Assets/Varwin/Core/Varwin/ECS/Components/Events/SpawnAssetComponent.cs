using Entitas;
using Varwin.Data;

namespace Varwin.ECS.Components.Events
{
    public sealed class SpawnAssetComponent : IComponent
    {
        /// <summary>
        /// Params for spawn
        /// </summary>
        public SpawnInitParams SpawnInitParams;
    }
}
