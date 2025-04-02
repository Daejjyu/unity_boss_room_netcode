using System;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// Causes the attacker to teleport near a target spot, then perform a melee attack. The client
    /// visualization moves the character locally beforehand, making the character appear to dash to the
    /// destination spot.
    ///
    /// After the ExecTime has elapsed, the character is immune to damage until the action ends.
    ///
    /// Since the "Range" field means "range when we can teleport to our target", we need another
    /// field to mean "range of our melee attack after dashing". We'll use the "Radius" field of the
    /// ActionDescription for that.
    /// </summary>
    /// <remarks>
    /// See MeleeAction for relevant discussion about targeting; we use the same concept here: preferring
    /// the chosen target, but using whatever is actually within striking distance at time of attack.
    /// </remarks>
    /// <summary>
    /// 공격자가 목표 지점 근처로 순간이동한 후 근접 공격을 수행하게 합니다. 클라이언트
    /// 시각화는 캐릭터를 로컬에서 이동시켜 캐릭터가 목적지 지점으로 돌진하는 것처럼 보이게 합니다.
    ///
    /// ExecTime이 경과하면 캐릭터는 액션이 끝날 때까지 피해를 입지 않습니다.
    ///
    /// "Range" 필드는 "우리가 목표에 순간이동할 수 있는 거리"를 의미하므로, 
    /// "Radius" 필드를 사용하여 "돌진 후 근접 공격의 범위"를 나타냅니다.
    /// </summary>
    /// <remarks>
    /// 타겟팅에 관한 관련 논의는 MeleeAction에서 참조하십시오. 여기서도 같은 개념을 사용합니다: 
    /// 선택된 타겟을 우선시하지만, 실제로 공격 시 거리 내에 있는 타겟을 사용합니다.
    /// </remarks>
    [CreateAssetMenu(menuName = "BossRoom/Actions/Dash Attack Action")]
    public class DashAttackAction : Action
    {
        private Vector3 m_TargetSpot;

        private bool m_Dashed;

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            // remember the exact spot we'll stop.
            // 우리가 멈출 정확한 지점을 기억합니다.
            m_TargetSpot = ActionUtils.GetDashDestination(serverCharacter.physicsWrapper.Transform, Data.Position, true, Config.Range, Config.Range);

            // snap to face our destination. This ensures the client visualization faces the right way while "pretending" to dash
            // 목적지를 향해 방향을 맞춥니다. 이를 통해 클라이언트 시각화에서 "돌진"할 때 올바른 방향을 유지하게 됩니다.
            serverCharacter.physicsWrapper.Transform.LookAt(m_TargetSpot);

            serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);

            // tell clients to visualize this action
            // 클라이언트에게 이 액션을 시각화하도록 알려줍니다.
            serverCharacter.clientCharacter.ClientPlayActionRpc(Data);

            return ActionConclusion.Continue;
        }

        public override void Reset()
        {
            base.Reset();
            m_TargetSpot = default;
            m_Dashed = false;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            return ActionConclusion.Continue;
        }

        public override void End(ServerCharacter serverCharacter)
        {
            // Anim2 contains the name of the end-loop-sequence trigger
            // Anim2는 종료 루프 시퀀스 트리거의 이름을 포함합니다.
            if (!string.IsNullOrEmpty(Config.Anim2))
            {
                serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim2);
            }

            // we're done, time to teleport!
            // 이제 끝났으니 순간이동할 시간입니다!
            serverCharacter.Movement.Teleport(m_TargetSpot);

            // and then swing!
            // 그리고 공격을 시작합니다!
            PerformMeleeAttack(serverCharacter);
        }

        public override void Cancel(ServerCharacter serverCharacter)
        {
            // OtherAnimatorVariable contains the name of the cancellation trigger
            // OtherAnimatorVariable은 취소 트리거의 이름을 포함합니다.
            if (!string.IsNullOrEmpty(Config.OtherAnimatorVariable))
            {
                serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.OtherAnimatorVariable);
            }

            // because the client-side visualization of the action moves the character visualization around,
            // we need to explicitly end the client-side visuals when we abort
            // 클라이언트 측 액션 시각화가 캐릭터 시각화를 이동시키기 때문에, 
            // 우리는 액션을 중단할 때 클라이언트 측 시각화를 명시적으로 종료해야 합니다.
            serverCharacter.clientCharacter.ClientCancelActionsByPrototypeIDRpc(ActionID);

        }

        public override void BuffValue(BuffableValue buffType, ref float buffedValue)
        {
            if (TimeRunning >= Config.ExecTimeSeconds && buffType == BuffableValue.PercentDamageReceived)
            {
                // we suffer no damage during the "dash" (client-side pretend movement)
                // "돌진" 중에는 피해를 입지 않습니다 (클라이언트 측 가상 이동)
                buffedValue = 0;
            }
        }

        private void PerformMeleeAttack(ServerCharacter parent)
        {
            // perform a typical melee-hit. But note that we are using the Radius field for range, not the Range field!
            // 일반적인 근접 공격을 수행합니다. 하지만 우리는 범위에 "Range" 필드가 아니라 "Radius" 필드를 사용하고 있습니다!
            IDamageable foe = MeleeAction.GetIdealMeleeFoe(Config.IsFriendly ^ parent.IsNpc,
                parent.physicsWrapper.DamageCollider,
                                                            Config.Radius,
                                                            (Data.TargetIds != null && Data.TargetIds.Length > 0 ? Data.TargetIds[0] : 0));

            if (foe != null)
            {
                foe.ReceiveHP(parent, -Config.Amount);
            }
        }

        public override bool OnUpdateClient(ClientCharacter clientCharacter)
        {
            if (m_Dashed) { return ActionConclusion.Stop; } // we're done!
            // 끝났습니다!

            return ActionConclusion.Continue;
        }
    }
}
