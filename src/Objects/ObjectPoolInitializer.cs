#region

#endregion

using Appalachia.Editing.Attributes;

namespace Appalachia.Pooling.Objects
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
