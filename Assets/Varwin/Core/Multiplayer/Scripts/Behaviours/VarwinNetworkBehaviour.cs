using Unity.Netcode;
using UnityEngine;
using Varwin.Core.Behaviours;

namespace Varwin.Multiplayer.NetworkBehaviours.v2
{
    public abstract class VarwinNetworkBehaviour : NetworkBehaviour
    {
        /// <summary>
        /// Минимальная дельта изменения для флоатов
        /// </summary>
        private const float FloatTolerance = 0.01f;

        /// <summary>
        /// Инициализация сетевого поведения
        /// </summary>
        /// <param name="varwinBehaviour">Синхронизируемое Varwin поведение</param>
        public abstract void InitializeNetworkBehaviour(VarwinBehaviour varwinBehaviour);

        /// <summary>
        /// Проверка на разницу значений у двух флоатов
        /// </summary>
        /// <param name="a">Первый флоат</param>
        /// <param name="b">Второй фолат</param>
        /// <returns>true если флоаты различаются, false если флоаты одинаковые</returns>
        protected bool IsFloatDifferent(float a, float b) => FastAbs(b - a) > FloatTolerance;

        /// <summary>
        /// Оптимизированный метод получения абсолютного числа
        /// </summary>
        /// <param name="x">Число</param>
        /// <returns>Абсолютной число</returns>
        protected static float FastAbs(float x) => Mathf.Sign(x) * x;
    }
}