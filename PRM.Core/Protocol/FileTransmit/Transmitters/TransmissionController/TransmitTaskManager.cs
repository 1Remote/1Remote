using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace PRM.Core.Protocol.FileTransmit.Transmitters.TransmissionController
{
    public class TransmitTaskManager : NotifyPropertyChangedBase, IDisposable
    {
        private readonly int _threadCount;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly ITransmitter _orgTransmitter;
        private readonly List<ITransmitter> _trans = new List<ITransmitter>();
        private ObservableCollection<TransmitTask> _transmitTasks = new ObservableCollection<TransmitTask>();
        private readonly ConcurrentQueue<TransmitTask> _transmitTasksQueue = new ConcurrentQueue<TransmitTask>();
        private readonly ManualResetEvent _waitStartNextTransmit = new ManualResetEvent(true);
        private readonly ManualResetEvent _waitNewTransmitTask = new ManualResetEvent(true);
        private readonly object _addTaskLocker = new object();

        public TransmitTaskManager(int threadCount, ITransmitter orgTransmitter)
        {
            _orgTransmitter = orgTransmitter;
            if (threadCount > 0)
                _threadCount = threadCount;
            else
                _threadCount = 1;
        }


        public void Dispose()
        {
            Release();
        }

        public void Release()
        {
            _cancellationTokenSource.Cancel(false);
        }

        ObservableCollection<TransmitTask> TransmitTasks
        {
            get => _transmitTasks;
            set => SetAndNotifyIfChanged(nameof(TransmitTasks), ref _transmitTasks, value);
        }

        private void AddTransmitTask(TransmitTask t)
        {
            lock (_addTaskLocker)
            {
                if (!t.ItemsWaitForTransmit.Any())
                    return;
                TransmitTasks.Add(t);
                _transmitTasksQueue.Enqueue(t);
            }
        }

        private void MainLoop()
        {
            int tIndex = 0;
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                if (_trans.Count < _threadCount)
                {
                    _trans.Add(_orgTransmitter.Clone());
                    if (_trans.Count >= _threadCount)
                        _waitStartNextTransmit.Reset();
                }
                else
                {
                    _waitStartNextTransmit.WaitOne();
                    _waitStartNextTransmit.Reset();
                }


                ++tIndex;
                if (tIndex >= _trans.Count)
                    tIndex = 0;

                lock (_addTaskLocker)
                {
                    if (!_transmitTasksQueue.Any() && TransmitTasks.Any())
                    {
                        foreach (var task in TransmitTasks)
                        {
                            _transmitTasksQueue.Enqueue(task);
                        }
                    }

                    if (!_transmitTasksQueue.Any())
                    {
                        _waitNewTransmitTask.Reset();
                    }
                }
                _waitNewTransmitTask.WaitOne();

                lock (_addTaskLocker)
                {
                    if (_transmitTasksQueue.TryPeek(out _))
                    {

                    }
                }
            }
        }
    }
}
