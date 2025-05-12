using UnityEditor;
using UnityEngine;

namespace Varwin.Editor
{
    public static class VarwinStyles
    {
        public static Color LinkColor => Color.Lerp(Color.cyan, Color.blue, 0.5f);
        
        public static readonly GUIStyle LinkStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal =
            {
                textColor = LinkColor
            }
        };

        public static bool Link(string url)
        {
            return Link(url, url);
        }
        
        public static bool Link(string text, string url)
        {
            var pressed = GUILayout.Button(text, LinkStyle);
            if (pressed)
            {
                Application.OpenURL(url);
            }
            return pressed;
        }

        public static bool Minus(Rect position, string tooltip = "Remove this item from list")
        {
            var minusIcon = EditorGUIUtility.TrIconContent("Toolbar Minus", tooltip);
            var preButton = (GUIStyle) "RL FooterButton";

            return GUI.Button(position, minusIcon, preButton);
        }
    }
}
