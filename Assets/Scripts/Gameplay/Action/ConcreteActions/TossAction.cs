using System;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Infrastructure;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// Action responsible for creating a physics-based thrown object.
    /// </summary>
    /// <summary>
    /// 물리 기반의 던져진 객체를 생성하는 액션입니다.
    /// </summary>
    [CreateAssetMenu(menuName = "BossRoom/Actions/Toss Action")]
    public class TossAction : Action
    {
        bool m_Launched;

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            // snap to face the direction we're firing
            // 우리가 발사하는 방향을 바라보도록 조정
            if (m_Data.TargetIds != null && m_Data.TargetIds.Length > 0)
            {
                var initialTarget = NetworkManager.Singleton.SpawnManager.SpawnedObjects[m_Data.TargetIds[0]];
                if (initialTarget)
                {
                    Vector3 lookAtPosition;
                    if (PhysicsWrapper.TryGetPhysicsWrapper(initialTarget.NetworkObjectId, out var physicsWrapper))
                    {
                        lookAtPosition = physicsWrapper.Transform.position;
                    }
                    else
                    {
                        lookAtPosition = initialTarget.transform.position;
                    }

                    // snap to face our target! This is the direction we'll attack in
                    // 목표를 향해 바라보도록 조정! 이것이 우리가 공격할 방향입니다.
                    serverCharacter.physicsWrapper.Transform.LookAt(lookAtPosition);
                }
            }

            serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);
            serverCharacter.clientCharacter.ClientPlayActionRpc(Data);
            return true;
        }

        public override void Reset()
        {
            base.Reset();
            m_Launched = false;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            if (TimeRunning >= Config.ExecTimeSeconds && !m_Launched)
            {
                Throw(clientCharacter);
            }

            return true;
        }

        /// <summary>
        /// Looks through the ProjectileInfo list and finds the appropriate one to instantiate.
        /// For the base class, this is always just the first entry with a valid prefab in it!
        /// </summary>
        /// <exception cref="System.Exception">thrown if no Projectiles are valid</exception>
        /// <exception cref="System.Exception">유효한 발사체가 없을 경우 예외가 발생합니다.</exception>
        ProjectileInfo GetProjectileInfo()
        {
            foreach (var projectileInfo in Config.Projectiles)
            {
                if (projectileInfo.ProjectilePrefab)
                {
                    return projectileInfo;
                }
            }
            throw new System.Exception($"Action {this.name} has no usable Projectiles!");
        }

        /// <summary>
        /// Instantiates and configures the thrown object. Repeatedly calling this does nothing.
        /// </summary>
        /// <remarks>
        /// This calls GetProjectilePrefab() to find the prefab it should instantiate.
        /// </remarks>
        /// <remarks>
        /// 이 메서드는 GetProjectilePrefab()을 호출하여 생성할 프리팹을 찾습니다.
        /// </remarks>
        void Throw(ServerCharacter parent)
        {
            if (!m_Launched)
            {
                m_Launched = true;

                var projectileInfo = GetProjectileInfo();

                var no = NetworkObjectPool.Singleton.GetNetworkObject(projectileInfo.ProjectilePrefab, projectileInfo.ProjectilePrefab.transform.position, projectileInfo.ProjectilePrefab.transform.rotation);

                var networkObjectTransform = no.transform;

                // point the thrown object the same way we're facing
                // 던져진 객체가 우리가 바라보는 방향을 향하도록 설정
                networkObjectTransform.forward = parent.physicsWrapper.Transform.forward;

                networkObjectTransform.position = parent.physicsWrapper.Transform.localToWorldMatrix.MultiplyPoint(networkObjectTransform.position) +
                    networkObjectTransform.forward + (Vector3.up * 2f);

                no.Spawn(true);

                // important to add a force AFTER a NetworkObject is spawned, since IsKinematic is enabled on the
                // Rigidbody component after it is spawned
                // NetworkObject가 생성된 후에 힘을 추가하는 것이 중요합니다. Rigidbody 컴포넌트에서 IsKinematic이 활성화되기 때문입니다.
                var tossedItemRigidbody = no.GetComponent<Rigidbody>();

                tossedItemRigidbody.AddForce((networkObjectTransform.forward * 80f) + (networkObjectTransform.up * 150f), ForceMode.Impulse);
                tossedItemRigidbody.AddTorque((networkObjectTransform.forward * Random.Range(-15f, 15f)) + (networkObjectTransform.up * Random.Range(-15f, 15f)), ForceMode.Impulse);
            }
        }
    }
}
