using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Bifrost.Public.Sdk;

namespace Bifrost.Node
{
    public class Program
    {
        public void Main(string[] args)
        {
            using (Server s = new Server())
            {
                s.AcceptComplete += eventArgs =>
                {
                    eventArgs.UserToken = "Test user object";
                };

                s.DataReceived += (eventArgs, sendCallback) =>
                {
                    Console.WriteLine("Received...");
                    eventArgs.SetBuffer(0, eventArgs.BytesTransferred);
                    Console.WriteLine("Echoing...");
                    sendCallback(eventArgs);
                };


                s.DataSent += (eventArgs, receiveCallback) =>
                {
                    Console.WriteLine("Sending...");
                };

                s.Start(new IPEndPoint(IPAddress.Loopback, 100));

                Thread.Sleep(2000);

                TcpClient client = new TcpClient("localhost", 100);

                using (var stream = client.GetStream())
                {
                    byte[] a = Encoding.ASCII.GetBytes("look at this");

                    stream.Write(a, 0, a.Length);
                    byte[] receive = new byte[1024];
                    var read = stream.Read(receive, 0, receive.Length);

                    byte[] b = new byte[read];
                    Buffer.BlockCopy(receive, 0, b, 0, read);

                    if(a.SequenceEqual(b))
                        Console.WriteLine("ECHO RECEIVED");

                }

                    Console.ReadLine();
            }
        }
    }

    
}
