using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Bifrost.Public.Sdk.Interfaces;

namespace Bifrost.Public.Sdk.Protocols
{
    public class MessageHandler : ProtocolHandler
    {
        public long TotalBytesReceived { get; set; }
        public long TotalBytesSent { get; set; }

        private int _nextFrameHeaderIndex = 0;
        private readonly byte[] _nextFrameHeaderBuffer = new byte[4];

        private int _nextMessageIndex = 0;
        private byte[] _nextMessageBuffer;
        private byte[] _remainderBuffer;

        private readonly BlockingCollection<ArraySegment<byte>> _receiveBuffer = new BlockingCollection<ArraySegment<byte>>();
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        public MessageHandler(IServer server, Socket socket, CancellationTokenSource cancellationTokenSource = null)
            : base(server, socket)
        {
            if(cancellationTokenSource == null)
                cancellationTokenSource = new CancellationTokenSource();

            _cancellationTokenSource = cancellationTokenSource;

            Task.Factory.StartNew(() =>
            {
                while (!_receiveBuffer.IsCompleted && !_cancellationTokenSource.IsCancellationRequested)
                {
                    var segment = _remainderBuffer == null ? 
                    _receiveBuffer.Take(_cancellationTokenSource.Token) : 
                    new ArraySegment<byte>(_remainderBuffer);

                    //Next message size is unknown, expect new frame.
                    if (_nextMessageBuffer == null)
                    {
                        if (_nextFrameHeaderIndex == _nextFrameHeaderBuffer.Length || segment.Count >= 4) //Do we have a header or a segment that is the size of or bigger than a header?
                        {
                            if (_nextFrameHeaderIndex < _nextFrameHeaderBuffer.Length)
                            {
                                Buffer.BlockCopy(segment.Array, 0, _nextFrameHeaderBuffer, 0, _nextFrameHeaderBuffer.Length);
                                _nextFrameHeaderIndex += _nextFrameHeaderBuffer.Length;

                                if (_nextFrameHeaderIndex == _nextFrameHeaderBuffer.Length)
                                {
                                    var messageSize = BitConverter.ToInt32(_nextFrameHeaderBuffer, 0);
                                    _nextMessageBuffer = new byte[messageSize];//@@@ Unsafe.. someone could send a header with a large message size

                                    //Reset frame header 
                                    _nextFrameHeaderIndex = 0;
                                }
                            }
                            
                            if (segment.Count > 4)
                            {
                                var remainingUnread = segment.Count - 4;
                                
                                Buffer.BlockCopy(segment.Array, 4, _nextMessageBuffer, _nextMessageIndex, remainingUnread);
                                _nextMessageIndex += remainingUnread;
                            }
                        }
                        else
                        {
                            Buffer.BlockCopy(segment.Array, 0, _nextFrameHeaderBuffer, _nextFrameHeaderIndex, segment.Count);
                            _nextFrameHeaderIndex += segment.Count;
                        }
                    }
                    else
                    {
                        var bytesToCopy = Math.Min(_nextMessageBuffer.Length - _nextMessageIndex, segment.Count);
                        Buffer.BlockCopy(segment.Array, 0, _nextMessageBuffer, _nextMessageIndex, bytesToCopy);
                        _nextMessageIndex += segment.Count;

                        if (bytesToCopy < segment.Count)
                        {
                            var bytesRemaining = segment.Count - bytesToCopy;
                            _remainderBuffer = new byte[bytesRemaining];
                            Buffer.BlockCopy(segment.Array, bytesToCopy, _remainderBuffer, 0, _remainderBuffer.Length);
                        }
                    }

                    //If _nextMessageIndex == _nextMessageBuffer // Fire Event
                    if (_nextMessageBuffer != null)
                    {
                        if (_nextMessageIndex == _nextMessageBuffer.Length)
                        {
                            Message m = new Message();
                            m.Payload = _nextMessageBuffer;
                            OnDataReceived(m);
                        }
                    }
                }
            }, _cancellationTokenSource.Token);
        }

        public override void OnDataRecevied(SocketAsyncEventArgs eventArgs)
        {
            var count = eventArgs.BytesTransferred;
            if (count > 0)
            {
                TotalBytesReceived += count;

                byte[] buffer = new byte[count];
                Buffer.BlockCopy(eventArgs.Buffer, eventArgs.Offset, buffer, 0, count);
                
                _receiveBuffer.Add(new ArraySegment<byte>(buffer, 0, buffer.Length));

                if (!Socket.ReceiveAsync(eventArgs))
                    Server.ProcessReceive(eventArgs);
            }
        }

        public override void OnDataSending(SocketAsyncEventArgs eventArgs)
        {
        }

        public override void Dispose()
        {
            _cancellationTokenSource.Cancel();
            base.Dispose();
        }
    }

    public class ProtocolHandler: IDisposable
    {
        protected readonly IServer Server;
        protected readonly Socket Socket;

        protected ProtocolHandler(IServer server, Socket socket, CancellationTokenSource cancellationTokenSource = null)
        {
            Server = server;
            Socket = socket;
        }

        public virtual void OnDataRecevied(SocketAsyncEventArgs eventArgs) { }

        public virtual void OnDataSending(SocketAsyncEventArgs eventArgs) { }

        internal void HandleReceived(SocketAsyncEventArgs receiveAsyncEventArgs)
        {
            OnDataRecevied(receiveAsyncEventArgs);
        }

        internal void HandleSend(SocketAsyncEventArgs sendAsyncEventArgs)
        {
            OnDataSending(sendAsyncEventArgs);
        }

        internal void Close()
        {
            // close the socket associated with the client
            try
            {
                Socket.Shutdown(SocketShutdown.Send);
            }
            catch (Exception) { }
            finally
            {
                Socket.Close();
            }
        }

        public virtual void Dispose() { }
    }
}
