using System;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// Action that represents a swing of a melee weapon. It is not explicitly targeted, but rather detects the foe that was hit with a physics check.
    /// </summary>
    /// <remarks>
    /// Q: Why do we DetectFoe twice, once in Start, once when we actually connect?
    /// A: The weapon swing doesn't happen instantaneously. We want to broadcast the action to other clients as fast as possible to minimize latency,
    ///    but this poses a conundrum. At the moment the swing starts, you don't know for sure if you've hit anybody yet. There are a few possible resolutions to this:
    ///      1. Do the DetectFoe operation once--in Start.
    ///         Pros: Simple! Only one physics cast per swing--saves on perf.
    ///         Cons: Is unfair. You can step out of the swing of an attack, but no matter how far you go, you'll still be hit. The reverse is also true--you can
    ///               "step into an attack", and it won't affect you. This will feel terrible to the attacker.
    ///      2. Do the DetectFoe operation once--in Update. Send a separate RPC to the targeted entity telling it to play its hit react.
    ///         Pros: Always shows the correct behavior. The entity that gets hit plays its hit react (if any).
    ///         Cons: You need another RPC. Adds code complexity and bandwidth. You also don't have enough information when you start visualizing the swing on
    ///               the client to do any intelligent animation handshaking. If your server->client latency is even a little uneven, your "attack" animation
    ///               won't line up correctly with the hit react, making combat look floaty and disjointed.
    ///      3. Do the DetectFoe operation twice, once in Start and once in Update.
    ///         Pros: Is fair--you do the hit-detect at the moment of the swing striking home. And will generally play the hit react on the right target.
    ///         Cons: Requires more complicated visualization logic. The initial broadcast foe can only ever be treated as a "hint". The graphics logic
    ///               needs to do its own range checking to pick the best candidate to play the hit react on.
    /// 
    /// As so often happens in networked games (and games in general), there's no perfect solution--just sets of tradeoffs. For our example, we're showing option "3".
    /// </remarks>
    /// <summary>
    /// 근접 무기의 휘두르기를 나타내는 액션입니다. 명시적으로 타겟팅되지는 않지만, 물리적 검사를 통해 적을 감지합니다.
    /// </summary>
    /// <remarks>
    /// Q: 왜 DetectFoe를 두 번 호출하나요? 한 번은 Start에서, 한 번은 실제로 연결될 때.
    /// A: 무기 휘두르기는 즉시 발생하지 않습니다. 가능한 한 빠르게 액션을 다른 클라이언트에 브로드캐스트하여 지연 시간을 최소화하려고 하지만,
    ///    이는 몇 가지 문제를 야기합니다. 휘두르기가 시작될 때는 적을 맞혔는지 확실히 알 수 없습니다. 이에 대한 해결책은 여러 가지가 있습니다:
    ///      1. DetectFoe 작업을 한 번만 수행--Start에서.
    ///         장점: 간단합니다! 휘두를 때마다 물리적 검사 한 번만 수행--성능 절약.
    ///         단점: 공정하지 않습니다. 공격 범위에서 벗어날 수 있지만, 얼마나 멀리 가더라도 여전히 맞을 수 있습니다. 반대로 "공격 범위 안으로 들어가면"
    ///               맞지 않게 됩니다. 이는 공격자에게 매우 불쾌하게 느껴질 수 있습니다.
    ///      2. DetectFoe 작업을 한 번만 수행--Update에서. 타겟에 별도의 RPC를 보내 타격 반응을 실행하게 합니다.
    ///         장점: 항상 올바른 동작을 보여줍니다. 맞은 엔티티는 타격 반응을 실행합니다 (있을 경우).
    ///         단점: 추가 RPC가 필요합니다. 코드 복잡성과 대역폭을 증가시킵니다. 또한 휘두르기를 시각화하는 동안 충분한 정보를 얻지 못해
    ///               애니메이션 핸드쉐이킹을 제대로 수행할 수 없습니다. 서버-클라이언트 지연 시간이 조금만 불균형하면 "공격" 애니메이션이
    ///               타격 반응과 맞지 않아 전투가 어색하고 분리되어 보일 수 있습니다.
    ///      3. DetectFoe 작업을 두 번 수행, 한 번은 Start에서, 한 번은 Update에서.
    ///         장점: 공정합니다--휘두르기가 적을 정확히 맞출 때 타격을 감지합니다. 일반적으로 올바른 타겟에서 타격 반응을 실행합니다.
    ///         단점: 시각화 로직이 더 복잡해집니다. 초기 타겟 방송은 "힌트"로만 취급됩니다. 그래픽 로직은 최적의 후보를 선택하는
    ///               자체 범위 검사를 수행해야 합니다.
    /// 
    /// 네트워크 게임에서 (그리고 일반적으로 게임에서) 자주 발생하는 문제로, 완벽한 해결책은 없으며, 단지 다양한 트레이드오프가 존재합니다.
    /// 우리의 예에서는 "3번" 옵션을 보여줍니다.
    /// </remarks>
    [CreateAssetMenu(menuName = "BossRoom/Actions/Melee Action")]
    public partial class MeleeAction : Action
    {
        private bool m_ExecutionFired;
        private ulong m_ProvisionalTarget;

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            ulong target = (Data.TargetIds != null && Data.TargetIds.Length > 0) ? Data.TargetIds[0] : serverCharacter.TargetId.Value;
            IDamageable foe = DetectFoe(serverCharacter, target);
            if (foe != null)
            {
                m_ProvisionalTarget = foe.NetworkObjectId;
                Data.TargetIds = new ulong[] { foe.NetworkObjectId };
            }

            // snap to face the right direction
            // 올바른 방향을 향하도록 조정
            if (Data.Direction != Vector3.zero)
            {
                serverCharacter.physicsWrapper.Transform.forward = Data.Direction;
            }

            serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);
            serverCharacter.clientCharacter.ClientPlayActionRpc(Data);
            return true;
        }

        public override void Reset()
        {
            base.Reset();
            m_ExecutionFired = false;
            m_ProvisionalTarget = 0;
            m_ImpactPlayed = false;
            m_SpawnedGraphics = null;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            if (!m_ExecutionFired && (Time.time - TimeStarted) >= Config.ExecTimeSeconds)
            {
                m_ExecutionFired = true;
                var foe = DetectFoe(clientCharacter, m_ProvisionalTarget);
                if (foe != null)
                {
                    foe.ReceiveHP(clientCharacter, -Config.Amount);
                }
            }

            return true;
        }

        /// <summary>
        /// Returns the ServerCharacter of the foe we hit, or null if none found.
        /// </summary>
        /// <returns></returns>
        /// <summary>
        /// 우리가 맞힌 적의 ServerCharacter를 반환하거나, 없으면 null을 반환합니다.
        /// </summary>
        private IDamageable DetectFoe(ServerCharacter parent, ulong foeHint = 0)
        {
            return GetIdealMeleeFoe(Config.IsFriendly ^ parent.IsNpc, parent.physicsWrapper.DamageCollider, Config.Range, foeHint);
        }

        /// <summary>
        /// Utility used by Actions to perform Melee attacks. Performs a melee hit-test
        /// and then looks through the results to find an alive target, preferring the provided
        /// enemy.
        /// </summary>
        /// <param name="isNPC">true if the attacker is an NPC (and therefore should hit PCs). False for the reverse.</param>
        /// <param name="ourCollider">The collider of the attacking GameObject.</param>
        /// <param name="meleeRange">The range in meters to check for foes.</param>
        /// <param name="preferredTargetNetworkId">The NetworkObjectId of our preferred foe, or 0 if no preference</param>
        /// <returns>ideal target's IDamageable, or null if no valid target found</returns>
        /// <summary>
        /// 액션에서 근접 공격을 수행하기 위한 유틸리티입니다. 근접 타격 테스트를 수행한 후,
        /// 결과를 통해 살아있는 타겟을 찾으며, 제공된 적을 우선시합니다.
        /// </summary>
        /// <param name="isNPC">공격자가 NPC인 경우 true, 그렇지 않으면 false</param>
        /// <param name="ourCollider">공격하는 GameObject의 콜라이더</param>
        /// <param name="meleeRange">적을 감지할 범위(미터)</param>
        /// <param name="preferredTargetNetworkId">우선시할 적의 NetworkObjectId, 우선시할 적이 없으면 0</param>
        /// <returns>이상적인 타겟의 IDamageable, 유효한 타겟이 없으면 null</returns>
        public static IDamageable GetIdealMeleeFoe(bool isNPC, Collider ourCollider, float meleeRange, ulong preferredTargetNetworkId)
        {
            RaycastHit[] results;
            int numResults = ActionUtils.DetectMeleeFoe(isNPC, ourCollider, meleeRange, out results);

            IDamageable foundFoe = null;

            // everything that got hit by the raycast should have an IDamageable component, so we can retrieve that and see if they're appropriate targets.
            // we always prefer the hinted foe. If he's still in range, he should take the damage, because he's who the client visualization
            // system will play the hit-react on (in case there's any ambiguity).
            // 레이캐스트에 맞은 모든 객체는 IDamageable 컴포넌트를 가져야 하므로 이를 통해 적합한 타겟인지 확인할 수 있습니다.
            // 우리는 항상 힌트로 제공된 적을 우선시합니다. 그가 여전히 범위 안에 있다면 그는 피해를 받아야 하며,
            // 클라이언트 시각화 시스템은 그에게 타격 반응을 실행할 것입니다 (모호함이 있을 경우).
            for (int i = 0; i < numResults; i++)
            {
                var damageable = results[i].collider.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsDamageable() &&
                    (damageable.NetworkObjectId == preferredTargetNetworkId || foundFoe == null))
                {
                    foundFoe = damageable;
                }
            }

            return foundFoe;
        }
    }
}