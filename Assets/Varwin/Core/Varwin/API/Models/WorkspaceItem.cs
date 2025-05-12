using System;
using Varwin.Data;

namespace Varwin.WWW.Models
{
    public class WorkspaceItem : IJsonSerializable
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}