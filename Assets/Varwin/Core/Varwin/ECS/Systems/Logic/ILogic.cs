using System;

namespace Varwin
{
    [Obsolete]
    public interface ILogic
    {
        void SetCollection(WrappersCollection collection);
        void Initialize();
        void Update();
        void Events();
        // TODO: На данный момент многие логики без Destroy, потому придется перекомпилировать все логики сцен. Пока что сделаю через рефлексию, через пару месяцев перейдем на интерфейс
        // см. LogicInstance.UpdateGroupLogic
        // void Destroy(); 
    }
}
