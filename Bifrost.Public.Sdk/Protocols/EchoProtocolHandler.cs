using System;
using System.Net.Sockets;
using Bifrost.Public.Sdk.Interfaces;

namespace Bifrost.Public.Sdk.Protocols
{
    public class EchoProtocolHandler : ProtocolHandler<byte[]>
    {
        public EchoProtocolHandler(IHandler handler, Socket socket)
            : base(handler, socket)
        { }

        public override void OnDataRecevied(SocketAsyncEventArgs eventArgs)
        {
            //echo the data received back to the client
            eventArgs.SetBuffer(eventArgs.Offset, eventArgs.BytesTransferred);
            if (!Socket.SendAsync(eventArgs))
                Handler.ProcessSend(eventArgs);
        }

        public override void OnDataSending(SocketAsyncEventArgs eventArgs)
        {
            // read the next block of data send from the client
            if (!Socket.ReceiveAsync(eventArgs))
                Handler.ProcessReceive(eventArgs);
        }
    }
}