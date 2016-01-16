using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Bifrost.Public.Sdk
{
    public static class SocketExtensions
    {
        public static Task<int> ReceiveAsync(this Socket socket, byte[] buffer, int offset, int count, SocketFlags socketFlags)
        {
            var tcs = new TaskCompletionSource<int>(socket);
            socket.BeginReceive(buffer, offset, count, socketFlags, iar =>
            {
                var t = (TaskCompletionSource<int>)iar.AsyncState;
                var s = (Socket)t.Task.AsyncState;
                try { t.TrySetResult(s.EndReceive(iar)); }
                catch (Exception exc) { t.TrySetException(exc); }
            }, tcs);
            return tcs.Task;
        }

        public static Task SendAsync(this Socket socket, byte[] buffer, int offset, int count, SocketFlags socketFlags)
        {
            var tcs = new TaskCompletionSource<int>(socket);
            socket.BeginSend(buffer, offset, count, socketFlags, iar =>
            {
                var t = (TaskCompletionSource<int>)iar.AsyncState;
                var s = (Socket)t.Task.AsyncState;
                try { t.TrySetResult(s.EndSend(iar)); }
                catch (Exception exc) { t.TrySetException(exc); }
            }, tcs);
            return tcs.Task;
        }
    }
}