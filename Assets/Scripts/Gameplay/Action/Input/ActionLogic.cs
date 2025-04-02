using System;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// List of all Types of Actions. There is a many-to-one mapping of Actions to ActionLogics.
    /// </summary>
    public enum ActionLogic
    {
        Melee,              // 근접 공격: 플레이어가 적을 근접하여 직접 공격
        RangedTargeted,     // 원거리 표적 공격: 플레이어가 특정 표적을 대상으로 원거리 공격
        Chase,                // 추적: 적을 추적하여 근접하거나 특정 행동을 수행
        Revive,              // 부활: 플레이어가 아군을 부활시키는 동작
        LaunchProjectile,    // 발사체 투척: 특정 발사체를 발사하는 동작
        Emote,               // 감정 표현: 플레이어가 감정 표현을 하는 동작 (예: 춤, 손 흔들기 등)
        RangedFXTargeted, // 원거리 효과 타겟팅: 원거리에서 특정 지역 또는 대상에 효과를 적용
        AoE,                // 범위 효과 (Area of Effect): 넓은 범위에 영향을 주는 공격 또는 효과
        Trample,            // 압박 (Trample): 특정 대상을 짓밟거나 물리적으로 압박하는 동작
        ChargedShield,          // 차지된 방어막: 방어막을 차지하거나 강화하는 동작
        Stunned,                // 기절 상태: 기절한 상태로 특정 동작을 수행하는 상태
        Target,                 // 타겟: 특정 타겟에 대한 동작을 정의하는 상태
        ChargedLaunchProjectile, // 차지된 발사체 투척: 발사체를 차지하여 더욱 강력하게 발사하는 동작
        StealthMode,             // 은신 모드: 잠시 동안 은신하여 적에게 보이지 않게 되는 모드
        DashAttack,              // 대시 공격: 빠르게 돌진하여 공격하는 동작
        ImpToss,                // 악마 던지기: 'ImpToss'로 해석되며, 악마를 던지거나 특정 대상에 던지는 동작
        PickUp,               // 아이템 주기: 아이템을 주거나 떨어뜨리는 동작
        Drop                    // 아이템 떨어뜨리기: 아이템을 바닥에 떨어뜨리는 동작
    }
}
