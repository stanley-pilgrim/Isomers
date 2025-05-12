using Varwin.PlatformAdapter;

namespace Varwin.UI
{
    using UnityEngine;

    namespace Varwin.UI
    {
        // ReSharper disable once InconsistentNaming
        public class UIPanel : PointableObject
        {
            public virtual void OnHover()
            {
            }

            public virtual void OnOut()
            {
            }

            public override void OnPointerIn()
            {
                OnHover();
            }

            public override void OnPointerOut()
            {
                OnOut();
            }

            public override void OnPointerDown()
            {
            }

            public override void OnPointerUp()
            {
            }

            public override void OnPointerUpAsButton()
            {
            }
        }
    }
}