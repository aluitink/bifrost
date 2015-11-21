using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Bifrost.Public.Sdk;
using Bifrost.Public.Sdk.Protocols;
using MsgPack;

namespace Bifrost.Node
{
    public class Program
    {
        public void Main(string[] args)
        {
            using (Server<MessageHandler, Message> s = new Server<MessageHandler, Message>())
            {
                ObjectPacker op = new ObjectPacker();

                s.DataReceived += message =>
                {
                    var m = op.Unpack<Message>((byte[])message.Payload);
                };

                s.Start(new IPEndPoint(IPAddress.Loopback, 100));

                Thread.Sleep(2000);

                TcpClient client = new TcpClient("localhost", 100);

                using (var stream = client.GetStream())
                {
                    Random r = new Random();

                    Message m = new Message();
                    m.Payload = Guid.NewGuid();
                    

                    byte[] data = op.Pack(m);

                    var n = op.Unpack<Message>(data);

                    byte[] header = BitConverter.GetBytes(data.Length);

                    stream.Write(header, 0, header.Length);
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }

                Console.ReadLine();
            }
        }
    }

    
}
