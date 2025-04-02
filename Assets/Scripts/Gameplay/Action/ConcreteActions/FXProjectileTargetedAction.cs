using System;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// Action that represents an always-hit raybeam-style ranged attack. A particle is shown from caster to target, and then the
    /// target takes damage. (It is not possible to escape the hit; the target ALWAYS takes damage.) This is intended for fairly
    /// swift particles; the time before it's applied is based on a simple distance-check at the attack's start.
    /// (If no target is provided (because the user clicked on an empty spot on the map) or if the caster doesn't have line of
    /// sight to the target (because it's behind a wall), we still perform an action, it just hits nothing.
    /// </summary>
    /// <summary>
    /// 항상 명중하는 레이빔 스타일의 원거리 공격을 나타내는 액션입니다. 캐스터에서 목표로 입자(파티클)가 표시되고, 
    /// 그 후 목표는 피해를 받습니다. (명중을 피할 수 없으며, 목표는 항상 피해를 받습니다.) 이는 비교적 빠른 입자에 
    /// 사용됩니다. 피해가 적용되기 전 시간은 공격 시작 시 간단한 거리 체크를 기반으로 계산됩니다.
    /// (목표가 제공되지 않았거나 (사용자가 지도에서 빈 곳을 클릭한 경우) 캐스터가 목표에 대한 시야가 없다면 
    /// (벽 뒤에 있다면), 우리는 여전히 액션을 수행하지만 아무것도 명중하지 않습니다.)
    /// </summary>

    [CreateAssetMenu(menuName = "BossRoom/Actions/FX Projectile Targeted Action")]
    public partial class FXProjectileTargetedAction : Action
    {
        private bool m_ImpactedTarget;
        private float m_TimeUntilImpact;
        private IDamageable m_DamageableTarget;

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            m_DamageableTarget = GetDamageableTarget(serverCharacter);

            // figure out where the player wants us to aim at...
            // 플레이어가 우리에게 조준할 목표가 어디인지 확인
            Vector3 targetPos = m_DamageableTarget != null ? m_DamageableTarget.transform.position : m_Data.Position;

            // then make sure we can actually see that point!
            // 그리고 실제로 그 지점을 볼 수 있는지 확인
            if (!ActionUtils.HasLineOfSight(serverCharacter.physicsWrapper.Transform.position, targetPos, out Vector3 collidePos))
            {
                // we do not have line of sight to the target point. So our target instead becomes the obstruction point
                // 우리는 목표 지점에 대한 시야가 없습니다. 그래서 우리의 목표는 대신 장애물 지점이 됩니다.
                m_DamageableTarget = null;
                targetPos = collidePos;

                // and update our action data so that when we send it to the clients, it will be up-to-date
                // 그리고 클라이언트에게 보낼 때 최신 상태가 되도록 액션 데이터를 업데이트합니다.
                Data.TargetIds = new ulong[0];
                Data.Position = targetPos;
            }

            // turn to face our target!
            // 목표를 향해 돌리기
            serverCharacter.physicsWrapper.Transform.LookAt(targetPos);

            // figure out how long the pretend-projectile will be flying to the target
            // 가상의 투사체가 목표 지점까지 비행하는 시간이 얼마나 걸릴지 계산
            float distanceToTargetPos = Vector3.Distance(targetPos, serverCharacter.physicsWrapper.Transform.position);
            m_TimeUntilImpact = Config.ExecTimeSeconds + (distanceToTargetPos / Config.Projectiles[0].Speed_m_s);

            serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);
            // tell clients to visualize this action
            // 클라이언트에게 이 액션을 시각화하라고 알림
            serverCharacter.clientCharacter.ClientPlayActionRpc(Data);
            return true;
        }

        public override void Reset()
        {
            base.Reset();

            m_ImpactedTarget = false;
            m_TimeUntilImpact = 0;
            m_DamageableTarget = null;
            m_ImpactPlayed = false;
            m_ProjectileDuration = 0;
            m_Projectile = null;
            m_Target = null;
            m_TargetTransform = null;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            if (!m_ImpactedTarget && m_TimeUntilImpact <= TimeRunning)
            {
                m_ImpactedTarget = true;
                if (m_DamageableTarget != null)
                {
                    m_DamageableTarget.ReceiveHP(clientCharacter, -Config.Projectiles[0].Damage);
                }
            }
            return true;
        }

        public override void Cancel(ServerCharacter serverCharacter)
        {
            if (!m_ImpactedTarget)
            {
                serverCharacter.clientCharacter.ClientCancelActionsByPrototypeIDRpc(ActionID);
            }
        }

        /// <summary>
        /// Returns our intended target, or null if not found/no target.
        /// </summary>
        /// <summary>
        /// 우리의 의도한 목표를 반환하거나, 찾을 수 없거나 목표가 없으면 null을 반환합니다.
        /// </summary>
        private IDamageable GetDamageableTarget(ServerCharacter parent)
        {
            if (Data.TargetIds == null || Data.TargetIds.Length == 0)
            {
                return null;
            }

            NetworkObject obj;
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(Data.TargetIds[0], out obj) && obj != null)
            {
                // make sure this isn't a friend (or if it is, make sure this is a friendly-fire action)
                // 이것이 아군이 아닌지 확인 (혹시 아군이라면, 친선 사격 액션인지 확인)
                var serverChar = obj.GetComponent<ServerCharacter>();
                if (serverChar && serverChar.IsNpc == (Config.IsFriendly ^ parent.IsNpc))
                {
                    // not a valid target
                    // 유효한 목표가 아닙니다
                    return null;
                }

                if (PhysicsWrapper.TryGetPhysicsWrapper(Data.TargetIds[0], out var physicsWrapper))
                {
                    return physicsWrapper.DamageCollider.GetComponent<IDamageable>();
                }
                else
                {
                    return obj.GetComponent<IDamageable>();
                }
            }
            else
            {
                // target could have legitimately disappeared in the time it took to queue this action... but that's pretty unlikely, so we'll log about it to ease debugging
                // 목표가 이 액션이 큐에 추가되는 동안 실제로 사라졌을 수 있습니다... 하지만 이는 매우 드물기 때문에 디버깅을 쉽게 하기 위해 로그를 남깁니다.
                Debug.Log($"FXProjectileTargetedAction was targeted at ID {Data.TargetIds[0]}, but that target can't be found in spawned object list! (May have just been deleted?)");
                return null;
            }
        }
    }
}
// Compare this snippet from Assets/Scripts/Gameplay/Action/ConcreteActions/FXProjectileTargetedAction.Client.cs: