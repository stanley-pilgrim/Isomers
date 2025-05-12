using System.Collections.Generic;

namespace Varwin.WWW.Models
{
    public class WorkspacesList
    {
        public int TotalCount { get; set; }

        public List<WorkspaceItem> Edges { get; set; } = new List<WorkspaceItem>();
    }
}