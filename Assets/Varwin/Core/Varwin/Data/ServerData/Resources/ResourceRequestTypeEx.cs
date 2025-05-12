using System.Collections.Generic;
using System.Linq;

namespace Varwin.Data.ServerData
{
    public static class ResourceRequestTypeEx
    {
        public static string ToSearchString(this ResourceRequestType resourceRequestType)
        {
            string[] formats = resourceRequestType.ToSearchArray();

            return string.Join(",", formats.Select(x => $"\"{x}\""));
        }

        public static string[] ToSearchArray(this ResourceRequestType resourceRequestType)
        {
            IEnumerable<string> formats;
            
            switch (resourceRequestType)
            {
                case ResourceRequestType.All:
                    formats = ResourceFormatCodes.AllFormats;
                    break;
                case ResourceRequestType.Image:
                    formats = ResourceFormatCodes.ImageFormats;
                    break;
                case ResourceRequestType.Model:
                    formats = ResourceFormatCodes.ModelFormats;
                    break;
                case ResourceRequestType.TextFile:
                    formats = ResourceFormatCodes.TextFormats;
                    break;
                case ResourceRequestType.Audio:
                    formats = ResourceFormatCodes.AudioFormats;
                    break;
                case ResourceRequestType.Video:
                    formats = ResourceFormatCodes.VideoFormats;
                    break;
                default:
                    return null;
            }

            return formats.ToArray();
        }
    }
}