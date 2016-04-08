using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Bifrost.Public.Sdk
{
    public class SocketListenerAsync
    {
        public event Action<Socket> ConnectionAccepted;

        private readonly Socket _listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        public SocketListenerAsync(IPEndPoint listeningEndPoint)
        {
            _listeningSocket.Bind(listeningEndPoint);
            _listeningSocket.Listen(10);
        }

        public async Task StartAsync()
        {
            await StartAcceptAsync(null);
        }

        public async Task StopAsync()
        {
            _listeningSocket.Close(1000);
            await Task.FromResult(0);
        }

        protected virtual void OnConnectionAccepted(Socket obj)
        {
            ConnectionAccepted?.Invoke(obj);
        }

        private async Task StartAcceptAsync(SocketAsyncEventArgs acceptEventArgs)
        {
            if (acceptEventArgs == null)
            {
                acceptEventArgs = new SocketAsyncEventArgs();
                acceptEventArgs.Completed += AcceptCompleted;
            }
            else
            {
                acceptEventArgs.AcceptSocket = null;
            }

            if (!_listeningSocket.AcceptAsync(acceptEventArgs))
                AcceptCompleted(null, acceptEventArgs);

            await Task.FromResult(0);
        }

        private void AcceptCompleted(object sender, SocketAsyncEventArgs acceptAsyncEventArgs)
        {
            if (acceptAsyncEventArgs.SocketError == SocketError.Success)
            {
                var socket = acceptAsyncEventArgs.AcceptSocket;
                OnConnectionAccepted(socket);
            }
            StartAcceptAsync(acceptAsyncEventArgs).Wait();
        }
    }
}