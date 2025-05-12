namespace Varwin.ECS.Systems.Compiler
{
    public sealed class ReflectionSystems : Feature
    {
        public ReflectionSystems(Contexts contexts)
        {
            Add(new LoadAssemblySystem(contexts));
            Add(new TypeAnalizatorSystem(contexts));
        }
    }
}
