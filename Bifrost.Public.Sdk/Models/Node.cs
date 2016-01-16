using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bifrost.Public.Sdk.Models
{
    [Serializable]
    public class Node
    {
        public Guid Id { get; set; }
        public string ControlAddress { get; set; }
    }

    [Serializable]
    public class Circuit
    {
        public Guid Id { get; set; }
        public List<Node> Nodes { get; set; }
    }
}
