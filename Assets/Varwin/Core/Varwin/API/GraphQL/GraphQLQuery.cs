using System.Collections.Generic;
using Newtonsoft.Json;
using Varwin.Data;

namespace Varwin.GraphQL
{
    public enum GraphRequestType
    {
        Query,
        Mutation
    }

    public class GraphQLQuery : IJsonSerializable
    {
        public string Query { get; set; }
        public Dictionary<string, object> Variables { get; set; }
        [JsonIgnore] public GraphRequestType Type { get; set; }
    }
}