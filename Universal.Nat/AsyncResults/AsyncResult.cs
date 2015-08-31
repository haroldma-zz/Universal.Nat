using System;
using System.Threading;

namespace Torrent.Uwp.Nat.AsyncResults
{
    internal class AsyncResult : IAsyncResult
    {
        private readonly AsyncCallback _callback;

        public AsyncResult(AsyncCallback callback, object asyncState)
        {
            _callback = callback;
            AsyncState = asyncState;
            AsyncWaitHandle = new ManualResetEvent(false);
        }

        public ManualResetEvent AsyncWaitHandle { get; }

        public Exception StoredException { get; private set; }

        public object AsyncState { get; }

        WaitHandle IAsyncResult.AsyncWaitHandle => AsyncWaitHandle;

        public bool CompletedSynchronously { get; protected internal set; }

        public bool IsCompleted { get; protected internal set; }

        public void Complete()
        {
            Complete(StoredException);
        }

        public void Complete(Exception ex)
        {
            StoredException = ex;
            IsCompleted = true;
            AsyncWaitHandle.Set();

            _callback?.Invoke(this);
        }
    }
}