using Entitas;
// ReSharper disable All

namespace Varwin.ECS.Components.Events
{
    public sealed class LoadResourcesCounter : IComponent
    {
        public int ResoursesCount;
        public int ResoursesLoaded;
        public bool LoadComplete;
    }
}