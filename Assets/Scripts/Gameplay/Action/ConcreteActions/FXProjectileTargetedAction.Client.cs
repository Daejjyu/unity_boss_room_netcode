using System;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.BossRoom.Gameplay.Actions
{
    public partial class FXProjectileTargetedAction
    {
        // have we actually played an impact?
        // 실제로 충격을 재생했는지 여부
        private bool m_ImpactPlayed;
        // the time the FX projectile spends in the air
        // FX 투사체가 공중에 있는 시간
        private float m_ProjectileDuration;
        // the currently-live projectile. (Note that the projectile will normally destroy itself! We only care in case someone calls Cancel() on us)
        // 현재 살아있는 투사체. (투사체는 보통 자신을 파괴합니다! 우리가 취소(Cancel) 요청을 받는 경우에만 신경 씁니다)
        private FXProjectile m_Projectile;
        // the enemy we're aiming at
        // 우리가 조준하고 있는 적
        private NetworkObject m_Target;
        Transform m_TargetTransform;

        public override bool OnStartClient(ClientCharacter clientCharacter)
        {
            base.OnStartClient(clientCharacter);
            m_Target = GetTarget(clientCharacter);

            if (m_Target && PhysicsWrapper.TryGetPhysicsWrapper(m_Target.NetworkObjectId, out var physicsWrapper))
            {
                m_TargetTransform = physicsWrapper.Transform;
            }

            if (Config.Projectiles.Length < 1 || Config.Projectiles[0].ProjectilePrefab == null)
                throw new System.Exception($"Action {name} has no valid ProjectileInfo!");

            return true;
        }

        public override bool OnUpdateClient(ClientCharacter clientCharacter)
        {
            if (TimeRunning >= Config.ExecTimeSeconds && m_Projectile == null)
            {
                // figure out how long the pretend-projectile will be flying to the target
                // 가상의 투사체가 목표 지점까지 비행하는 시간이 얼마나 걸릴지 계산
                var targetPos = m_TargetTransform ? m_TargetTransform.position : Data.Position;
                var initialDistance = Vector3.Distance(targetPos, clientCharacter.transform.position);
                m_ProjectileDuration = initialDistance / Config.Projectiles[0].Speed_m_s;

                // create the projectile. It will control itself from here on out
                // 투사체를 생성합니다. 이제부터 투사체는 스스로 제어합니다.
                m_Projectile = SpawnAndInitializeProjectile(clientCharacter);
            }

            // we keep going until the projectile's duration ends
            // 투사체의 지속 시간이 끝날 때까지 계속 진행
            return TimeRunning <= m_ProjectileDuration + Config.ExecTimeSeconds;
        }

        public override void CancelClient(ClientCharacter clientCharacter)
        {
            if (m_Projectile)
            {
                // we aborted post-projectile-launch (somehow)! Tell the graphics! (It will destroy itself, possibly after playing some more FX)
                // 투사체 발사 후 취소되었습니다 (어떻게든)! 그래픽에 알려주세요! (그래픽은 자신을 파괴할 것입니다, 아마도 추가적인 FX를 재생한 후)
                m_Projectile.Cancel();
            }
        }

        public override void EndClient(ClientCharacter clientCharacter)
        {
            PlayHitReact();
        }

        void PlayHitReact()
        {
            if (m_ImpactPlayed)
                return;
            m_ImpactPlayed = true;

            if (NetworkManager.Singleton.IsServer)
            {
                return;
            }

            if (m_Target && m_Target.TryGetComponent(out ServerCharacter clientCharacter) && clientCharacter.clientCharacter != null)
            {
                var hitReact = !string.IsNullOrEmpty(Config.ReactAnim) ? Config.ReactAnim : k_DefaultHitReact;
                clientCharacter.clientCharacter.OurAnimator.SetTrigger(hitReact);
            }
        }

        NetworkObject GetTarget(ClientCharacter parent)
        {
            if (Data.TargetIds == null || Data.TargetIds.Length == 0)
            {
                return null;
            }

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(Data.TargetIds[0], out NetworkObject targetObject) && targetObject != null)
            {
                // make sure this isn't a friend (or if it is, make sure this is a friendly-fire action)
                // 이것이 아군이 아닌지 확인 (혹시 아군이라면, 친선 사격 액션인지 확인)
                var targetable = targetObject.GetComponent<ITargetable>();
                if (targetable != null && targetable.IsNpc == (Config.IsFriendly ^ parent.serverCharacter.IsNpc))
                {
                    // not a valid target
                    // 유효한 목표가 아닙니다
                    return null;
                }

                return targetObject;
            }
            else
            {
                // target could have legitimately disappeared in the time it took to queue this action... but that's pretty unlikely, so we'll log about it to ease debugging
                // 목표가 이 액션이 큐에 추가되는 동안 실제로 사라졌을 수 있습니다... 하지만 이는 매우 드물기 때문에 디버깅을 쉽게 하기 위해 로그를 남깁니다.
                Debug.Log($"FXProjectileTargetedActionFX was targeted at ID {Data.TargetIds[0]}, but that target can't be found in spawned object list! (May have just been deleted?)");
                return null;
            }
        }

        FXProjectile SpawnAndInitializeProjectile(ClientCharacter parent)
        {
            var projectileGO = Object.Instantiate(Config.Projectiles[0].ProjectilePrefab, parent.transform.position, parent.transform.rotation, null);

            var projectile = projectileGO.GetComponent<FXProjectile>();
            if (!projectile)
            {
                throw new System.Exception($"FXProjectileTargetedAction tried to spawn projectile {projectileGO.name}, as dictated for action {name}, but the object doesn't have a FXProjectile component!");
            }

            // now that we have our projectile, initialize it so it'll fly at the target appropriately
            // 이제 투사체를 얻었으므로, 그것이 목표를 향해 적절히 비행하도록 초기화합니다.
            projectile.Initialize(parent.transform.position, m_TargetTransform, Data.Position, m_ProjectileDuration);
            return projectile;
        }

        public override void AnticipateActionClient(ClientCharacter clientCharacter)
        {
            base.AnticipateActionClient(clientCharacter);

            // see if this is going to be a "miss" because the player tried to click through a wall. If so,
            // we change our data in the same way that the server will (changing our target point to the spot on the wall)
            // 플레이어가 벽을 통해 클릭하려 했기 때문에 "빗맞기"가 발생할지 확인합니다. 그렇다면, 우리는 서버와 동일한 방식으로 데이터를 변경합니다 (목표 지점을 벽에 있는 지점으로 변경)
            Vector3 targetSpot = Data.Position;
            if (Data.TargetIds != null && Data.TargetIds.Length > 0)
            {
                var targetObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[Data.TargetIds[0]];
                if (targetObj)
                {
                    targetSpot = targetObj.transform.position;
                }
            }

            if (!ActionUtils.HasLineOfSight(clientCharacter.transform.position, targetSpot, out Vector3 collidePos))
            {
                // we do not have line of sight to the target point. So our target instead becomes the obstruction point
                // 우리는 목표 지점에 대한 시야가 없습니다. 그래서 우리의 목표는 대신 장애물 지점이 됩니다.
                Data.TargetIds = null;
                Data.Position = collidePos;
            }
        }
    }
}
