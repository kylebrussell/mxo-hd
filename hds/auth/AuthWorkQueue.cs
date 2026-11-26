using System;
using System.Collections.Generic;
using System.Threading;

namespace hds.auth
{
    /// <summary>
    /// Bounded FIFO work queue backed by dedicated background threads so auth work (PBKDF2, RSA, DB) never blocks
    /// NetCoreServer's accept/receive callbacks or starves the .NET ThreadPool under login bursts.
    /// </summary>
    public sealed class AuthWorkQueue : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Queue<Action> _queue = new Queue<Action>();
        private readonly Thread[] _workers;
        private readonly int _maxQueueDepth;
        private bool _disposed;

        public int WorkerCount => _workers.Length;
        public int MaxQueueDepth => _maxQueueDepth;

        public int QueuedCount
        {
            get
            {
                lock (_gate)
                {
                    return _queue.Count;
                }
            }
        }

        public AuthWorkQueue(int workerCount, int maxQueueDepth, string namePrefix)
        {
            if (workerCount < 1) throw new ArgumentOutOfRangeException(nameof(workerCount));
            if (maxQueueDepth < 1) throw new ArgumentOutOfRangeException(nameof(maxQueueDepth));

            _maxQueueDepth = maxQueueDepth;
            _workers = new Thread[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                Thread worker = new Thread(RunWorker)
                {
                    IsBackground = true,
                    Name = (namePrefix ?? "AuthWorker") + "-" + i
                };
                _workers[i] = worker;
                worker.Start();
            }
        }

        /// <summary>
        /// Attempts to enqueue work without blocking. Returns false when the queue is at capacity or shut down.
        /// </summary>
        public bool TryEnqueue(Action work)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));

            lock (_gate)
            {
                if (_disposed || _queue.Count >= _maxQueueDepth)
                    return false;

                _queue.Enqueue(work);
                Monitor.Pulse(_gate);
                return true;
            }
        }

        private void RunWorker()
        {
            while (true)
            {
                Action work;
                lock (_gate)
                {
                    while (_queue.Count == 0 && !_disposed)
                    {
                        Monitor.Wait(_gate);
                    }

                    if (_disposed)
                        return;

                    work = _queue.Dequeue();
                }

                try
                {
                    work();
                }
                catch (Exception ex)
                {
                    Output.WriteLine("AuthWorkQueue worker caught unhandled exception: " + ex.Message);
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _queue.Clear();
                Monitor.PulseAll(_gate);
            }
        }
    }
}
