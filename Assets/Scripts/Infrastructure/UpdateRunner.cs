using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.BossRoom.Infrastructure
{
  public class UpdateRunner : MonoBehaviour
  {
    class SubscriberData
    {
      public float Period;
      public float NextCallTime;
      public float LastCallTime;
    }

    readonly Queue<Action> m_PendingHandlers = new Queue<Action>();
    readonly HashSet<Action<float>> m_Subscribers = new HashSet<Action<float>>();
    readonly Dictionary<Action<float>, SubscriberData> m_SubscriberData = new Dictionary<Action<float>, SubscriberData>();

    public void OnDestroy()
    {
      m_PendingHandlers.Clear();
      m_Subscribers.Clear();
      m_SubscriberData.Clear();
    }

    public void Subscribe(Action<float> onUpdate, float updatePeriod)
    {
      if (onUpdate == null)
      {
        return;
      }

      bool isLocalFunction = onUpdate.Target == null;
      if (isLocalFunction)
      {
        Debug.LogError("Can't subscribe to a local function that can go out of scope and can't be unsubscribed from.");
        return;
      }

      bool isLambdaFunction = onUpdate.Method.ToString().Contains("<");
      if (isLambdaFunction)
      {
        Debug.LogError("Can't subscribe with an anonymous function that cannot be Unsubscribed, by checking for a character that can't exist in a declared method name.");
        return;
      }

      bool isNewAction = !m_Subscribers.Contains(onUpdate);
      if (isNewAction)
      {
        m_PendingHandlers.Enqueue(() =>
        {
          if (m_Subscribers.Add(onUpdate))
          {
            m_SubscriberData.Add(onUpdate, new SubscriberData() { Period = updatePeriod, NextCallTime = 0, LastCallTime = Time.time });
          }
        });
      }
    }

    void Update()
    {
      while (m_PendingHandlers.Count > 0)
      {
        m_PendingHandlers.Dequeue()?.Invoke();
      }

      foreach (var subscriber in m_Subscribers)
      {
        var subscriberData = m_SubscriberData[subscriber];

        if (Time.time >= subscriberData.NextCallTime)
        {
          subscriber.Invoke(Time.time - subscriberData.LastCallTime);
          subscriberData.LastCallTime = Time.time;
          subscriberData.NextCallTime = Time.time + subscriberData.Period;
        }
      }
    }
  }
}