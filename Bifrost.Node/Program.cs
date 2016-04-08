using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bifrost.Public.Sdk;

namespace Bifrost.Node
{
    public class Program
    {
        public void Main(string[] args)
        {
            Log.Configure(new FileInfo("log4net.xml"), new DirectoryInfo("C:\\Temp\\bifrost"));
            NodeAsync node1 = new NodeAsync();
            NodeAsync node2 = new NodeAsync();
            NodeAsync node3 = new NodeAsync();
            NodeAsync node4 = new NodeAsync();


            NodeAsync node5 = new NodeAsync();
            NodeAsync node6 = new NodeAsync();
            NodeAsync node7 = new NodeAsync();
            NodeAsync node8 = new NodeAsync();

            node8.StartAsync("127.0.0.1", 128).Wait();
            node7.StartAsync("127.0.0.1", 127).Wait();
            node6.StartAsync("127.0.0.1", 126).Wait();
            node5.StartAsync("127.0.0.1", 125).Wait();

            node4.StartAsync("127.0.0.1", 124).Wait();
            node3.StartAsync("127.0.0.1", 123).Wait();
            node2.StartAsync("127.0.0.1", 122).Wait();
            node1.StartAsync("127.0.0.1", 121).Wait();

            node3.AddNode(node4.Self);
            node2.AddNode(node3.Self);
            node1.AddNode(node2.Self);
            node4.AddNode(node1.Self);

            node7.AddNode(node8.Self);
            node6.AddNode(node7.Self);
            node5.AddNode(node6.Self);
            node8.AddNode(node5.Self);

            Thread.Sleep(60000);

            node1.AddNode(node8.Self);
            node8.AddNode(node1.Self);

            Console.WriteLine("Press Enter to exit");
            Console.ReadLine();
        }
    }
}
