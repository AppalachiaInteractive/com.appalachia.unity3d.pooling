#region

using System.Collections.Generic;

#endregion

namespace Appalachia.Core.Pooling.Objects
{
    public static class SelfPoolingObjectExtensions
    {
        public static void ResetElements<T>(this List<T> list)
            where T : SelfPoolingObject, new()
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    list[i].Reset();
                }
            }
        }

        public static void ReturnElements<T>(this List<T> list)
            where T : SelfPoolingObject, new()
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    list[i].Return();
                }
            }
        }

        public static void InitializeElements<T>(this List<T> list)
            where T : SelfPoolingObject, new()
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    list[i].Initialize();
                }
            }
        }
    }
}
