using System.Net.Sockets;
using System.Threading.Tasks;
using Bifrost.Public.Sdk.Communication.Messages;

namespace Bifrost.Public.Sdk.Communication
{
    public class ServerSession
    {
        private readonly Protocol _protocol;

        public ServerSession(Socket socket)
        {
            _protocol = new Protocol(socket);
        }

        public void Send(Message message)
        {
            _protocol.Send(message);
        }

        public Message Receive()
        {
            return _protocol.Receive();
        }

        public Task<Message> ReceiveAsync()
        {
            return _protocol.ReceiveAsync();
        }
    }
}