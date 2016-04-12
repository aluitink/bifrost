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
    public class Session
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

        private readonly ILog _logger = Log.GetLogger(typeof(Session));
        private readonly DataContractSerializer _serializer;
        private readonly Socket _socket;

        public Session(Socket socket)
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

        public T Receive<T>() where T : class
        {
            var buffer = new byte[sizeof(int)];
            _socket.Receive(buffer);
            int length = BitConverter.ToInt32(buffer, 0);
            return ReceiveContent<T>(length);
        }

        public async Task<T> ReceiveAsync<T>() where T : class
        {
            return await Task.Factory.StartNew<T>(Receive<T>);
        }

        public void Send<T>(T message) where T : class
        {
            using (var ms = new MemoryStream())
            {
                _serializer.WriteObject(ms, message);
                byte[] buffer = ms.ToArray();
                _socket.Send(BitConverter.GetBytes(buffer.Length));
                _socket.Send(buffer);
            }
        }

        public async Task SendAsync<T>(T message) where T: class
        {
            await Task.Factory.StartNew(() => Send<T>(message));
        }

        public void Disconnect()
        {
            if (_socket != null)
            {
                _socket.Close(1);
                _socket.Dispose();
            }
        }

        protected T ReceiveContent<T>(int length) where T: class
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
                return _serializer.ReadObject(memoryStream) as T;
            }
        }
    }
}
