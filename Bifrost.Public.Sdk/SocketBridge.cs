using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Bifrost.Public.Sdk
{
    public class SocketBridge
    {
        public Socket Source { get; private set; }
        public Socket Destination { get; private set; }

        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly Task _bridgeThread;

        public SocketBridge(Socket source, Socket destination, CancellationTokenSource cancellationTokenSource = null)
        {
            if (!source.Connected || !destination.Connected)
                throw new ApplicationException("Source and Destination Sockets need to be Connected");
            
            Source = source;
            Destination = destination;
            
            if (cancellationTokenSource == null)
                cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;

            _bridgeThread = new Task(RunThread, _cancellationTokenSource.Token);
        }

        public void Start()
        {
            if(_bridgeThread.Status != TaskStatus.Running)
                _bridgeThread.Start();
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
        }

        private void RunThread()
        {
            var receiveTask = Task.Factory.StartNew(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    byte[] buffer = new byte[1024];
                    int receivedBytes = 0;
                    while ((receivedBytes = await Destination.ReceiveAsync(buffer, 0, buffer.Length, SocketFlags.None)) > 0)
                    {
                        await Source.SendAsync(buffer, 0, receivedBytes, SocketFlags.None);
                    }
                }
            }, _cancellationTokenSource.Token);

            var sendTask = Task.Factory.StartNew(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    byte[] buffer = new byte[1024];
                    int receivedBytes = 0;
                    while ((receivedBytes = await Source.ReceiveAsync(buffer, 0, buffer.Length, SocketFlags.None)) > 0)
                    {
                        await Destination.SendAsync(buffer, 0, receivedBytes, SocketFlags.None);
                    }
                }
            }, _cancellationTokenSource.Token);

            Task.WaitAll(new Task[] {  receiveTask, sendTask }, _cancellationTokenSource.Token);
        }
    }
}