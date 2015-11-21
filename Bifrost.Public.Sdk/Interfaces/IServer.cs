using System.Net.Sockets;

namespace Bifrost.Public.Sdk.Interfaces
{
    public interface IHandler
    {
        void ProcessSend(SocketAsyncEventArgs e);
        void ProcessReceive(SocketAsyncEventArgs e);
    }
}