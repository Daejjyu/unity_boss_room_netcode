using System;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// Action that plays while a character is Stunned. The character does nothing... just sits there.
    /// 
    /// If desired, we can make the character take extra damage from attacks while stunned!
    /// The 'Amount' field of our ActionDescription is used as a multiplier on damage suffered.
    /// (Set it to 1 if you don't want to take more damage while stunned... set it to 2 to take double damage,
    /// or 0.5 to take half damage, etc.)
    /// </summary>
    /// <summary>
    /// 캐릭터가 스턴 상태일 때 실행되는 액션입니다. 캐릭터는 아무 것도 하지 않고 앉아만 있습니다.
    /// 
    /// 원한다면, 스턴 상태에서 공격을 받을 때 추가 피해를 받게 만들 수 있습니다!
    /// 'Amount' 필드는 받은 피해에 대한 배율로 사용됩니다.
    /// (스턴 상태에서 더 많은 피해를 원하지 않으면 1로 설정하고, 2로 설정하면 두 배의 피해를 받고,
    /// 0.5로 설정하면 반으로 받는 방식입니다, 등.)
    /// </summary>
    [CreateAssetMenu(menuName = "BossRoom/Actions/Stunned Action")]
    public class StunnedAction : Action
    {
        public override bool OnStart(ServerCharacter serverCharacter)
        {
            serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);
            return true;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            return true;
        }

        public override void BuffValue(BuffableValue buffType, ref float buffedValue)
        {
            if (buffType == BuffableValue.PercentDamageReceived)
            {
                buffedValue *= Config.Amount;
            }
        }

        public override void Cancel(ServerCharacter serverCharacter)
        {
            if (!string.IsNullOrEmpty(Config.Anim2))
            {
                serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim2);
            }
        }

        public override bool OnUpdateClient(ClientCharacter clientCharacter)
        {
            return ActionConclusion.Continue;
        }
    }
}