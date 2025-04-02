using System;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// The "Target" Action is not a skill, but rather the result of a user left-clicking an enemy. This
    /// Action runs persistently, and automatically resets the NetworkCharacterState.Target property if the
    /// target becomes ineligible (dies or disappears). Note that while Actions in general can have multiple targets,
    /// you as a player can only have a single target selected at a time (the character that your target reticule appears under).
    /// </summary>
    /// <summary>
    /// "타겟" 액션은 스킬이 아니라 사용자가 적을 왼쪽 클릭했을 때 발생하는 결과입니다. 이
    /// 액션은 지속적으로 실행되며, 타겟이 유효하지 않게 되면(NetworkCharacterState.Target 속성을 자동으로 초기화함) 
    /// 타겟이 죽거나 사라지면 타겟을 초기화합니다. 액션은 일반적으로 여러 타겟을 가질 수 있지만,
    /// 플레이어는 한 번에 하나의 타겟만 선택할 수 있습니다(타겟 조준선이 나타나는 캐릭터).
    /// </summary>
    [CreateAssetMenu(menuName = "BossRoom/Actions/Target Action")]
    public partial class TargetAction : Action
    {
        public override bool OnStart(ServerCharacter serverCharacter)
        {
            //we must always clear the existing target, even if we don't run. This is how targets get cleared--running a TargetAction
            //with no target selected.
            // 기존 타겟을 항상 초기화해야 합니다. 실행하지 않더라도, 타겟이 선택되지 않은 상태에서 TargetAction을 실행하면 이렇게 타겟이 초기화됩니다.
            serverCharacter.TargetId.Value = 0;

            //there can only be one TargetAction at a time!
            // 한 번에 하나의 TargetAction만 실행될 수 있습니다!
            serverCharacter.ActionPlayer.CancelRunningActionsByLogic(ActionLogic.Target, true, this);

            if (Data.TargetIds == null || Data.TargetIds.Length == 0) { return false; }

            serverCharacter.TargetId.Value = TargetId;

            FaceTarget(serverCharacter, TargetId);

            return true;
        }

        public override void Reset()
        {
            base.Reset();
            m_TargetReticule = null;
            m_CurrentTarget = 0;
            m_NewTarget = 0;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            bool isValid = ActionUtils.IsValidTarget(TargetId);

            if (clientCharacter.ActionPlayer.RunningActionCount == 1 && !clientCharacter.Movement.IsMoving() && isValid)
            {
                //we're the only action running, and we're not moving, so let's swivel to face our target, just to be cool!
                // 현재 실행 중인 액션이 유일하고, 이동하지 않으면, 타겟을 향해 회전하여 멋지게 보이게 만듭니다!
                FaceTarget(clientCharacter, TargetId);
            }

            return isValid;
        }

        public override void Cancel(ServerCharacter serverCharacter)
        {
            if (serverCharacter.TargetId.Value == TargetId)
            {
                serverCharacter.TargetId.Value = 0;
            }
        }

        private ulong TargetId { get { return Data.TargetIds[0]; } }

        /// <summary>
        /// Only call this after validating the target via IsValidTarget.
        /// </summary>
        /// <param name="targetId"></param>
        /// <summary>
        /// IsValidTarget를 통해 타겟을 유효성 검사한 후에만 호출해야 합니다.
        /// </summary>
        /// <param name="targetId"></param>
        private void FaceTarget(ServerCharacter parent, ulong targetId)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var targetObject))
            {
                Vector3 targetObjectPosition;

                if (targetObject.TryGetComponent(out ServerCharacter serverCharacter))
                {
                    targetObjectPosition = serverCharacter.physicsWrapper.Transform.position;
                }
                else
                {
                    targetObjectPosition = targetObject.transform.position;
                }

                Vector3 diff = targetObjectPosition - parent.physicsWrapper.Transform.position;

                diff.y = 0;
                if (diff != Vector3.zero)
                {
                    parent.physicsWrapper.Transform.forward = diff;
                }
            }
        }
    }
}
