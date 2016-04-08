using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Bifrost.Public.Sdk.Communication.Messages;
using log4net;

namespace Bifrost.Public.Sdk.Communication
{
    public class Protocol
    {
        public bool IsConnected
        {
            get
            {
                bool part1 = _socket.Poll(1000, SelectMode.SelectRead);
                bool part2 = (_socket.Available == 0);
                return !(part1 && part2);
            }
        }

        private readonly ILog _logger = Log.GetLogger(typeof(Protocol));
        private readonly DataContractSerializer _serializer;
        private readonly Socket _socket;

        public Protocol(Socket socket)
        {
            _logger.Debug("Initializing protocol");
            _socket = socket;
            List<Type> contracts = GetType()
                .Assembly.GetTypes()
                .Where(x => x.GetCustomAttributes(typeof(DataContractAttribute), true).Any())
                .ToList();

            _logger.DebugFormat("Discovered {0} contracts", contracts.Count);

            _serializer = new DataContractSerializer(typeof(Message), contracts);
        }

        public Message Receive()
        {
            var buffer = new byte[sizeof(int)];
            _socket.Receive(buffer);
            int length = BitConverter.ToInt32(buffer, 0);
            return ReceiveContent(length);
        }

        public async Task<Message> ReceiveAsync()
        {
            return await Task.Factory.StartNew<Message>(Receive);
        }

        public void Send(Message message)
        {
            using (var ms = new MemoryStream())
            {
                _serializer.WriteObject(ms, message);
                byte[] buffer = ms.ToArray();
                _socket.Send(BitConverter.GetBytes(buffer.Length));
                _socket.Send(buffer);
            }
        }

        public async Task SendAsync(Message message)
        {
            await Task.Factory.StartNew(() => Send(message));
        }

        public void Disconnect()
        {
            if (_socket != null)
            {
                _socket.Close(1);
                _socket.Dispose();
            }
        }

        protected Message ReceiveContent(int length)
        {
            using (var memoryStream = new MemoryStream())
            {
                int totalReceived = 0;
                while (totalReceived != length)
                {
                    var buffer = new byte[Math.Min(1024 * 10, length - totalReceived)];
                    int received = _socket.Receive(buffer);
                    totalReceived += received;
                    memoryStream.Write(buffer, 0, received);
                }

                memoryStream.Seek(0, SeekOrigin.Begin);
                return _serializer.ReadObject(memoryStream) as Message;
            }
        }
    }
}
