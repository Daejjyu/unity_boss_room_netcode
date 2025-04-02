// <summary>
// This class manages the visual effects of a charged launch projectile action on the client side.
// </summary>
// <summary>
// 이 클래스는 클라이언트 측에서 충전된 발사 투사체 액션의 시각적 효과를 관리합니다.
// </summary>

using System;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.VisualEffects;

namespace Unity.BossRoom.Gameplay.Actions
{
    public partial class ChargedLaunchProjectileAction
    {
        /// <summary>
        /// A list of the special particle graphics we spawned.
        /// </summary>
        /// <summary>
        /// 우리가 생성한 특별한 파티클 그래픽들의 목록입니다.
        /// </summary>
        /// <remarks>
        /// Performance note: repeatedly creating and destroying GameObjects is not optimal, and on low-resource platforms
        /// (like mobile devices), it can lead to major performance problems. On mobile platforms, visual graphics should
        /// use object-pooling (i.e. reusing the same GameObjects repeatedly). But that's outside the scope of this demo.
        /// </remarks>
        /// <remarks>
        /// 성능 참고: GameObject를 반복적으로 생성하고 파괴하는 것은 최적화되지 않으며, 저사양 플랫폼(모바일 기기 등)에서는 성능 문제를 일으킬 수 있습니다.
        /// 모바일 플랫폼에서는 시각적 그래픽이 객체 풀링을 사용해야 합니다(즉, 동일한 GameObject를 반복적으로 재사용). 하지만 이는 이 데모의 범위를 벗어납니다.
        /// </remarks>
        private List<SpecialFXGraphic> m_Graphics = new List<SpecialFXGraphic>();

        private bool m_ChargeEnded;

        public override bool OnStartClient(ClientCharacter clientCharacter)
        {
            base.OnStartClient(clientCharacter);

            m_Graphics = InstantiateSpecialFXGraphics(clientCharacter.transform, true);
            return true;
        }

        public override bool OnUpdateClient(ClientCharacter clientCharacter)
        {
            return !m_ChargeEnded;
        }

        public override void CancelClient(ClientCharacter clientCharacter)
        {
            if (!m_ChargeEnded)
            {
                foreach (var graphic in m_Graphics)
                {
                    if (graphic)
                    {
                        graphic.Shutdown();
                    }
                }
            }
        }

        public override void OnStoppedChargingUpClient(ClientCharacter clientCharacter, float finalChargeUpPercentage)
        {
            m_ChargeEnded = true;
            foreach (var graphic in m_Graphics)
            {
                if (graphic)
                {
                    graphic.Shutdown();
                }
            }

            // the graphics will now take care of themselves and shutdown, so we can forget about 'em
            // 이제 그래픽들은 스스로 종료되며 관리되므로 우리는 더 이상 신경 쓸 필요가 없습니다.
            m_Graphics.Clear();
        }
    }
}
