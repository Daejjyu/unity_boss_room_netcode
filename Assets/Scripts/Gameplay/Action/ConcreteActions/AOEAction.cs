// <summary>
// Area-of-effect attack Action. The attack is centered on a point provided by the client.
// </summary>
// <summary>
// 범위 공격 액션. 공격은 클라이언트가 제공한 지점을 중심으로 발생합니다.
// </summary>

using System;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// Area-of-effect attack Action. The attack is centered on a point provided by the client.
    /// </summary>
    /// <summary>
    /// 범위 공격 액션. 공격은 클라이언트가 제공한 지점을 중심으로 발생합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "BossRoom/Actions/AOE Action")]
    public class AOEAction : Action
    {
        /// <summary>
        /// Cheat prevention: to ensure that players don't perform AoEs outside of their attack range,
        /// we ensure that the target is less than Range meters away from the player, plus this "fudge
        /// factor" to accommodate miscellaneous minor movement.
        /// </summary>
        /// <summary>
        /// 치트 방지: 플레이어가 공격 범위를 벗어난 곳에서 AoE를 수행하지 않도록, 
        /// 타겟이 플레이어로부터 Range 미터 이내에 있는지 확인하고, 이를 보정하기 위해 "허용 오차" 값을 추가하여 작은 움직임을 고려합니다.
        /// </summary>
        const float k_MaxDistanceDivergence = 1;

        bool m_DidAoE;

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            float distanceAway = Vector3.Distance(serverCharacter.physicsWrapper.Transform.position, Data.Position);
            if (distanceAway > Config.Range + k_MaxDistanceDivergence)
            {
                // Due to latency, it's possible for the client side click check to be out of date with the server driven position. Doing a final check server side to make sure.
                // 지연으로 인해 클라이언트 측 클릭 체크가 서버 위치와 일치하지 않을 수 있습니다. 최종 확인을 서버 측에서 수행하여 확인합니다.
                return ActionConclusion.Stop;
            }

            // broadcasting to all players including myself.
            // We don't know our actual targets for this attack until it triggers, so the client can't use the TargetIds list (and we clear it out for clarity).
            // This means we are responsible for triggering reaction-anims ourselves, which we do in PerformAoe()
            // 모든 플레이어에게 방송합니다. 실제 타겟은 이 공격이 실행될 때까지 알 수 없으므로 클라이언트는 TargetIds 목록을 사용할 수 없습니다 (이 목록을 명확하게 비웁니다).
            // 이는 우리가 직접 반응 애니메이션을 트리거해야 한다는 의미이며, 이는 PerformAoe()에서 처리됩니다.
            Data.TargetIds = new ulong[0];
            serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);
            serverCharacter.clientCharacter.ClientPlayActionRpc(Data);
            return ActionConclusion.Continue;
        }

        public override void Reset()
        {
            base.Reset();
            m_DidAoE = false;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            if (TimeRunning >= Config.ExecTimeSeconds && !m_DidAoE)
            {
                // actually perform the AoE attack
                // 실제로 AoE 공격을 수행합니다.
                m_DidAoE = true;
                PerformAoE(clientCharacter);
            }

            return ActionConclusion.Continue;
        }

        private void PerformAoE(ServerCharacter parent)
        {
            // Note: could have a non-alloc version of this overlap sphere where we statically store our collider array, 
            // but since this is a self-destroyed object, the complexity added to have a static pool of colliders 
            // that could be called by multiplayer players at the same time doesn't seem worth it for now.
            // 참고: 이 오버랩 구체에는 비할당 버전이 있을 수 있으며, 우리가 콜라이더 배열을 정적으로 저장할 수 있습니다. 
            // 그러나 이것은 자기 소멸 객체이므로 멀티플레이어 플레이어가 동시에 호출할 수 있는 정적 풀을 만드는 복잡성은 현재로서는 그만한 가치가 없어 보입니다.
            var colliders = Physics.OverlapSphere(m_Data.Position, Config.Radius, LayerMask.GetMask("NPCs"));
            for (var i = 0; i < colliders.Length; i++)
            {
                var enemy = colliders[i].GetComponent<IDamageable>();
                if (enemy != null)
                {
                    // actually deal the damage
                    // 실제로 피해를 입힙니다.
                    enemy.ReceiveHP(parent, -Config.Amount);
                }
            }
        }

        public override bool OnStartClient(ClientCharacter clientCharacter)
        {
            base.OnStartClient(clientCharacter);
            GameObject.Instantiate(Config.Spawns[0], Data.Position, Quaternion.identity);
            return ActionConclusion.Stop;
        }

        public override bool OnUpdateClient(ClientCharacter clientCharacter)
        {
            throw new Exception("This should not execute");
        }
    }
}
