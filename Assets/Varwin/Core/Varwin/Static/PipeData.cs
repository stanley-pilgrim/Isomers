using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Varwin
{
    public enum PipeCommandType
    {
        [EnumMember(Value = "launch")] Launch,
        [EnumMember(Value = "terminate")] Terminate,
        [EnumMember(Value = "runtime_blocks")] RunningBlocks,
        [EnumMember(Value = "runtime_error")] RuntimeError,
        [EnumMember(Value = "restart")] Restart
    }

    public class PipeCommand
    {
        public class PipeLaunchCommandData
        {
            public string Lang;
            public int ProjectId;
            public int SceneId;
            public int ProjectConfigurationId;
            public PlatformMode PlatformMode;

            public GameMode LaunchMode;

            public string AccessToken;
            public string RefreshToken;
            public string ApiBaseUrl;
            public int WorkspaceId;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public PipeCommandType Command;

        public PipeLaunchCommandData LaunchParams;
    }
}