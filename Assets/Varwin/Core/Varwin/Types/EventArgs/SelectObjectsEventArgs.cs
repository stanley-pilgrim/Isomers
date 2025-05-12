using System;
using System.Collections.Generic;

namespace Varwin
{
    public sealed class SelectObjectsEventArgs : EventArgs
    {
        public List<ObjectController> ObjectControllers;
    }
}