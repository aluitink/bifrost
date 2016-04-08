using System.Runtime.Serialization;

namespace Bifrost.Public.Sdk.Communication.Messages
{
    [DataContract]
    public enum Action : byte
    {
        [EnumMember]
        Response,
        [EnumMember]
        Handshake,
        [EnumMember]
        GetNodes
    }
}