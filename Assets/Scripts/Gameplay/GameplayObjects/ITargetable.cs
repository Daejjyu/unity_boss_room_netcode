using System;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
  public interface ITargetable
  {
    bool IsNpc { get; }
    bool IsValidTarget { get; }
  }
}