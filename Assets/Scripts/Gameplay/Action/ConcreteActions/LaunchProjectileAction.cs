using System;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Infrastructure;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// Action responsible for creating a projectile object.
    /// </summary>
    /// <summary>
    /// 발사체 객체를 생성하는 액션입니다.
    /// </summary>
    [CreateAssetMenu(menuName = "BossRoom/Actions/Launch Projectile Action")]
    public class LaunchProjectileAction : Action
    {
        private bool m_Launched = false;

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            // snap to face the direction we're firing, and then broadcast the animation, which we do immediately.
            // 발사할 방향을 향하게 하고 애니메이션을 즉시 실행합니다.
            serverCharacter.physicsWrapper.Transform.forward = Data.Direction;

            serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);
            serverCharacter.clientCharacter.ClientPlayActionRpc(Data);
            return true;
        }

        public override void Reset()
        {
            m_Launched = false;
            base.Reset();
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            if (TimeRunning >= Config.ExecTimeSeconds && !m_Launched)
            {
                LaunchProjectile(clientCharacter);
            }

            return true;
        }

        /// <summary>
        /// Looks through the ProjectileInfo list and finds the appropriate one to instantiate.
        /// For the base class, this is always just the first entry with a valid prefab in it!
        /// </summary>
        /// <exception cref="System.Exception">thrown if no Projectiles are valid</exception>
        /// <exception cref="System.Exception">유효한 발사체가 없을 경우 예외가 발생합니다.</exception>
        protected virtual ProjectileInfo GetProjectileInfo()
        {
            foreach (var projectileInfo in Config.Projectiles)
            {
                if (projectileInfo.ProjectilePrefab && projectileInfo.ProjectilePrefab.GetComponent<PhysicsProjectile>())
                    return projectileInfo;
            }
            throw new System.Exception($"Action {name} has no usable Projectiles!");
        }

        /// <summary>
        /// Instantiates and configures the arrow. Repeatedly calling this does nothing.
        /// </summary>
        /// <remarks>
        /// This calls GetProjectilePrefab() to find the prefab it should instantiate.
        /// </remarks>
        /// <remarks>
        /// 이 메서드는 GetProjectilePrefab()을 호출하여 생성할 프리팹을 찾습니다.
        /// </remarks>
        protected void LaunchProjectile(ServerCharacter parent)
        {
            if (!m_Launched)
            {
                m_Launched = true;

                var projectileInfo = GetProjectileInfo();

                NetworkObject no = NetworkObjectPool.Singleton.GetNetworkObject(projectileInfo.ProjectilePrefab, projectileInfo.ProjectilePrefab.transform.position, projectileInfo.ProjectilePrefab.transform.rotation);
                // point the projectile the same way we're facing
                // 발사체가 우리가 바라보는 방향으로 향하도록 설정합니다.
                no.transform.forward = parent.physicsWrapper.Transform.forward;

                //this way, you just need to "place" the arrow by moving it in the prefab, and that will control
                //where it appears next to the player.
                // 이렇게 하면 프리팹에서 화살을 이동시켜서 화살의 위치를 조정할 수 있습니다.
                no.transform.position = parent.physicsWrapper.Transform.localToWorldMatrix.MultiplyPoint(no.transform.position);

                no.GetComponent<PhysicsProjectile>().Initialize(parent.NetworkObjectId, projectileInfo);

                no.Spawn(true);
            }
        }

        public override void End(ServerCharacter serverCharacter)
        {
            // make sure this happens.
            // 이 작업이 반드시 실행되도록 합니다.
            LaunchProjectile(serverCharacter);
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
