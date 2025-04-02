using System;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// Comprehensive class that contains information needed to play back any action on the server. This is what gets sent client->server when
    /// the Action gets played, and also what gets sent server->client to broadcast the action event. Note that the OUTCOMES of the action effect
    /// don't ride along with this object when it is broadcast to clients; that information is sync'd separately, usually by NetworkVariables.
    /// </summary>
    /// <summary>
    /// 서버에서 액션을 재생하는 데 필요한 정보를 포함하는 포괄적인 클래스입니다. 이는 액션이 실행될 때 클라이언트->서버로 전송되며,
    /// 또한 서버->클라이언트로 액션 이벤트를 브로드캐스트하는 데 사용됩니다. 액션 효과의 결과는 클라이언트로 브로드캐스트될 때 
    /// 이 객체와 함께 전송되지 않습니다. 해당 정보는 일반적으로 NetworkVariables를 통해 별도로 동기화됩니다.
    /// </summary>
    public struct ActionRequestData : INetworkSerializable
    {
        // index of the action in the list of all actions in the game - a way to recover the reference to the instance at runtime
        /// 게임 내 모든 액션 목록에서 액션의 인덱스 - 런타임에서 
        // 인스턴스를 참조할 수 있는 방법입니다.
        public ActionID ActionID;
        // center position of skill, e.g. "ground zero" of a fireball skill.
        /// 기술의 중심 위치, 예를 들어 화염구 기술의 "원점"입니다.
        public Vector3 Position;
        // direction of skill, if not inferrable from the character's current facing.
        /// 기술의 방향, 캐릭터의 현재 방향에서 추론할 수 없는 경우에 해당합니다.
        public Vector3 Direction;
        // NetworkObjectIds of targets, or null if untargeted.
        /// 타겟의 NetworkObjectId 목록, 타겟이 없으면 null입니다.
        public ulong[] TargetIds;
        // can mean different things depending on the Action. For a ChaseAction, it will be target range the ChaseAction is trying to achieve.
        /// Action에 따라 다른 의미를 가질 수 있습니다. 
        // ChaseAction의 경우, ChaseAction이 달성하려는 타겟 범위입니다.
        public float Amount;
        // if true, this action should queue. If false, it should clear all current actions and play immediately.
        /// true인 경우, 이 액션은 큐에 들어가야 합니다. 
        // false인 경우, 현재의 모든 액션을 취소하고 즉시 실행됩니다.
        public bool ShouldQueue;
        // if true, the server should synthesize a ChaseAction to close to within range of the target before playing the Action. Ignored for untargeted actions.
        /// true인 경우, 서버는 액션을 실행하기 전에 타겟과의 범위 내로 
        // 들어가기 위해 ChaseAction을 합성해야 합니다. 
        // 타겟이 없는 액션에서는 무시됩니다.
        public bool ShouldClose;
        // if true, movement is cancelled before playing this action
        /// true인 경우, 이 액션을 실행하기 전에 이동이 취소됩니다.
        public bool CancelMovement;

        //O__O Hey, are you adding something? 
        // Be sure to update ActionLogicInfo, as well as the methods below.
        //O__O 혹시 추가하고 있나요? 
        // ActionLogicInfo와 아래 메서드들을 업데이트하는 것을 잊지 마세요.

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
            //currently serialized with a byte. Change Read/Write if you add more than 8 fields.
            //현재는 바이트로 직렬화됩니다. 
            // 8개 이상의 필드를 추가하면 Read/Write를 변경하세요.
        }

        public static ActionRequestData Create(Action action) =>
            new()
            {
                ActionID = action.ActionID
            };

        /// <summary>
        /// Returns true if the ActionRequestDatas are "functionally equivalent" (not including their Queueing or Closing properties).
        /// </summary>
        /// <summary>
        /// ActionRequestData들이 "기능적으로 동등한지" 확인합니다
        ///  (Queueing이나 Closing 속성은 포함되지 않음).
        /// </summary>
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
