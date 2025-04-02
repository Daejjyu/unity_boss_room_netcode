using System;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    [CreateAssetMenu(menuName = "BossRoom/Actions/Chase Action")]
    public class ChaseAction : Action
    {
        private NetworkObject m_Target;

        Transform m_TargetTransform;

        /// <summary>
        /// Called when the Action starts actually playing (which may be after it is created, because of queueing).
        /// </summary>
        /// <summary>
        /// 액션이 실제로 실행되기 시작할 때 호출됩니다 (이는 생성된 후, 큐에 의해 실행될 수 있습니다).
        /// </summary>
        /// <returns>false if the action decided it doesn't want to run after all, true otherwise. </returns>
        /// <returns>액션이 최종적으로 실행하지 않기로 결정한 경우 false를 반환하고, 그렇지 않으면 true를 반환합니다.</returns>
        public override bool OnStart(ServerCharacter serverCharacter)
        {
            if (!HasValidTarget())
            {
                Debug.Log("Failed to start ChaseAction. The target entity  wasn't submitted or doesn't exist anymore");
                return ActionConclusion.Stop;
            }

            m_Target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[m_Data.TargetIds[0]];

            if (PhysicsWrapper.TryGetPhysicsWrapper(m_Data.TargetIds[0], out var physicsWrapper))
            {
                m_TargetTransform = physicsWrapper.Transform;
            }
            else
            {
                m_TargetTransform = m_Target.transform;
            }

            Vector3 currentTargetPos = m_TargetTransform.position;

            if (StopIfDone(serverCharacter))
            {
                serverCharacter.physicsWrapper.Transform.LookAt(currentTargetPos); //even if we didn't move, snap to face the target!
                return ActionConclusion.Stop;
            }

            if (!serverCharacter.Movement.IsPerformingForcedMovement())
            {
                serverCharacter.Movement.FollowTransform(m_TargetTransform);
            }
            return ActionConclusion.Continue;
        }

        public override void Reset()
        {
            base.Reset();
            m_Target = null;
            m_TargetTransform = null;
        }

        /// <summary>
        /// Returns true if our ActionRequestData came with a valid target. For the ChaseAction, this is pretty liberal (could be friend or foe, could be
        /// dead or alive--just needs to be present).
        /// </summary>
        /// <summary>
        /// 액션 요청 데이터에 유효한 타겟이 포함되어 있으면 true를 반환합니다. ChaseAction의 경우, 이는 꽤 너그럽습니다 (친구나 적, 죽은 혹은 살아있는 타겟이 될 수 있음).
        /// </summary>
        private bool HasValidTarget()
        {
            return m_Data.TargetIds != null &&
                m_Data.TargetIds.Length > 0 &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.ContainsKey(m_Data.TargetIds[0]);
        }

        /// <summary>
        /// Tests to see if we've reached our target. Returns true if we've reached our target, false otherwise (in which case it also stops our movement).
        /// </summary>
        /// <summary>
        /// 타겟에 도달했는지 확인합니다. 타겟에 도달했으면 true를 반환하고, 그렇지 않으면 false를 반환합니다 (이 경우 이동도 멈춥니다).
        /// </summary>
        private bool StopIfDone(ServerCharacter parent)
        {
            if (m_TargetTransform == null)
            {
                //if the target disappeared on us, then just stop.
                //타겟이 사라졌다면 그냥 멈춥니다.
                Cancel(parent);
                return true;
            }

            float distToTarget2 = (parent.physicsWrapper.Transform.position - m_TargetTransform.position).sqrMagnitude;
            if ((m_Data.Amount * m_Data.Amount) > distToTarget2)
            {
                //we made it! we're done.
                //우리가 도달했습니다! 이제 끝입니다.
                Cancel(parent);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Called each frame while the action is running.
        /// </summary>
        /// <summary>
        /// 액션이 실행 중일 때 매 프레임마다 호출됩니다.
        /// </summary>
        /// <returns>true to keep running, false to stop. The Action will stop by default when its duration expires, if it has a duration set. </returns>
        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            if (StopIfDone(clientCharacter)) { return ActionConclusion.Stop; }

            // Keep re-assigning our chase target whenever possible.
            // 이 방법으로, 추격 중에 밀려서 멈췄을 때도 바로 타겟을 다시 할당하여 추격을 계속할 수 있습니다.
            if (!clientCharacter.Movement.IsPerformingForcedMovement())
            {
                clientCharacter.Movement.FollowTransform(m_TargetTransform);
            }

            return ActionConclusion.Continue;
        }

        public override void Cancel(ServerCharacter serverCharacter)
        {
            if (serverCharacter.Movement && !serverCharacter.Movement.IsPerformingForcedMovement())
            {
                serverCharacter.Movement.CancelMove();
            }
        }

        public override bool OnUpdateClient(ClientCharacter clientCharacter)
        {
            return ActionConclusion.Continue;
        }
    }
}
