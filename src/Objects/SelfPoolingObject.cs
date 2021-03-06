#region

using System;
using Unity.Profiling;

#endregion

namespace Appalachia.Pooling.Objects
{
    public abstract class SelfPoolingObject
    {
        public abstract void Reset();
        public abstract void Return();
        public abstract void Initialize();
    }

    public abstract class SelfPoolingObject<T> : SelfPoolingObject
        where T : SelfPoolingObject<T>, new()
    {
        private static ObjectPool<T> _internalPool;
        private static bool _initializing;

        private static readonly ProfilerMarker _PRF_SelfPoolingObject_Get =
            new("SelfPoolingObject.Get");

        private static readonly ProfilerMarker _PRF_SelfPoolingObject_Get_CreatePool =
            new("SelfPoolingObject.Get.CreatePool");

        private static readonly ProfilerMarker _PRF_SelfPoolingObject_Return =
            new("SelfPoolingObject.Return");

        private static readonly ProfilerMarker _PRF_SelfPoolingObject_ExecuteReset =
            new("SelfPoolingObject.ExecuteReset");

        private static readonly ProfilerMarker _PRF_SelfPoolingObject_ExecuteInitialize =
            new("SelfPoolingObject.ExecuteInitialize");

        [Obsolete]
        protected SelfPoolingObject()
        {
            if (!_initializing)
            {
                throw new NotSupportedException("Do not call constructor directly");
            }
        }

        public static T Get()
        {
            using (_PRF_SelfPoolingObject_Get.Auto())
            {
                _initializing = true;

                if (_internalPool == null)
                {
                    using (_PRF_SelfPoolingObject_Get_CreatePool.Auto())
                    {
                        _internalPool = ObjectPoolProvider.Create<T>(
                            ExecuteReset,
                            ExecuteInitialize
                        );
                    }
                }

                var result = _internalPool.Get();

                _initializing = false;
                return result;
            }
        }

        private static void ExecuteReset(T obj)
        {
            using (_PRF_SelfPoolingObject_ExecuteReset.Auto())
            {
                obj.Reset();
            }
        }

        private static void ExecuteInitialize(T obj)
        {
            using (_PRF_SelfPoolingObject_ExecuteInitialize.Auto())
            {
                obj.Initialize();
            }
        }

        public override void Return()
        {
            using (_PRF_SelfPoolingObject_Return.Auto())
            {
                _internalPool.Return((T) this);
            }
        }
    }
}
