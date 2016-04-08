using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace Bifrost.Public.Sdk
{
    public class RelayManagerAsync
    {
        private readonly List<SocketBridge> _socketBridges = new List<SocketBridge>();
        private readonly CancellationTokenSource _cancellationTokenSource;

        public RelayManagerAsync(CancellationTokenSource cancellationTokenSource = null)
        {
            if (cancellationTokenSource == null)
                cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;
        }
        
        public SocketBridge BridgeSockets(Socket source, Socket destination)
        {
            return new SocketBridge(source, destination, _cancellationTokenSource);
        }
    }
}