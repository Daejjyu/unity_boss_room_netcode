using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.BossRoom.Infrastructure
{
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