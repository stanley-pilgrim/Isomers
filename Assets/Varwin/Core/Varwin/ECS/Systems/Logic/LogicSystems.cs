﻿namespace Varwin.ECS.Systems
{
    public sealed class LogicSystems : Feature
    {
        public LogicSystems(Contexts contexts)
        {
            Add(new AddLogicSystem(contexts));
            Add(new LogicExecuteSystem(contexts));
        }
    }
}
