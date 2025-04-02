using System;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
  public struct ActionRequestData : INetworkSerializable
  {
    public ActionID ActionID;
    public Vector3 Position;
    public Vector3 Direction;
    public ulong[] TargetIds;
    public float Amount;
    public bool ShouldQueue;
    public bool ShouldClose;
    public bool CancelMovement;

    [Flags]
    private enum PackFlags
    {
      None = 0,
      HasPosition = 1,
      HasDirection = 1 << 1,
      HasTargetIds = 1 << 2,
      HasAmount = 1 << 3,
      ShouldQueue = 1 << 4,
      ShouldClose = 1 << 5,
      CancelMovement = 1 << 6,
    }

    public static ActionRequestData Create(Action action) =>
        new()
        {
          ActionID = action.ActionID
        };

    public bool Compare(ref ActionRequestData rhs)
    {
      bool scalarParamsEqual = (ActionID, Position, Direction, Amount) == (rhs.ActionID, rhs.Position, rhs.Direction, rhs.Amount);
      if (!scalarParamsEqual) { return false; }

      if (TargetIds == rhs.TargetIds) { return true; } //covers case of both being null.
      if (TargetIds == null || rhs.TargetIds == null || TargetIds.Length != rhs.TargetIds.Length) { return false; }
      for (int i = 0; i < TargetIds.Length; i++)
      {
        if (TargetIds[i] != rhs.TargetIds[i]) { return false; }
      }

      return true;
    }


    private PackFlags GetPackFlags()
    {
      PackFlags flags = PackFlags.None;
      if (Position != Vector3.zero) { flags |= PackFlags.HasPosition; }
      if (Direction != Vector3.zero) { flags |= PackFlags.HasDirection; }
      if (TargetIds != null) { flags |= PackFlags.HasTargetIds; }
      if (Amount != 0) { flags |= PackFlags.HasAmount; }
      if (ShouldQueue) { flags |= PackFlags.ShouldQueue; }
      if (ShouldClose) { flags |= PackFlags.ShouldClose; }
      if (CancelMovement) { flags |= PackFlags.CancelMovement; }

      return flags;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
      PackFlags flags = PackFlags.None;
      if (!serializer.IsReader)
      {
        flags = GetPackFlags();
      }

      serializer.SerializeValue(ref ActionID);
      serializer.SerializeValue(ref flags);

      if (serializer.IsReader)
      {
        ShouldQueue = (flags & PackFlags.ShouldQueue) != 0;
        CancelMovement = (flags & PackFlags.CancelMovement) != 0;
        ShouldClose = (flags & PackFlags.ShouldClose) != 0;
      }

      if ((flags & PackFlags.HasPosition) != 0)
      {
        serializer.SerializeValue(ref Position);
      }
      if ((flags & PackFlags.HasDirection) != 0)
      {
        serializer.SerializeValue(ref Direction);
      }
      if ((flags & PackFlags.HasTargetIds) != 0)
      {
        serializer.SerializeValue(ref TargetIds);
      }
      if ((flags & PackFlags.HasAmount) != 0)
      {
        serializer.SerializeValue(ref Amount);
      }
    }
  }
}
