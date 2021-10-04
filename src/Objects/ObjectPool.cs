using System;
using Appalachia.Core.Collections;
using Appalachia.Core.Collections.NonSerialized;
using Unity.Profiling;

namespace Appalachia.Pooling.Objects
{
    public class ObjectPool<T> : IDisposable
        where T : class
    {
        private static readonly ProfilerMarker _PRF_ObjectPool_ObjectPool = new ProfilerMarker("ObjectPool.ObjectPool");
        private static readonly ProfilerMarker _PRF_ObjectPool_Dispose = new ProfilerMarker("ObjectPool.Dispose");
        private static readonly ProfilerMarker _PRF_ObjectPool_Get = new ProfilerMarker("ObjectPool.Get");
        private static readonly ProfilerMarker _PRF_ObjectPool_Return = new ProfilerMarker("ObjectPool.Return");

        private static readonly ProfilerMarker _PRF_ObjectPool_Get_DisposalCheck = new ProfilerMarker("ObjectPool.Get.DisposalCheck");
        private static readonly ProfilerMarker _PRF_ObjectPool_Get_ListCheck = new ProfilerMarker("ObjectPool.Get.ListCheck");
        private static readonly ProfilerMarker _PRF_ObjectPool_Get_ListCheck_Add = new ProfilerMarker("ObjectPool.Get.ListCheck.Add");
        private static readonly ProfilerMarker _PRF_ObjectPool_Get_ListCheck_GetLast = new ProfilerMarker("ObjectPool.Get.ListCheck.GetLast");
        private static readonly ProfilerMarker _PRF_ObjectPool_Get_ListCheck_RemoveLast = new ProfilerMarker("ObjectPool.Get.ListCheck.RemoveLast");
        private static readonly ProfilerMarker _PRF_ObjectPool_Get_CustomPreGet = new ProfilerMarker("ObjectPool.Get.CustomPreGet");

        private static readonly ProfilerMarker _PRF_ObjectPool_Return_SelfPoolReset = new ProfilerMarker("ObjectPool.Return.SelfPoolReset");
        private static readonly ProfilerMarker _PRF_ObjectPool_Return_CustomReset = new ProfilerMarker("ObjectPool.Return.CustomReset");
        private static readonly ProfilerMarker _PRF_ObjectPool_Return_OnReset = new ProfilerMarker("ObjectPool.Return.CustomReset");

        protected volatile bool _isDisposed;

        private readonly Action<T> _customReset;
        private readonly Func<T> _customAdd;
        private readonly Action<T> _customPreGet;

        private protected readonly AppaList<T> _list;

        private readonly bool _selfPooling;

        public ObjectPool(Func<T> customAdd) : this(customAdd, null)
        {
        }

        public ObjectPool(Func<T> customAdd, Action<T> customReset) : this(customAdd, customReset, null)
        {
        }

        public ObjectPool(Func<T> customAdd, Action<T> customReset, Action<T> customPreGet)
        {
            using (_PRF_ObjectPool_ObjectPool.Auto())
            {
                _selfPooling = typeof(SelfPoolingObject).IsAssignableFrom(typeof(T));

                _list = new NonSerializedList<T>(32, noTracking: true);
                _customReset = customReset;
                _customAdd = customAdd;
                _customPreGet = customPreGet;

                OnInitialize();
            }
        }

        public bool IsDisposed => _isDisposed;

        public void Dispose()
        {
            using (_PRF_ObjectPool_Dispose.Auto())
            {
                _isDisposed = true;

                OnDispose();
            }
        }

        protected virtual void OnInitialize()
        {
        }

        protected virtual void OnDispose()
        {
        }

        public T Get()
        {
            using (_PRF_ObjectPool_Get.Auto())
            {
                using (_PRF_ObjectPool_Get_DisposalCheck.Auto())
                {
                    if (_isDisposed)
                    {
                        throw new ObjectDisposedException(GetType().Name);
                    }
                }

                T item;

                using (_PRF_ObjectPool_Get_ListCheck.Auto())
                {
                    if (_list.Count == 0)
                    {
                        using (_PRF_ObjectPool_Get_ListCheck_Add.Auto())
                        {
                            item = _customAdd();
                        }
                    }
                    else
                    {
                        using (_PRF_ObjectPool_Get_ListCheck_GetLast.Auto())
                        {
                            item = _list[_list.Count - 1];
                        }

                        using (_PRF_ObjectPool_Get_ListCheck_RemoveLast.Auto())
                        {
                            _list.RemoveAt(_list.Count - 1);
                        }
                    }
                }

                using (_PRF_ObjectPool_Get_CustomPreGet.Auto())
                {
                    _customPreGet?.Invoke(item);
                }

                return item;
            }
        }

        public void Return(T obj)
        {
            using (_PRF_ObjectPool_Return.Auto())
            {
                using (_PRF_ObjectPool_Return_SelfPoolReset.Auto())
                {
                    if (_selfPooling)
                    {
                        (obj as SelfPoolingObject)?.Reset();
                    }
                }

                using (_PRF_ObjectPool_Return_CustomReset.Auto())
                {
                    _customReset?.Invoke(obj);
                }

                using (_PRF_ObjectPool_Return_OnReset.Auto())
                {
                    OnReset(obj);
                }
            }
        }

        protected virtual void OnReset(T obj)
        {
            _list.Add(obj);
        }
    }
}
