using System.Collections.Generic;
using Varwin.Data;

namespace Varwin.WWW.Models
{
    public class ProjectsList : IJsonSerializable
    {
        public int TotalCount { get; set; }

        public List<ProjectItem> Edges { get; set; } = new List<ProjectItem>();
    }
}