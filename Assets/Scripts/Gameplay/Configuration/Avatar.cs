using System;
using Unity.BossRoom.Infrastructure;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Configuration
{
    /// <summary>
    /// This ScriptableObject defines a Player Character for BossRoom. It defines its CharacterClass field for
    /// associated game-specific properties, as well as its graphics representation.
    /// </summary>
    /// <summary>
    /// 이 ScriptableObject는 BossRoom의 플레이어 캐릭터를 정의합니다. 이는 관련된 게임 특정 속성을 위한 CharacterClass 필드를
    /// 정의하며, 그래픽 표현도 포함합니다.
    /// </summary>
    [CreateAssetMenu]
    [Serializable]
    public sealed class Avatar : GuidScriptableObject
    {
        public CharacterClass CharacterClass;

        public GameObject Graphics;

        public GameObject GraphicsCharacterSelect;

        public Sprite Portrait;
    }
}
