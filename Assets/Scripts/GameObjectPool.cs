using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrostboundFrontier
{
    /// <summary>Small reusable pool for runtime-created world visuals.</summary>
    public sealed class GameObjectPool
    {
        private readonly Stack<GameObject> available = new Stack<GameObject>();
        private readonly HashSet<GameObject> pooled = new HashSet<GameObject>();
        private readonly Func<GameObject> factory;
        private readonly Transform inactiveRoot;

        public int AvailableCount => available.Count;

        public GameObjectPool(Func<GameObject> factory, Transform inactiveRoot)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.inactiveRoot = inactiveRoot;
        }

        public GameObject Get(Transform parent)
        {
            GameObject item = available.Count > 0 ? available.Pop() : factory();
            pooled.Remove(item);
            item.transform.SetParent(parent, false);
            item.SetActive(true);
            return item;
        }

        public void Release(GameObject item)
        {
            if (item == null || pooled.Contains(item)) return;
            item.SetActive(false);
            item.transform.SetParent(inactiveRoot, false);
            available.Push(item);
            pooled.Add(item);
        }
    }
}
