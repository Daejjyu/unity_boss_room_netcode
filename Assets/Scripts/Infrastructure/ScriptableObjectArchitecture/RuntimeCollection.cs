using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.BossRoom.Infrastructure
{
    /// <summary>
    /// ScriptableObject class that contains a list of a given type. The instance of this ScriptableObject can be
    /// referenced by components, without a hard reference between systems.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// /// <summary>
    /// ScriptableObject 클래스이며, 특정 유형의 리스트를 포함합니다. 
    /// 이 ScriptableObject의 인스턴스는 시스템 간의 강한 의존성 없이 컴포넌트에서 참조될 수 있습니다.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class RuntimeCollection<T> : ScriptableObject
    {
        public List<T> Items = new List<T>();

        public event Action<T> ItemAdded;

        public event Action<T> ItemRemoved;

        public void Add(T item)
        {
            if (!Items.Contains(item))
            {
                Items.Add(item);
                ItemAdded?.Invoke(item);
            }
        }

        public void Remove(T item)
        {
            if (Items.Contains(item))
            {
                Items.Remove(item);
                ItemRemoved?.Invoke(item);
            }
        }
    }
}
