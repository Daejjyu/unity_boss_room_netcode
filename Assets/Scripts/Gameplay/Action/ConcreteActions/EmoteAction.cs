using System;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    [CreateAssetMenu(menuName = "BossRoom/Actions/Emote Action")]
    public class EmoteAction : Action
    {
        public override bool OnStart(ServerCharacter serverCharacter)
        {
            serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);
            return false;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            // since we return false at Start(), this method should not execute
            // Start()에서 false를 반환하므로 이 메서드는 실행되지 않아야 합니다.
            throw new InvalidOperationException("No logic defined.");
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
