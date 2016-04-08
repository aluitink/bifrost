using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Bifrost.Public.Sdk.Communication.Messages;

namespace Bifrost.Public.Sdk.Communication
{
    public class Client
    {
        private ServerSession _serverSession;

        private readonly TcpClient _tcpClient = new TcpClient();
        private readonly IPAddress _ipAddress;
        private readonly int _port;
        
        public Client(string ipAddress, int port)
        {
            _ipAddress = IPAddress.Parse(ipAddress);
            _port = port;
        }

        public void Connect()
        {
            _tcpClient.Connect(_ipAddress, _port);
            _serverSession = new ServerSession(_tcpClient.Client);
        }

        public void Disconnect()
        {
            _tcpClient.Close();
        }

        public void Send(Message message)
        {
            _serverSession.Send(message);
        }

        public Message Receive()
        {
            return _serverSession.Receive();
        }
    }
}
