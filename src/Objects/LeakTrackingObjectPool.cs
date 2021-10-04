#region

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#endregion

namespace Appalachia.Pooling.Objects
{
    public class LeakTrackingObjectPool<T>
        where T : class, new()
    {
        private readonly ConditionalWeakTable<T, Tracker> _trackers = new ConditionalWeakTable<T, Tracker>();
        private readonly ObjectPool<T> _inner;

        public LeakTrackingObjectPool(ObjectPool<T> inner)
        {
            if (inner == null)
            {
                throw new ArgumentNullException(nameof(inner));
            }

            _inner = inner;
        }

        public T Get()
        {
            var value = _inner.Get();
            _trackers.Add(value, new Tracker());
            return value;
        }

        public void Return(T obj)
        {
            Tracker tracker;
            if (_trackers.TryGetValue(obj, out tracker))
            {
                _trackers.Remove(obj);
                tracker.Dispose();
            }

            _inner.Return(obj);
        }

        private class Tracker : IDisposable
        {
            private readonly string _stack;
            private bool _disposed;

            public Tracker()
            {
                _stack = Environment.StackTrace;
            }

            public void Dispose()
            {
                _disposed = true;
                GC.SuppressFinalize(this);
            }

            ~Tracker()
            {
                if (!_disposed && !Environment.HasShutdownStarted)
                {
                    Debug.Fail($"{typeof(T).Name} was leaked. Created at: {Environment.NewLine}{_stack}");
                }
            }
        }
    }
}
