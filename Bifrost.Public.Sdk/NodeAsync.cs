using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Bifrost.Public.Sdk.Communication;
using Bifrost.Public.Sdk.Communication.Messages;
using Bifrost.Public.Sdk.Models;
using log4net;
using Action = Bifrost.Public.Sdk.Communication.Messages.Action;

namespace Bifrost.Public.Sdk
{
    public class NodeAsync: IDisposable
    {
        public List<Node> Peers => _establishedPeers != null ? _establishedPeers.Keys.ToList() : new List<Node>();

        public Node Self { get { return _self; } }

        protected ILog Logger = Log.GetLogger(typeof (NodeAsync));

        private readonly BlockingCollection<Node> _newNodes = new BlockingCollection<Node>(); 
        private readonly ConcurrentDictionary<Node, Client> _establishedPeers = new ConcurrentDictionary<Node, Client>();

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private readonly Server _server;
        private readonly Node _self;

        public NodeAsync()
        {
            _server = new Server(this);
            _self = new Node();
        }

        public async Task StartAsync(string ipAddress, int port)
        {
            Logger.Info("Node starting...");
            _server.Start(ipAddress, port);
            _self.Id = Guid.NewGuid();
            _self.Endpoint = ipAddress;
            _self.Port = port;

            InitializeNode();
            await Task.FromResult(0);
        }

        public async Task StopAsync()
        {
            Logger.Info("Node Stopping...");
            _newNodes.CompleteAdding();
            _cancellationTokenSource.Cancel();
            _server.Stop();
            await Task.FromResult(0);
        }

        public void AddNode(Node node)
        {
            //Add node if we are not already established or trying to establish
            lock (_newNodes)
            {
                if (_establishedPeers.Keys.All(n => n.Id != node.Id) && _newNodes.All(n => n.Id != node.Id) && _self.Id != node.Id)
                    _newNodes.Add(node);
            }
        }

        public void RemoveNode(Node node)
        {
            Client client;
            if (_establishedPeers.TryRemove(node, out client))
            {
                client.Disconnect();
            }
        }
        
        public void Dispose()
        {
            if (_establishedPeers != null)
            {
                foreach (var client in _establishedPeers.Values)
                {
                    client.Disconnect();
                }
            }
        }

        protected void InitializeNode()
        {
            Task.Factory.StartNew(ConnectWithNewNodes);
            Task.Factory.StartNew(SyncWithPeers);
        }
        
        protected void ConnectWithNewNodes()
        {
            while (!_cancellationTokenSource.IsCancellationRequested && !_newNodes.IsCompleted)
            {
                Node newNode = _newNodes.Take();

                Logger.InfoFormat("New Node: {0}", newNode);

                var client = new Client(newNode.Endpoint, newNode.Port);

                try
                {
                    client.Connect();
                    client.Send(new Message() { Action = Action.Handshake, Payload = _self });
                    var message = client.Receive();

                    if (message.Action != Action.Handshake)
                        throw new ApplicationException("Invalid response.");

                    var clientNode = message.Payload as Node;

                    if (clientNode == null)
                        throw new ApplicationException("Unexpected Client Response");

                    if (newNode.Id == Guid.Empty)
                        newNode.Id = clientNode.Id;
                    else if (!newNode.Id.Equals(clientNode.Id))
                        throw new ApplicationException("Received Invalid Server ID");

                    if (!_establishedPeers.TryAdd(newNode, client))
                        throw new ApplicationException("Could not add client.");

                    Logger.InfoFormat("Peer Established: {0}", newNode);
                }
                catch (Exception e)
                {
                    Logger.Error("Failed to establish connection with node.", e);
                    client.Disconnect();
                }
                
            }
        }

        protected void SyncWithPeers()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    foreach (KeyValuePair<Node, Client> establishedPeer in _establishedPeers)
                    {
                        var node = establishedPeer.Key;

                        Logger.InfoFormat("Syncing with peer: {0}", node);

                        var client = establishedPeer.Value;

                        client.Send(new Message() { Action = Action.GetNodes });

                        var queryResponse = client.Receive();

                        if (queryResponse.Action != Action.Response)
                            throw new ApplicationException("Unexpected Response.");

                        var nodes = queryResponse.Payload as List<Node>;

                        if (nodes == null)
                            continue;

                        foreach (Node newNode in nodes)
                        {
                            AddNode(newNode);
                        }

                    }
                }
                catch (Exception e)
                {
                    Logger.Error("Sync failure.", e);
                }
                finally
                {
                    Task.Delay(TimeSpan.FromSeconds(10)).Wait();
                }
            }
        }
    }
}