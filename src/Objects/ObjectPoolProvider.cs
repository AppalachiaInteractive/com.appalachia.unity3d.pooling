#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

#endregion

namespace Appalachia.Core.Pooling.Objects
{
    public static class ObjectPoolProvider
    {
        public static ObjectPool<T> Create<T>()
            where T : class, new()
        {
            return new ObjectPool<T>(() => new T());
        }

        public static ObjectPool<T> Create<T>(Action<T> customReset)
            where T : class, new()
        {
            return new ObjectPool<T>(() => new T(), customReset);
        }

        public static ObjectPool<T> Create<T>(Action<T> customReset, Action<T> customPreGet)
            where T : class, new()
        {
            return new ObjectPool<T>(() => new T(), customReset, customPreGet);
        }

        public static ObjectPool<T> Create<T>(Func<T> customAdd)
            where T : class
        {
            return new ObjectPool<T>(customAdd);
        }

        public static ObjectPool<T> Create<T>(Func<T> customAdd, Action<T> customReset)
            where T : class
        {
            return new ObjectPool<T>(customAdd, customReset);
        }

        public static ObjectPool<T> Create<T>(Func<T> customAdd, Action<T> customReset, Action<T> customPreGet)
            where T : class
        {
            return new ObjectPool<T>(customAdd, customReset, customPreGet);
        }

        public static ObjectPool<GameObject> CreateGameObjectPool(HideFlags hideFlags = HideFlags.DontSave)
        {
            var resetAction = new Action<GameObject>(
                obj =>
                {
                    obj.name = string.Empty;
                    obj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    obj.transform.localScale = Vector3.one;
                    obj.hideFlags = HideFlags.HideAndDontSave;
                    obj.SetActive(false);
                }
            );

            var preGetAction = new Action<GameObject>(
                obj =>
                {
                    obj.hideFlags = hideFlags;
                    obj.SetActive(true);
                }
            );

            return new ObjectPool<GameObject>(() => new GameObject(), resetAction, preGetAction);
        }

        public static ObjectPool<GameObject> CreatePrefabPool(
            GameObject prefab,
            HideFlags hideFlagsReset = HideFlags.HideAndDontSave,
            HideFlags hideFlagsAdd = HideFlags.DontSave)
        {
            return CreatePrefabPool(() => Object.Instantiate(prefab), hideFlagsReset, hideFlagsAdd);
        }

        public static ObjectPool<GameObject> CreatePrefabPool(
            GameObject[] prefabs,
            HideFlags hideFlagsReset = HideFlags.HideAndDontSave,
            HideFlags hideFlagsAdd = HideFlags.DontSave)
        {
            return CreatePrefabPool(() => Object.Instantiate(prefabs[Random.Range(0, prefabs.Length - 1)]), hideFlagsReset, hideFlagsAdd);
        }

        public static ObjectPool<GameObject> CreatePrefabPool(
            IList<GameObject> prefabs,
            HideFlags hideFlagsReset = HideFlags.HideAndDontSave,
            HideFlags hideFlagsAdd = HideFlags.DontSave)
        {
            return CreatePrefabPool(() => Object.Instantiate(prefabs[Random.Range(0, prefabs.Count - 1)]), hideFlagsReset, hideFlagsAdd);
        }

        public static ObjectPool<GameObject> CreatePrefabPool(
            Func<GameObject> selector,
            HideFlags hideFlagsReset = HideFlags.HideAndDontSave,
            HideFlags hideFlagsAdd = HideFlags.DontSave)
        {
            var resetAction = new Action<GameObject>(
                obj =>
                {
                    obj.hideFlags = hideFlagsReset;
                    obj.SetActive(false);
                }
            );

            var addAction = new Func<GameObject>(
                () =>
                {
                    var obj = selector();
                    obj.hideFlags = hideFlagsAdd;
                    return obj;
                }
            );

            var preGetAction = new Action<GameObject>(
                obj =>
                {
                    obj.hideFlags = hideFlagsAdd;
                    obj.SetActive(true);
                }
            );

            return new ObjectPool<GameObject>(addAction, resetAction, preGetAction);
        }

        public static LeakTrackingObjectPool<T> CreateLeakTrackingPool<T>()
            where T : class, new()
        {
            return new LeakTrackingObjectPool<T>(Create<T>());
        }
    }
}
