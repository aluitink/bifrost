using System.Collections.Generic;
using System.Runtime.Serialization;
using Bifrost.Public.Sdk.Models;

namespace Bifrost.Public.Sdk.Communication.Messages
{
    [DataContract]
    public class Message { }

    [DataContract]
    public class HelloMessage: Message
    {
        [DataMember]
        public Node Node { get; set; }
    }

    [DataContract]
    public class LinkStateRequestMessage : Message
    {
        [DataMember]
        public long SerialNumber { get; set; }
    }

    [DataContract]
    public class LinkStateUpdateMessage : Message
    {
        [DataMember]
        public bool Initial { get; set; }

        [DataMember]
        public long SerialNumber { get; set; }

        [DataMember]
        public List<Node> Neighbors { get; set; }
    }
}