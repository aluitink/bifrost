using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting.Contexts;
using System.Runtime.Remoting.Messaging;
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
    public class TcpListenerEx : TcpListener
    {
        public bool Active { get { return base.Active; } }

        public TcpListenerEx(IPEndPoint localEP) : base(localEP) { }

        public TcpListenerEx(IPAddress localaddr, int port) : base(localaddr, port) { }

        public TcpListenerEx(int port) : base(port) { }
    }

    public abstract class StateBase
    {
        protected ILog Logger = Log.GetLogger(typeof (StateBase));
        public abstract void Handle(Peer peer);

    }

    public class DownState : StateBase
    {
        public override void Handle(Peer peer)
        {
            if (peer.Initiatior)
            {
                peer.State = new AttemptState();
                peer.State.Handle(peer);
                return;
            }

            HelloMessage response = peer.Session.Receive<HelloMessage>();
            peer.Node = response.Node;

            peer.State = new InitState();
            peer.State.Handle(peer);
        }
    }
    
    public class InitState : StateBase
    {
        public override void Handle(Peer peer)
        {
            peer.Session.Send(new HelloMessage() { Node = peer.Self.Self });
            peer.State = new TwoWayState();
            peer.State.Handle(peer);
        }
    }

    public class AttemptState : StateBase
    {
        public override void Handle(Peer peer)
        {
            peer.Session.Send(new HelloMessage() { Node = peer.Self.Self });
            HelloMessage response = peer.Session.Receive<HelloMessage>();
            peer.Node = response.Node;

            peer.State = new TwoWayState();
            peer.State.Handle(peer);
        }
    }

    public class TwoWayState : StateBase
    {
        public override void Handle(Peer peer)
        {
            if (peer.Initiatior)
            {
                peer.State = new ExStartState();
                peer.State.Handle(peer);
            }
            else
            {
                peer.State = new ExchangeState();
                peer.State.Handle(peer);
            }
        }
    }

    public class ExStartState : StateBase
    {
        public override void Handle(Peer peer)
        {
            //Send Peers
            peer.Session.Send(new LinkStateUpdateMessage() { Initial = true, Neighbors = peer.Self.EstablishedPeers.Values.Select(p => p.Node).ToList(), SerialNumber = 1});
            
            if (peer.Initiatior)
            {
                peer.State = new ExchangeState();
                peer.State.Handle(peer);
            }
            else
            {
                peer.State = new LoadingState();
                peer.State.Handle(peer);
            }
        }
    }

    public class ExchangeState : StateBase
    {
        public override void Handle(Peer peer)
        {
            var linkStateUpdateMessage = peer.Session.Receive<LinkStateUpdateMessage>();

            if (linkStateUpdateMessage.Initial)
            {
                peer.SerialNumber = linkStateUpdateMessage.SerialNumber;
            }

            if (linkStateUpdateMessage.SerialNumber > peer.SerialNumber)
            {
                try
                {
                    foreach (Node neighbor in linkStateUpdateMessage.Neighbors)
                    {
                        peer.Neighbors.Enqueue(neighbor);
                    }
                }
                finally
                {
                    peer.SerialNumber = linkStateUpdateMessage.SerialNumber;
                }
            }

            if (peer.Initiatior)
            {
                peer.State = new LoadingState();
                peer.State.Handle(peer);
            }
            else
            {
                peer.State = new ExStartState();
                peer.State.Handle(peer);
            }
        }
    }

    public class LoadingState : StateBase
    {
        public override void Handle(Peer peer)
        {
            //Add neig
            while (!peer.Neighbors.IsEmpty)
            {
                Node newNeighbor;
                if(peer.Neighbors.TryDequeue(out newNeighbor))
                    peer.Self.AddNode(newNeighbor);
            }

            peer.State = new FullState();
            peer.State.Handle(peer);
        }
    }

    public class FullState : StateBase
    {
        public override void Handle(Peer peer)
        {
            while (peer.Session.IsConnected)
            {
                
            }
            Console.WriteLine("Peer: {0} <-> {1}", peer.Self.Self.Id, peer.Node.Id);
        }
    }

    public class AdvertiseState : StateBase
    {
        public override void Handle(Peer peer)
        {
            throw new NotImplementedException();
        }
    }

    public class Peer
    {
        public bool Initiatior { get; set; }
        
        public NodeAsync Self { get; set; }

        public Node Node { get; set; }
        public Session Session { get; private set; }
        public StateBase State { get; set; }
        
        public ConcurrentQueue<Node> Neighbors { get; set; } 

        public long SerialNumber { get; set; }

        public Peer(NodeAsync self, Session session, bool initiator = false)
        {
            Neighbors = new ConcurrentQueue<Node>();

            Self = self;

            Session = session;
            Initiatior = initiator;
            SerialNumber = 0;
            State = new DownState();
        }

        public void Initialize()
        {
            State.Handle(this);
        }

        public override string ToString()
        {
            return string.Format("Node: {0}, State: {1}, SerialNumber: {2}", Node, State, SerialNumber);
        }
    }

    public class NodeAsync: IDisposable
    {
        //public List<Node> Peers => EstablishedPeers != null ? EstablishedPeers.Keys.ToList() : new List<Node>();

        public Node Self { get { return _self; } }

        protected ILog Logger = Log.GetLogger(typeof (NodeAsync));
        
        internal readonly BlockingCollection<Node> NewNodes = new BlockingCollection<Node>();
        internal readonly BlockingCollection<Peer> PendingPeers = new BlockingCollection<Peer>();
        internal readonly ConcurrentDictionary<Guid, Peer> EstablishedPeers = new ConcurrentDictionary<Guid, Peer>();
        
        private readonly CancellationTokenSource _listeningTaskCancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationTokenSource _discoveryCancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationTokenSource _replicationCancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationTokenSource _peerStateManagerCancellationTokenSource = new CancellationTokenSource();

        internal Task DiscoveryTask;
        internal Task ListenTask;
        internal Task ReplicationTask;
        internal Task PeerStateManagerTask;

        private TcpListenerEx _tcpListener;
        

        private readonly Node _self;

        public NodeAsync()
        {
            _self = new Node();
        }
        
        public async Task StartAsync(string ipAddress, int port)
        {
            _self.Id = Guid.NewGuid();
            _self.Endpoint = ipAddress;
            _self.Port = port;

            _tcpListener = new TcpListenerEx(IPAddress.Parse(ipAddress), port);

            StartListening();
            StartDiscovery();
            StartPeerStateManager();
            //StartReplication();

            await Task.FromResult(0);
        }

        public async Task StopAsync()
        {
            Logger.Info("Node Stopping...");
            NewNodes.CompleteAdding();
            
            await Task.FromResult(0);
        }

        public void AddNode(Node node)
        {
            //Add node if we are not already established or trying to establish
            if (!EstablishedPeers.ContainsKey(node.Id) &&
                PendingPeers.All(p => p.Node.Id != node.Id) &&
                NewNodes.All(n => n.Id != node.Id))
            {
                NewNodes.Add(node);
            }
        }

        public void RemoveNode(Node node)
        {
            Peer peer;
            if (EstablishedPeers.TryRemove(node.Id, out peer))
            {
                peer.Session.Disconnect();
            }
        }
        
        public void Dispose()
        {
            StopAsync().Wait();
            if (EstablishedPeers != null)
            {
                foreach (var client in EstablishedPeers.Values)
                {
                    client.Session.Disconnect();
                }
            }
        }

        internal void StartListening()
        {
            if (!_tcpListener.Active)
            {
                _tcpListener.Start();

                ListenTask = Task.Factory.StartNew(() =>
                {
                    Logger.Info("Listening for new connections...");
                    while (!_listeningTaskCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        TcpClient client = _tcpListener.AcceptTcpClient();
                        _listeningTaskCancellationTokenSource.Token.ThrowIfCancellationRequested();
                        PendingPeers.Add(new Peer(this, new Session(client.Client)));
                    }
                }, _listeningTaskCancellationTokenSource.Token);
            }
        }

        internal void StopListening()
        {
            _listeningTaskCancellationTokenSource.Cancel();
            _tcpListener.Stop();
            ListenTask = null;
        }

        internal void StartDiscovery()
        {
            if (DiscoveryTask != null)
                return;
            DiscoveryTask = Task.Factory.StartNew(() =>
            {
                while (!_discoveryCancellationTokenSource.Token.IsCancellationRequested && !NewNodes.IsCompleted)
                {
                    Node newNode = NewNodes.Take(_discoveryCancellationTokenSource.Token);
                    _discoveryCancellationTokenSource.Token.ThrowIfCancellationRequested();

                    Logger.InfoFormat("New Node: {0}", newNode);
                    TcpClient client = new TcpClient();
                    client.Connect(newNode.Endpoint, newNode.Port);
                    PendingPeers.Add(new Peer(this, new Session(client.Client), true));

                    //try
                    //{
                    //    client.Connect();
                    //    client.Send(new Message() {Action = Action.Handshake, Payload = Self.Id});
                    //    var message = client.Receive();

                    //    if (message.Action != Action.Handshake)
                    //        throw new ApplicationException("Invalid response.");

                    //    var serverId = (Guid) message.Payload;

                    //    if (newNode.Id == Guid.Empty)
                    //        newNode.Id = serverId;
                    //    else if (!newNode.Id.Equals(serverId))
                    //        throw new ApplicationException("Received Invalid Server ID");

                    //    if (!EstablishedPeers.TryAdd(newNode, client))
                    //        throw new ApplicationException("Could not add client.");

                    //    Logger.InfoFormat("Peer Established: {0}", newNode);
                    //}
                    //catch (Exception e)
                    //{
                    //    Logger.Error("Failed to establish connection with node.", e);
                    //    client.Disconnect();
                    //}
                }
            }, _discoveryCancellationTokenSource.Token);
            
        }

        internal void StopDiscovery()
        {
            _discoveryCancellationTokenSource.Cancel();
            DiscoveryTask = null;
        }

        internal void StartReplication()
        {
            if (ReplicationTask != null)
                return;

            ReplicationTask = Task.Factory.StartNew(() =>
            {
                //while (!_replicationCancellationTokenSource.IsCancellationRequested)
                //{
                //    try
                //    {
                //        foreach (KeyValuePair<Node, Client> establishedPeer in EstablishedPeers)
                //        {
                //            var n = establishedPeer.Key;

                //            Logger.InfoFormat("Syncing with peer: {0}", n);

                //            var client = establishedPeer.Value;

                //            client.Send(new Message() { Action = Action.GetNodes });

                //            var queryResponse = client.Receive();

                //            if (queryResponse.Action != Action.Response)
                //                throw new ApplicationException("Unexpected Response.");

                //            var nodes = queryResponse.Payload as List<Node>;

                //            if (nodes == null)
                //                continue;

                //            foreach (Node newNode in nodes)
                //            {
                //                AddNode(newNode);
                //            }

                //        }
                //    }
                //    catch (Exception e)
                //    {
                //        Logger.Error("Sync failure.", e);
                //    }
                //    finally
                //    {
                //        Task.Delay(TimeSpan.FromSeconds(10), _replicationCancellationTokenSource.Token).Wait();
                //    }
                //}

            }, _replicationCancellationTokenSource.Token);

            
        }

        internal void StopReplication()
        {
            _replicationCancellationTokenSource.Cancel();
            ReplicationTask = null;
        }

        internal void StartPeerStateManager()
        {
            if (PeerStateManagerTask != null)
                return;

            PeerStateManagerTask = Task.Factory.StartNew(() =>
            {
                while (!_peerStateManagerCancellationTokenSource.Token.IsCancellationRequested && !PendingPeers.IsCompleted)
                {
                    var pendingPeer = PendingPeers.Take(_peerStateManagerCancellationTokenSource.Token);
                    _peerStateManagerCancellationTokenSource.Token.ThrowIfCancellationRequested();

                    Task.Factory.StartNew(() =>
                    {
                        pendingPeer.Initialize();
                        EstablishedPeers.GetOrAdd(pendingPeer.Node.Id, pendingPeer);
                    }, _peerStateManagerCancellationTokenSource.Token);
                }
            }, _peerStateManagerCancellationTokenSource.Token);
        }
    }
}