using System;
using System.Net;
using System.Net.Sockets;
using Bifrost.Public.Sdk.Communication.Messages;
using log4net;
using Action = Bifrost.Public.Sdk.Communication.Messages.Action;

namespace Bifrost.Public.Sdk.Communication
{
    public class ClientSession
    {
        private readonly ILog _logger = Log.GetLogger(typeof(ClientSession));
        private readonly Protocol _protocol;
        private readonly IPAddress _remoteAddress;
        private readonly NodeAsync _nodeAsync;

        public ClientSession(Socket socket, NodeAsync nodeAsync)
        {
            _remoteAddress = ((IPEndPoint)socket.RemoteEndPoint).Address;
            _protocol = new Protocol(socket);
            _nodeAsync = nodeAsync;
        }

        public void SessionHandler()
        {
            try
            {
                _logger.DebugFormat("New session from {0}", _remoteAddress);
                while (_protocol.IsConnected)
                {
                    Message message = _protocol.Receive();

                    switch (message.Action)
                    {
                        case Action.Handshake:
                            _protocol.Send(new Message() { Action = Action.Handshake, Payload = _nodeAsync.Self.Id });
                            break;
                        case Action.GetNodes:
                            _protocol.Send(new Message() { Action = Action.Response, Payload = _nodeAsync.Peers });
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
            catch (Exception e)
            {
                _logger.ErrorFormat(e.ToString());
                throw;
            }
            finally
            {
            }
        }
        
    }
}