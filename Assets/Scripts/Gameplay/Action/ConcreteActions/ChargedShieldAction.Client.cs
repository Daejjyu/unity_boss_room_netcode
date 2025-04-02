// <summary>
// This class manages the behavior of a charged shield action in the game, including 
// graphics related to charging and activating the shield, and handling animation triggers.
// </summary>
// <summary>
// 이 클래스는 게임에서 차지된 방패 액션의 동작을 관리하며, 
// 차징 및 방패 활성화와 관련된 그래픽과 애니메이션 트리거 처리를 담당합니다.
// </summary>

using System;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.VisualEffects;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    public partial class ChargedShieldAction
    {
        /// <summary>
        /// The "charging up" graphics. These are disabled as soon as the player stops charging up
        /// </summary>
        /// <summary>
        /// "충전 중" 그래픽. 플레이어가 충전을 멈추면 바로 비활성화됩니다.
        /// </summary>
        SpecialFXGraphic m_ChargeGraphics;

        /// <summary>
        /// The "I'm fully charged" graphics. This is null until instantiated
        /// </summary>
        /// <summary>
        /// "완전히 충전된" 그래픽. 이 객체는 인스턴스화될 때까지 null입니다.
        /// </summary>
        SpecialFXGraphic m_ShieldGraphics;

        public override bool OnUpdateClient(ClientCharacter clientCharacter)
        {
            return IsChargingUp() || (Time.time - m_StoppedChargingUpTime) < Config.EffectDurationSeconds;
        }

        public override void CancelClient(ClientCharacter clientCharacter)
        {
            if (IsChargingUp())
            {
                // we never actually stopped "charging up" so do necessary clean up here
                // 실제로 "충전 중"이 멈추지 않았으므로 여기서 필요한 정리 작업을 수행합니다.
                if (m_ChargeGraphics)
                {
                    m_ChargeGraphics.Shutdown();
                }
            }

            if (m_ShieldGraphics)
            {
                m_ShieldGraphics.Shutdown();
            }
        }

        public override void OnStoppedChargingUpClient(ClientCharacter clientCharacter, float finalChargeUpPercentage)
        {
            if (!IsChargingUp()) { return; }

            m_StoppedChargingUpTime = Time.time;
            if (m_ChargeGraphics)
            {
                m_ChargeGraphics.Shutdown();
                m_ChargeGraphics = null;
            }

            // if fully charged, we show a special graphic
            // 완전히 충전되었으면, 특별한 그래픽을 표시합니다.
            if (Mathf.Approximately(finalChargeUpPercentage, 1))
            {
                m_ShieldGraphics = InstantiateSpecialFXGraphic(Config.Spawns[1], clientCharacter.transform, true);
            }
        }

        public override void AnticipateActionClient(ClientCharacter clientCharacter)
        {
            // because this action can be visually started and stopped as often and as quickly as the player wants, it's possible
            // for several copies of this action to be playing at once. This can lead to situations where several
            // dying versions of the action raise the end-trigger, but the animator only lowers it once, leaving the trigger
            // in a raised state. So we'll make sure that our end-trigger isn't raised yet. (Generally a good idea anyway.)
            // 이 액션은 플레이어가 원할 때마다 시각적으로 시작하고 멈출 수 있기 때문에 여러 번 실행될 수 있습니다. 
            // 이로 인해 여러 죽어가는 액션 버전이 종료 트리거를 활성화할 수 있지만, 애니메이터는 한 번만 이를 비활성화하여 
            // 트리거가 활성화된 상태로 남을 수 있습니다. 따라서 종료 트리거가 아직 활성화되지 않았는지 확인합니다. 
            // (어쨌든 좋은 아이디어입니다.)
            clientCharacter.OurAnimator.ResetTrigger(Config.Anim2);
            base.AnticipateActionClient(clientCharacter);
        }
    }
}
