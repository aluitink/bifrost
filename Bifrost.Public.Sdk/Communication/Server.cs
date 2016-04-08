using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace Bifrost.Public.Sdk.Communication
{
    public class Server : IDisposable
    {
        private readonly ILog _logger = Log.GetLogger(typeof(Server));
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private TcpListener _tcpListener;
        private Task _listenTask;

        private readonly NodeAsync _nodeAsync;

        public Server(NodeAsync nodeAsync)
        {
            _nodeAsync = nodeAsync;
        }

        public void Start(string ipAddress, int port)
        {
            _tcpListener = new TcpListener(IPAddress.Parse(ipAddress), port);
            _tcpListener.Start();
            _logger.InfoFormat("Server started {0}:{1}", ipAddress, port);
            _listenTask = Task.Factory.StartNew(() => StartListening(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            if (_tcpListener != null && _tcpListener.Server != null)
            {
                _tcpListener.Server.Close(0);
                _tcpListener = null;
            }
            if (_listenTask != null)
                Task.WaitAll(_listenTask);
        }

        public void Dispose()
        {
            Stop();
        }

        private void StartListening(CancellationToken cancellationToken)
        {
            _logger.Info("Listening for new connections...");
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client = _tcpListener.AcceptTcpClient();
                cancellationToken.ThrowIfCancellationRequested();
                var clientSession = new ClientSession(client.Client, _nodeAsync);
                Task.Factory.StartNew(clientSession.SessionHandler, cancellationToken);
            }
        }
    }
}
