using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Bifrost.Public.Sdk.Models
{
    [DataContract]
    public class Node
    {
        [DataMember]
        public Guid Id { get; set; }
        [DataMember]
        public string Endpoint { get; set; }
        [DataMember]
        public int Port { get; set; }

        public override string ToString()
        {
            return string.Format("Id: {0}, Endpoint: {1}:{2}", Id, Endpoint, Port);
        }
    }

    [Serializable]
    public class Circuit
    {
        public Guid Id { get; set; }
        public List<Node> Nodes { get; set; }
    }
}
