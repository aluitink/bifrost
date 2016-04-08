using System.Collections.Generic;
using System.Runtime.Serialization;
using Bifrost.Public.Sdk.Models;

namespace Bifrost.Public.Sdk.Communication.Messages
{
    [DataContract]
    [KnownType(typeof(Node))]
    [KnownType(typeof(List<Node>))]
    public class Message
    {
        [DataMember]
        public Action Action { get; set; }
        [DataMember]
        public object Payload { get; set; }
    }
}