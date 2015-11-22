using System;
using System.Diagnostics.SymbolStore;
using System.Net;
using System.Net.Sockets;


namespace Bifrost.Public.Sdk
{
    public class Server : IDisposable
    {
        public event Action<SocketAsyncEventArgs> AcceptComplete;
        public event Action<SocketAsyncEventArgs, Action<SocketAsyncEventArgs>> DataReceived;
        public event Action<SocketAsyncEventArgs, Action<SocketAsyncEventArgs>> DataSent;

        
        private readonly Socket _listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        public void Start(IPEndPoint listeningAddress)
        {
            _listeningSocket.Bind(listeningAddress);
            _listeningSocket.Listen(4);
            StartAccept(null);
        }

        private void StartAccept(SocketAsyncEventArgs acceptEventArgs)
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
            {   //operation completed synchronously
                AcceptCompleted(null, acceptEventArgs);
            }
        }
        private void AcceptCompleted(object sender, SocketAsyncEventArgs acceptAsyncEventArgs)
        {
            if (acceptAsyncEventArgs.SocketError == SocketError.Success)
            {
                var socket = acceptAsyncEventArgs.AcceptSocket;
                
                SocketAsyncEventArgs receiveAsyncEventArgs = new SocketAsyncEventArgs();
                receiveAsyncEventArgs.AcceptSocket = socket;
                receiveAsyncEventArgs.Completed += IOCompleted;

                receiveAsyncEventArgs.SetBuffer(new byte[1024], 0, 1024);

                OnAcceptComplete(receiveAsyncEventArgs);//allows subscriber to set UserToken before call to ReceiveAsync

                Receive(receiveAsyncEventArgs);
            }
            StartAccept(acceptAsyncEventArgs);
        }

        private void ProcessReceive(SocketAsyncEventArgs receiveAsyncEventArgs)
        {
            OnDataReceived(receiveAsyncEventArgs);
        }

        private void ProcessSend(SocketAsyncEventArgs sendAsyncEventArgs)
        {
            OnDataSent(sendAsyncEventArgs);
        }

        private void Receive(SocketAsyncEventArgs receiveAsyncEventArgs)
        {
            var socket = receiveAsyncEventArgs.AcceptSocket;
            if (!socket.ReceiveAsync(receiveAsyncEventArgs))
                ProcessReceive(receiveAsyncEventArgs);
        }

        private void Send(SocketAsyncEventArgs sendAsyncEventArgs)
        {
            var socket = sendAsyncEventArgs.AcceptSocket;
            if (!socket.SendAsync(sendAsyncEventArgs))
                ProcessSend(sendAsyncEventArgs);
        }

        private void IOCompleted(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.Receive:
                        ProcessReceive(e);
                        break;
                    case SocketAsyncOperation.Send:
                        ProcessSend(e);
                        break;
                    default:
                        throw new NotImplementedException("The code will handle only receive and send operations");
                }
            }
            catch (Exception)
            {
                CloseConnection(e);
            }

        }
        

        public void Dispose()
        {
            _listeningSocket?.Dispose();
        }

        private void CloseConnection(SocketAsyncEventArgs e)
        {

        }

        protected virtual void OnDataReceived(SocketAsyncEventArgs obj)
        {
            DataReceived?.Invoke(obj, Send);
        }

        protected virtual void OnDataSent(SocketAsyncEventArgs obj)
        {
            DataSent?.Invoke(obj, Receive);
        }

        protected virtual void OnAcceptComplete(SocketAsyncEventArgs obj)
        {
            AcceptComplete?.Invoke(obj);
        }
    }
}