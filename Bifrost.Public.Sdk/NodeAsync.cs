using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bifrost.Public.Sdk.Interfaces;
using Bifrost.Public.Sdk.Models;
using Hik.Communication.Scs.Communication.EndPoints.Tcp;
using Hik.Communication.ScsServices.Client;
using Hik.Communication.ScsServices.Service;

namespace Bifrost.Public.Sdk
{
    public class ClientManagerAsync
    {
        private readonly ConcurrentQueue<IScsServiceClient<INodeService>> _newClients = new ConcurrentQueue<IScsServiceClient<INodeService>>();
        private readonly ConcurrentDictionary<Guid, IScsServiceClient<INodeService>> _establishedClients = new ConcurrentDictionary<Guid, IScsServiceClient<INodeService>>();

        public ClientManagerAsync()
        {
            
        }

        public async void AddNodeAsync(Node node)
        {
            string ipAddress;
            int port;
            if (!TryParseEndpoint(node.ControlAddress, out ipAddress, out port))
                throw new ApplicationException("Could not parse control address.");

            IScsServiceClient<INodeService> client = null;

            await Task.Factory.StartNew(() =>
            {
                client = ScsServiceClientBuilder.CreateClient<INodeService>(new ScsTcpEndPoint(ipAddress, port));
                client.Connect();
            });
            if (client == null)
                throw new ApplicationException("Could not connect to client.");

            _newClients.Enqueue(client);
        }


        private bool TryParseEndpoint(string endpoint, out string ipAddress, out int port)
        {
            ipAddress = null;
            port = 0;
            var parts = endpoint.Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return false;
            try
            {
                ipAddress = parts[0];
                port = Int32.Parse(parts[1]);
                return true;
            }
            catch (Exception)
            {
                ipAddress = null;
                port = 0;
                return false;
            }
        }
    }


    public class NodeAsync: ScsService, INodeService, IDisposable
    {
        public Node Self { get { return _self; } }
        private IScsServiceApplication _serviceApplication;

        private readonly List<Node> _availableNodes = new List<Node>();
        private Node _self;

        private readonly ConcurrentQueue<IScsServiceClient<INodeService>> _newClients = new ConcurrentQueue<IScsServiceClient<INodeService>>();
        private readonly ConcurrentDictionary<Guid, IScsServiceClient<INodeService>> _establishedClients = new ConcurrentDictionary<Guid, IScsServiceClient<INodeService>>();
        
        public async Task StartAsync(string ipAddress, int port)
        {
            _serviceApplication = ScsServiceBuilder.CreateService(new ScsTcpEndPoint(ipAddress, port));
            _serviceApplication.AddService<INodeService, NodeAsync>(this);
            _serviceApplication.ClientConnected += ServiceApplicationOnClientConnected;
            _serviceApplication.ClientDisconnected += ServiceApplicationOnClientDisconnected;
            _serviceApplication.Start();

            _self = new Node();
            _self.ControlAddress = string.Format("{0}:{1}", ipAddress, port);
            _self.Id = Guid.NewGuid();

            InitializeNode();
            await Task.FromResult(0);
        }

        public async Task StopAsync()
        {
            _serviceApplication.Stop();
            await Task.FromResult(0);
        }

        public void AddNode(Node node)
        {
            if(!_availableNodes.Exists(n => n.Id == node.Id))
                _availableNodes.Add(node);
        }

        public void RemoveNode(Node node)
        {
            _availableNodes.Remove(node);
        }

        public IEnumerable<Node> RetrieveNodes()
        {
            return _availableNodes;
        }

        public void SendNodes(IEnumerable<Node> nodes)
        {
            throw new NotImplementedException();
        }

        public bool RequestCircuit(Circuit circuit, string callbackAddress)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            if (_establishedClients != null)
            {
                foreach (var client in _establishedClients.Values)
                {
                    client.Disconnect();
                    client.Dispose();
                }
            }
        }

        protected void InitializeNode()
        {
            ConnectWithNewNodes();
            SyncWithConnectedClients();
        }

        protected void ConnectWithNewNodes()
        {
            //Select only nodes that are not already clients
            foreach (Node availableNode in _availableNodes.Where(n => !_establishedClients.ContainsKey(n.Id)))
            {
                string ipAddress;
                int port;
                if (!TryParseEndpoint(availableNode.ControlAddress, out ipAddress, out port))
                    continue;

                var client = CreateClient(ipAddress, port);

                try
                {
                    client.Connect();
                    _newClients.Enqueue(client);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        protected void SyncWithConnectedClients()
        {
            List<Guid> establishedClients = new List<Guid>();
            foreach (var clientData in _newClients)
            {
                var clientId = clientData.Key;
                var client = clientData.Value;

                var nodes = client.ServiceProxy.RetrieveNodes();
                if(nodes == null)
                    continue;

                foreach (Node node in nodes)
                {
                    AddNode(node);
                }
                establishedClients.Add(clientId);
                _establishedClients.GetOrAdd(clientId, guid => client);
                
            }
            ConnectWithNewNodes();
        }

        protected IScsServiceClient<INodeService> CreateClient(string ipAddress, int port)
        {
            return ScsServiceClientBuilder.CreateClient<INodeService>(new ScsTcpEndPoint(ipAddress, port));
        }

        private void ServiceApplicationOnClientDisconnected(object sender, ServiceClientEventArgs serviceClientEventArgs)
        {
            Console.WriteLine("Client Disconnected: {0}", serviceClientEventArgs.Client.ClientId);
        }

        private void ServiceApplicationOnClientConnected(object sender, ServiceClientEventArgs serviceClientEventArgs)
        {
            Console.WriteLine("Client Connected: {0}", serviceClientEventArgs.Client.ClientId);
        }

        private bool TryParseEndpoint(string endpoint, out string ipAddress, out int port)
        {
            ipAddress = null;
            port = 0;
            var parts = endpoint.Split(new [] { ":" } , StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return false;
            try
            {
                ipAddress = parts[0];
                port = Int32.Parse(parts[1]);
                return true;
            }
            catch (Exception)
            {
                ipAddress = null;
                port = 0;
                return false;
            }
        }


        
    }
}