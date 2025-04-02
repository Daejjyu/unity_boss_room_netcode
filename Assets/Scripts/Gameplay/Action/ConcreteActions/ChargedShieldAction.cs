// <summary>
// A defensive action where the character becomes resistant to damage. 
// The player can charge it up for increased effectiveness, and it provides special benefits when fully charged.
// </summary>
// <summary>
// 캐릭터가 피해에 저항하는 방어적인 액션입니다. 
// 플레이어는 이를 차지하여 효과를 높일 수 있으며, 완전히 차지되었을 때 특별한 혜택을 제공합니다.
// </summary>

using System;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// A defensive action where the character becomes resistant to damage.
    /// </summary>
    /// <remarks>
    /// The player can hold down the button for this ability to "charge it up" and make it more effective. Once it's been
    /// charging for Description.ExecTimeSeconds, it reaches maximum charge. If the player is attacked by an enemy, that
    /// also immediately stops the charge-up.
    /// 
    /// Once the charge-up stops (for any reason), the Action lasts for Description.EffectTimeSeconds before elapsing. During
    /// this time, all incoming damage is reduced by a percentage from 50% to 100%, depending on how "charged up" it was.
    /// 
    /// When the Action is fully charged up, it provides a special additional benefit: if the boss tries to trample this
    /// character, the boss becomes Stunned.
    /// </remarks>
    /// <summary>
    /// 이 액션은 캐릭터가 피해에 저항하는 방어적인 액션입니다. 
    /// 플레이어는 이를 차지하여 효과를 더욱 강하게 만들 수 있습니다. 
    /// 최대 차지 후, 적의 공격을 받으면 충전이 멈춥니다.
    /// 충전이 멈추면 액션은 일정 시간 동안 지속되며, 이 시간 동안 피해를 일정 비율로 줄여줍니다.
    /// 완전히 충전되었을 때, 보스가 캐릭터를 짓누르려 할 때 보스가 기절하는 추가적인 효과가 발생합니다.
    /// </summary>

    [CreateAssetMenu(menuName = "BossRoom/Actions/Charged Shield Action")]
    public partial class ChargedShieldAction : Action
    {
        /// <summary>
        /// Set once we've stopped charging up, for any reason:
        /// - the player has let go of the button,
        /// - we were attacked,
        /// - or the maximum charge was reached.
        /// </summary>
        /// <summary>
        /// 충전이 멈추었을 때 설정됩니다:
        /// - 플레이어가 버튼을 놓았을 때,
        /// - 공격을 받았을 때,
        /// - 최대 충전에 도달했을 때.
        /// </summary>
        private float m_StoppedChargingUpTime = 0;

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            if (m_Data.TargetIds != null && m_Data.TargetIds.Length > 0)
            {
                NetworkObject initialTarget = NetworkManager.Singleton.SpawnManager.SpawnedObjects[m_Data.TargetIds[0]];
                if (initialTarget)
                {
                    // face our target, if we had one
                    // 타겟이 있으면 타겟을 향해 얼굴을 돌립니다.
                    serverCharacter.physicsWrapper.Transform.LookAt(initialTarget.transform.position);
                }
            }

            // because this action can be visually started and stopped as often and as quickly as the player wants, it's possible
            // for several copies of this action to be playing at once. This can lead to situations where several
            // dying versions of the action raise the end-trigger, but the animator only lowers it once, leaving the trigger
            // in a raised state. So we'll make sure that our end-trigger isn't raised yet. (Generally a good idea anyway.)
            // 이 액션은 플레이어가 원할 때마다 시각적으로 시작하고 멈출 수 있기 때문에 여러 번 실행될 수 있습니다. 
            // 이로 인해 여러 죽어가는 액션 버전이 종료 트리거를 활성화할 수 있지만, 애니메이터는 한 번만 이를 비활성화하여 
            // 트리거가 활성화된 상태로 남을 수 있습니다. 따라서 종료 트리거가 아직 활성화되지 않았는지 확인합니다. 
            // (어쨌든 좋은 아이디어입니다.)
            serverCharacter.serverAnimationHandler.NetworkAnimator.ResetTrigger(Config.Anim2);

            // raise the start trigger to start the animation loop!
            // 애니메이션 루프를 시작하기 위해 시작 트리거를 활성화합니다.
            serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);

            serverCharacter.clientCharacter.ClientPlayActionRpc(Data);
            return true;
        }

        public override void Reset()
        {
            base.Reset();
            m_ChargeGraphics = null;
            m_ShieldGraphics = null;
            m_StoppedChargingUpTime = 0;
        }

        private bool IsChargingUp()
        {
            return m_StoppedChargingUpTime == 0;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            if (m_StoppedChargingUpTime == 0)
            {
                // we haven't explicitly stopped charging up... but if we've reached max charge, that implicitly stops us
                // 명시적으로 충전이 멈추지 않았지만, 최대 충전이 되면 자동으로 멈춥니다.
                if (TimeRunning >= Config.ExecTimeSeconds)
                {
                    StopChargingUp(clientCharacter);
                }
            }

            // we stop once the charge-up has ended and our effect duration has elapsed
            // 충전이 끝나고 효과 지속 시간이 지나면 멈춥니다.
            return m_StoppedChargingUpTime == 0 || Time.time < (m_StoppedChargingUpTime + Config.EffectDurationSeconds);
        }

        public override bool ShouldBecomeNonBlocking()
        {
            return m_StoppedChargingUpTime != 0;
        }

        private float GetPercentChargedUp()
        {
            return ActionUtils.GetPercentChargedUp(m_StoppedChargingUpTime, TimeRunning, TimeStarted, Config.ExecTimeSeconds);
        }

        public override void BuffValue(BuffableValue buffType, ref float buffedValue)
        {
            if (buffType == BuffableValue.PercentDamageReceived)
            {
                float percentChargedUp = GetPercentChargedUp();

                // the amount of damage reduction starts at 50% (for not-charged-up), then slowly increases to 100% depending on how charged-up we got
                // 피해 감소 비율은 50%에서 시작하여 차지가 될수록 서서히 100%로 증가합니다.
                float percentDamageReduction = 0.5f + ((percentChargedUp * percentChargedUp) / 2);

                // Now that we know how much damage to reduce it by, we need to set buffedValue to the inverse (because
                // it's looking for how much damage to DO, not how much to REDUCE BY). Also note how we don't just SET
                // buffedValue... we multiply our buff in with the current value. This lets our Action "stack"
                // with any other Actions that also alter this variable.)
                // 얼마나 피해를 줄일지 알았으므로, buffedValue는 그 반대로 설정해야 합니다 (피해를 얼마나 줄일지가 아니라 얼마나 줄일지).
                buffedValue *= 1 - percentDamageReduction;
            }
            else if (buffType == BuffableValue.ChanceToStunTramplers)
            {
                // if we are at "full charge", we stun enemies that try to trample us!
                // 완전 충전 상태에서는 우리를 짓밟으려는 적을 기절시킵니다.
                if (GetPercentChargedUp() >= 1)
                {
                    buffedValue = 1;
                }
            }
        }

        public override void OnGameplayActivity(ServerCharacter serverCharacter, GameplayActivity activityType)
        {
            // for this particular type of Action, being attacked immediately causes you to stop charging up
            // 이 액션의 경우, 공격을 받으면 즉시 충전이 멈춥니다.
            if (activityType == GameplayActivity.AttackedByEnemy || activityType == GameplayActivity.StoppedChargingUp)
            {
                StopChargingUp(serverCharacter);
            }
        }

        public override void Cancel(ServerCharacter serverCharacter)
        {
            StopChargingUp(serverCharacter);

            // if stepped into invincibility, decrement invincibility counter
            // 무적 상태에 들어갔다면 무적 카운터를 감소시킵니다.
            if (Mathf.Approximately(GetPercentChargedUp(), 1f))
            {
                serverCharacter.serverAnimationHandler.NetworkAnimator.Animator.SetInteger(Config.OtherAnimatorVariable,
                    serverCharacter.serverAnimationHandler.NetworkAnimator.Animator.GetInteger(Config.OtherAnimatorVariable) - 1);
            }
        }

        private void StopChargingUp(ServerCharacter parent)
        {
            if (IsChargingUp())
            {
                m_StoppedChargingUpTime = Time.time;
                parent.clientCharacter.ClientStopChargingUpRpc(GetPercentChargedUp());

                parent.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim2);

                parent.serverAnimationHandler.NetworkAnimator.ResetTrigger(Config.Anim);

                //tell the animator controller to enter "invincibility mode" (where we don't flinch from damage)
                // 애니메이터에게 "무적 모드"로 전환하라고 지시합니다 (이 상태에서는 피해를 받지 않습니다).
                if (Mathf.Approximately(GetPercentChargedUp(), 1f))
                {
                    // increment our "invincibility counter". We use an integer count instead of a boolean because the player
                    // can restart their shield before the first one has ended, thereby getting two stacks of invincibility.
                    // So each active copy of the charge-up increments the invincibility counter, and the animator controller
                    // knows anything greater than zero means we shouldn't show hit-reacts.
                    // "무적 카운터"를 증가시킵니다. 우리는 부울 대신 정수 카운트를 사용하여 플레이어가 첫 번째 무적이 끝나기 전에 
                    // 방패를 다시 시작할 수 있게 하며, 이를 통해 두 번의 무적 상태를 얻을 수 있습니다.
                    parent.serverAnimationHandler.NetworkAnimator.Animator.SetInteger(Config.OtherAnimatorVariable,
                        parent.serverAnimationHandler.NetworkAnimator.Animator.GetInteger(Config.OtherAnimatorVariable) + 1);
                }
            }
        }

        public override bool OnStartClient(ClientCharacter clientCharacter)
        {
            Assert.IsTrue(Config.Spawns.Length == 2, $"Found {Config.Spawns.Length} spawns for action {name}. Should be exactly 2: a charge-up particle and a fully-charged particle");

            base.OnStartClient(clientCharacter);
            m_ChargeGraphics = InstantiateSpecialFXGraphic(Config.Spawns[0], clientCharacter.transform, true);
            return true;
        }
    }
}
