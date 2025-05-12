using Varwin.WWW;

namespace Varwin.Data
{
    public class ResponseGraph : IResponse
    {
        public string Status;
        public string Message;
        public string Code;
        public string Data;
        public long ResponseCode;
    }
}