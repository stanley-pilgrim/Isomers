using System;
using System.Collections.Generic;
using Varwin.Data;
using Varwin.Data.ServerData;

namespace Varwin.WWW.Models
{
    public class ProjectItem : IJsonSerializable
    {
        public int Id { get; set; }
        public string Name { get; set; }
        
        public string Guid { get; set; }
        public int SceneCount { get; set; }
        public List<ProjectConfiguration> Configurations { get; set; }
        public ProjectType ProjectType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public bool IsPc => ProjectType?.IsPc ?? false;
        public bool IsMobile => ProjectType?.IsMobile ?? false;

        public bool IsLocal { get; set; }
        
        public bool Multiplayer { get; set; }
        
        public string ProjectPath { get; set; }
        
        public string Cursor { get; set; }
    }
}