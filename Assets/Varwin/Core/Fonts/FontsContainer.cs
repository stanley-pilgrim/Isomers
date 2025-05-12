using System.Linq;
using TMPro;
using UnityEngine;

namespace Varwin
{
    public static class FontsContainer
    {
        private const string UbuntuRegularName = "Ubuntu-Regular SDF";
        private const string PTSerifRegularName = "PTSerif-Regular SDF";
        private const string RobotoMonoRegularName = "RobotoMono-Regular SDF";
        private const string BadScriptRegularName = "BadScript-Regular SDF";

        private static TMP_FontAsset Ubuntu;
        private static TMP_FontAsset PtSerif;
        private static TMP_FontAsset RobotoMono;
        private static TMP_FontAsset BadScript;

        static FontsContainer()
        {
            Initialize();
        }

        public static TMP_FontAsset GetFont(Fonts font)
        {
            return font switch
            {
                Fonts.Ubuntu        => Ubuntu,
                Fonts.PtSerif       => PtSerif,
                Fonts.RobotoMono    => RobotoMono,
                Fonts.BadScript     => BadScript,
                _                   => Ubuntu
            };
        }

        public enum Fonts
        {
            [Item(English:"Ubuntu",Russian:"Ubuntu",Chinese:"Ubuntu",Kazakh:"Ubuntu",Korean:"Ubuntu")]
            Ubuntu,
            [Item(English:"PT Serif",Russian:"PT Serif",Chinese:"Pt Serif",Kazakh:"PT Serif",Korean:"PT Serif")]
            PtSerif,
            [Item(English:"Roboto Mono",Russian:"Roboto Mono",Chinese:"Roboto Mono",Kazakh:"Roboto Mono",Korean:"Roboto Mono")]
            RobotoMono,
            [Item(English:"BadScript",Russian:"BadScript",Chinese:"BadScript",Kazakh:"BadScript",Korean:"BadScript")]
            BadScript
        }

        private static void Initialize()
        {
            var fonts = Resources.LoadAll<TMP_FontAsset>("");

            Ubuntu = fonts.FirstOrDefault(x => x.name.Contains(UbuntuRegularName));
            PtSerif = fonts.FirstOrDefault(x => x.name.Contains(PTSerifRegularName));
            RobotoMono = fonts.FirstOrDefault(x => x.name.Contains(RobotoMonoRegularName));
            BadScript = fonts.FirstOrDefault(x => x.name.Contains(BadScriptRegularName));
        }
    }
}
