#region

#endregion

using Appalachia.Core.Editing.Attributes;

namespace Appalachia.Core.Pooling.Objects
{
    [EditorOnlyInitializeOnLoad]
    public static class ObjectPoolInitializer
    {
        public static ObjectPool<T> Create<T>()
            where T : class, new()
        {
            return ObjectPoolProvider.Create<T>();
        }
    }
}
