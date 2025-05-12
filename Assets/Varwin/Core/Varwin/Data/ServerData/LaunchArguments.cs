// ReSharper disable InconsistentNaming

namespace Varwin.Data.ServerData
{
    public class LaunchParameters : IJsonSerializable
    {
        public string apiBaseUrl { get; set; }
        public string accessToken { get; set; }
        public string refreshToken { get; set; }
    }

    public class LaunchArguments : IJsonSerializable
    {
        public MultiplayerMode? multiplayerMode { get; set; }
        public GameMode gm { get; set; }
        public PlatformMode platformMode { get; set; }
        public string lang { get; set; }
        public int sceneId { get; set; }
        public int projectId { get; set; }
        public int projectConfigurationId { get; set; }
    }
}