using System;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// Action for dropping "Heavy" items.
    /// </summary>
    /// <summary>
    /// "Heavy" 아이템을 떨어뜨리는 액션입니다.
    /// </summary>
    [CreateAssetMenu(menuName = "BossRoom/Actions/Drop Action")]
    public class DropAction : Action
    {
        float m_ActionStartTime;

        NetworkObject m_HeldNetworkObject;

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            m_ActionStartTime = Time.time;

            // play animation of dropping a heavy object, if one is already held
            // 이미 아이템을 들고 있다면, 무거운 물건을 떨어뜨리는 애니메이션을 재생합니다.
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    serverCharacter.HeldNetworkObject.Value, out var heldObject))
            {
                m_HeldNetworkObject = heldObject;

                Data.TargetIds = null;

                if (!string.IsNullOrEmpty(Config.Anim))
                {
                    serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);
                }
            }

            return true;
        }

        public override void Reset()
        {
            base.Reset();
            m_ActionStartTime = 0;
            m_HeldNetworkObject = null;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            if (Time.time > m_ActionStartTime + Config.ExecTimeSeconds)
            {
                // drop the pot in space
                // 공중에 pot을 떨어뜨립니다.
                m_HeldNetworkObject.transform.SetParent(null);
                clientCharacter.HeldNetworkObject.Value = 0;

                return ActionConclusion.Stop;
            }

            return ActionConclusion.Continue;
        }
    }
}
