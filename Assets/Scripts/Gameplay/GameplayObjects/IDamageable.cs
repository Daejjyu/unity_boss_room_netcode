using System;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
    /// <summary>
    /// Generic interface for damageable objects in the game. This includes ServerCharacter, as well as other things like
    /// ServerBreakableLogic.
    /// </summary>
    /// <summary>
    /// 게임 내에서 피해를 받을 수 있는 객체에 대한 일반적인 인터페이스입니다. 
    /// 여기에는 ServerCharacter뿐만 아니라 ServerBreakableLogic과 같은 객체도 포함됩니다.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Receives HP damage or healing.
        /// </summary>
        /// <summary>
        /// HP 피해 또는 치유를 받습니다.
        /// </summary>
        /// <param name="inflicter">The Character responsible for the damage. May be null.</param>
        /// <param name="inflicter">피해를 준 캐릭터입니다. null일 수도 있습니다.</param>
        /// <param name="HP">The damage done. Negative value is damage, positive is healing.</param>
        /// <param name="HP">입힌 피해량입니다. 음수 값은 피해를 의미하고, 양수 값은 치유를 의미합니다.</param>
        void ReceiveHP(ServerCharacter inflicter, int HP);

        /// <summary>
        /// The NetworkId of this object.
        /// </summary>
        /// <summary>
        /// 이 객체의 네트워크 ID입니다.
        /// </summary>
        ulong NetworkObjectId { get; }

        /// <summary>
        /// The transform of this object.
        /// </summary>
        /// <summary>
        /// 이 객체의 Transform입니다.
        /// </summary>
        Transform transform { get; }

        [Flags]
        public enum SpecialDamageFlags
        {
            None = 0,
            UnusedFlag = 1 << 0, // does nothing; see comments below
            // 아무런 효과가 없습니다. 아래 주석을 참고하세요.
            StunOnTrample = 1 << 1,
            NotDamagedByPlayers = 1 << 2,

            // The "UnusedFlag" flag does nothing. It exists to work around a Unity editor quirk involving [Flags] enums:
            // if you enable all the flags, Unity stores the value as 0xffffffff (labeled "Everything"), meaning that not
            // only are all the currently-existing flags enabled, but any future flags you added later would also be enabled!
            // This is not future-proof and can cause hard-to-track-down problems, when prefabs magically inherit a new flag
            // you just added. So we have the Unused flag, which should NOT do anything, and shouldn't be selected on prefabs.
            // It's just there so that we can select all the "real" flags and not get it turned into "Everything" in the editor.

            // "UnusedFlag" 플래그는 아무런 효과가 없습니다. 
            // 이 플래그는 Unity 에디터의 [Flags] 열거형과 
            // 관련된 문제를 해결하기 위해 존재합니다.
            // 모든 플래그를 활성화하면 Unity는 이를 0xffffffff("Everything")로 
            // 저장하는데, 이는 현재 존재하는 모든 플래그뿐만 아니라
            // 나중에 추가할 새로운 플래그까지 자동으로 활성화되는 문제가 있습니다.
            // 이는 유지보수에 불리하며, 새로 추가한 플래그가 자동으로 
            // 적용되는 문제를 일으킬 수 있습니다.
            // 따라서 "UnusedFlag"는 실제로 아무런 기능도 하지 않으며, 
            // 프리팹에서 선택되지 않도록 설정해야 합니다.
            // 단지, "실제" 플래그들만 선택할 수 있도록 존재하는 것입니다.
        }
        SpecialDamageFlags GetSpecialDamageFlags();

        /// <summary>
        /// Are we still able to take damage? If we're broken or dead, should return false!
        /// </summary>
        /// <summary>
        /// 현재 피해를 받을 수 있는 상태인지 확인합니다. 객체가 파괴되었거나 사망한 경우 false를 반환해야 합니다.
        /// </summary>
        bool IsDamageable();
    }
}
