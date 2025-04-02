using System;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
  public class NetworkHealthState : NetworkBehaviour
  {
    [HideInInspector]
    public NetworkVariable<int> HitPoints = new NetworkVariable<int>();

    public event Action HitPointsDepleted;
    public event Action HitPointsReplenished;

    void OnEnable()
    {
      HitPoints.OnValueChanged += HitPointsChanged;
    }

    void OnDisable()
    {

      HitPoints.OnValueChanged -= HitPointsChanged;
    }

    void HitPointsChanged(int previousValue, int newValue)
    {
      if (previousValue > 0 && newValue <= 0)
      {
        // newly reached 0 HP
        HitPointsDepleted?.Invoke();
      }
      else if (previousValue <= 0 && newValue > 0)
      {
        // newly revived
        HitPointsReplenished?.Invoke();
      }
    }
  }
}