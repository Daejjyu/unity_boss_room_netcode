using System;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
    /// <summary>
    /// Describes how the character's movement should be animated: as standing idle, running normally,
    /// magically slowed, sped up, etc. (Not all statuses are currently used by game content,
    /// but they are set up to be displayed correctly for future use.)
    /// </summary>
    /// <summary>
    /// 캐릭터의 이동이 어떻게 애니메이션화되어야 하는지를 설명합니다.  
    /// (예: 가만히 서 있기, 정상적인 달리기, 마법적으로 느려지거나 빨라지는 경우 등)  
    /// 현재 게임 콘텐츠에서 모든 상태가 사용되는 것은 아니지만,  
    /// 향후 올바르게 표시될 수 있도록 설정되어 있습니다.
    /// </summary>
    [Serializable]
    public enum MovementStatus
    {
        Idle,         // 이동하지 않는 상태  
        Normal,       // 캐릭터가 정상적으로 이동하는 상태  
        Uncontrolled, // 넉백(knockback) 등으로 인해 캐릭터가 강제로 이동하는 상태 (사용자가 제어 불가능)  
        Slowed,       // 마법 등의 영향으로 이동이 느려진 상태  
        Hasted,       // 마법 등의 영향으로 이동이 빨라진 상태  
        Walking,      // 일반적인 달리기가 아닌 "걷기" 상태로 표시되어야 하는 경우 (예: 컷씬)  
    }
}
