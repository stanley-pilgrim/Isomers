using System.IO;
using UnityEngine;

namespace Varwin
{
    public static class UnityProject
    {
        private static string _path = null;
        
        public static string Path => _path ?? (_path = new DirectoryInfo(Application.dataPath).Parent?.FullName.Replace("\\", "/"));

        public static string Assets => $"{Path}/Assets";
        public static string Library => $"{Path}/Library";
        public static string Packages => $"{Path}/Packages";
    }
}