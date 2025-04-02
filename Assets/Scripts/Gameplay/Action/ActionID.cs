using System;
using Unity.Netcode;

namespace Unity.BossRoom.Gameplay.Actions
{

  public struct ActionID : INetworkSerializeByMemcpy, IEquatable<ActionID>
  {
    public int ID;

    public bool Equals(ActionID other)
    {
      return ID == other.ID;
    }

    public override bool Equals(object obj)
    {
      return obj is ActionID other && Equals(other);
    }

    public override int GetHashCode()
    {
      return ID;
    }

    public static bool operator ==(ActionID x, ActionID y)
    {
      return x.Equals(y);
    }

    public static bool operator !=(ActionID x, ActionID y)
    {
      return !(x == y);
    }

    public override string ToString()
    {
      return $"ActionID({ID})";
    }
  }
}