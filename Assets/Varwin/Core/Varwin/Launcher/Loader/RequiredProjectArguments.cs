namespace Varwin.Data
{
    public struct RequiredProjectArguments
    {
        public int ProjectId;
        public int SceneId;
        public string Guid;
        public GameMode GameMode;
        public PlatformMode PlatformMode;
        public int ProjectConfigurationId;
        public bool ForceReloadProject;
    }
}