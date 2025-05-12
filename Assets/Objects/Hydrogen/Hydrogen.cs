using UnityEngine;
using Varwin;
using Varwin.Public;
using Varwin.SocketLibrary;

namespace Varwin.Types.Hydrogen_71a42bba03f64d75b6e0183fd089c472
{
    [VarwinComponent(English: "Hydrogen", Russian: "Водород")]
    public class Hydrogen : VarwinObject
    {
        private bool _hasSocketController;

        [Variable(English: "Has socket controller", Russian: "есть socket controller")]
        public bool HasSocketController
        {
            get => _hasSocketController;
            internal set => _hasSocketController = value;
        }
        private void Start()
        {
            SocketController socketController = GetComponentInParent<SocketController>();
            HasSocketController = (socketController != null);
        }
    }
}
