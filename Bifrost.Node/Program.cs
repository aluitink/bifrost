using System;
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
            NodeAsync node1 = new NodeAsync();

            NodeAsync node2 = new NodeAsync();

            

            NodeAsync node3 = new NodeAsync();
            

            NodeAsync node4 = new NodeAsync();
            

            node4.StartAsync("127.0.0.1", 124).Wait();
            
            node3.AddNode(node4.Self);

            node3.StartAsync("127.0.0.1", 123).Wait();

            
            node2.AddNode(node3.Self);

            node2.StartAsync("127.0.0.1", 122).Wait();

            node1.AddNode(node2.Self);

            node1.StartAsync("127.0.0.1", 121).Wait();
        }
    }
}
