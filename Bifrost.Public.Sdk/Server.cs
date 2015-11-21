using System;
using System.Net;
using System.Net.Sockets;
using Bifrost.Public.Sdk.Interfaces;
using Bifrost.Public.Sdk.Protocols;

namespace Bifrost.Public.Sdk
{
    public class Server<T> : IHandler, IDisposable
        where T : ProtocolHandler
    {
        private readonly Socket _listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly Func<IHandler, Socket, T> _createProtocolHandlerFunc; 
        public Server(Func<IHandler, Socket, T> createProtocolHandlerFunc)
        {
            _createProtocolHandlerFunc = createProtocolHandlerFunc;
        }

        public void Start(IPEndPoint listeningAddress)
        {
            _listeningSocket.Bind(listeningAddress);
            _listeningSocket.Listen(4);
            StartAccept(null);
        }

        public virtual void ProcessSend(SocketAsyncEventArgs e)
        {
            // check if the remote host closed the connection
            if (e.SocketError != SocketError.Success)
                throw new ApplicationException("Socket was in error.");

            T protocolHandler = e.UserToken as T;

            if (protocolHandler == null)
                throw new ApplicationException("Could not get protocol handler from UserToken");

            protocolHandler.HandleSend(e);
        }
        public virtual void ProcessReceive(SocketAsyncEventArgs e)
        {
            // check if the remote host closed the connection
            if (e.SocketError != SocketError.Success)
                throw new ApplicationException("Socket was in error.");

            T protocolHandler = e.UserToken as T;

            if (protocolHandler == null)
                throw new ApplicationException("Could not get protocol handler from UserToken");

            if (e.BytesTransferred > 0)
                protocolHandler.HandleReceived(e);
            else
                CloseConnection(e);
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

                T protocolHandler = _createProtocolHandlerFunc(this, socket);
                
                SocketAsyncEventArgs receiveAsyncEventArgs = new SocketAsyncEventArgs();
                receiveAsyncEventArgs.AcceptSocket = socket;
                receiveAsyncEventArgs.Completed += IOCompleted;
                receiveAsyncEventArgs.UserToken = protocolHandler;
                receiveAsyncEventArgs.SetBuffer(new byte[1024], 0, 1024);

                if (!socket.ReceiveAsync(receiveAsyncEventArgs))
                    ProcessReceive(receiveAsyncEventArgs);
            }
            StartAccept(acceptAsyncEventArgs);
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
        private void CloseConnection(SocketAsyncEventArgs e)
        {
            T userToken = e.UserToken as T;

            if (userToken == null)
                throw new ApplicationException("Could not get AsyncServerState");

            userToken.Close();
        }

        public void Dispose()
        {
            if (_listeningSocket != null)
                _listeningSocket.Dispose();
        }

        protected virtual void OnDataReceived(PD obj)
        {
            if (DataReceived != null)
                DataReceived(obj);
        }
    }
}