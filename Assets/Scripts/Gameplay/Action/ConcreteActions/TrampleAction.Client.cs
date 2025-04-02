using System;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.VisualEffects;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    public partial class TrampleAction
    {
        /// <summary>
        /// We spawn the "visual cue" graphics a moment after we begin our action.
        /// (A little extra delay helps ensure we have the correct orientation for the
        /// character, so the graphics are oriented in the right direction!)
        /// </summary>
        /// <summary>
        /// 우리는 액션을 시작한 후 잠시 후 "시각적 신호" 그래픽을 생성합니다.
        /// (약간의 추가 지연이 캐릭터의 올바른 방향을 보장하는 데 도움이 되므로 그래픽이 올바른 방향으로 배치됩니다!)
        /// </summary>
        private const float k_GraphicsSpawnDelay = 0.3f;

        /// <summary>
        /// Prior to spawning graphics, this is null. Once we spawn the graphics, this is a list of everything we spawned.
        /// </summary>
        /// <remarks>
        /// Mobile performance note: constantly creating new GameObjects like this has bad performance on mobile and should
        /// be replaced with object-pooling (i.e. reusing the same art GameObjects repeatedly). But that's outside the scope of this demo.
        /// </remarks>
        /// <summary>
        /// 그래픽을 생성하기 전에는 null입니다. 그래픽을 생성한 후에는 생성한 모든 객체가 들어있는 목록입니다.
        /// </summary>
        /// <remarks>
        /// 모바일 성능 주의: 이렇게 새로운 GameObject를 계속 생성하는 것은 모바일에서 성능이 좋지 않으며,
        /// 오브젝트 풀링(즉, 같은 아트 GameObject를 반복적으로 재사용)을 사용해야 합니다. 그러나 이는 이 데모의 범위를 벗어납니다.
        /// </remarks>
        private List<SpecialFXGraphic> m_SpawnedGraphics = null;

        public override bool OnUpdateClient(ClientCharacter clientCharacter)
        {
            float age = Time.time - TimeStarted;
            if (age > k_GraphicsSpawnDelay && m_SpawnedGraphics == null)
            {
                m_SpawnedGraphics = InstantiateSpecialFXGraphics(clientCharacter.transform, false);
            }

            return true;
        }

        public override void CancelClient(ClientCharacter clientCharacter)
        {
            // we've been aborted -- destroy the "cue graphics"
            // 실행이 취소되었습니다. "시각적 신호 그래픽"을 파괴합니다.
            if (m_SpawnedGraphics != null)
            {
                foreach (var fx in m_SpawnedGraphics)
                {
                    if (fx)
                    {
                        fx.Shutdown();
                    }
                }
            }

            m_SpawnedGraphics = null;
        }
    }
}
