// <summary>
// A version of LaunchProjectileAction that can be "powered up" by holding down the attack key.
// </summary>
// <summary>
// 공격 키를 눌러 충전할 수 있는 LaunchProjectileAction의 버전입니다.
// </summary>
// <remarks>
// The player can hold down the button for this ability to "charge it up" and make it more effective. Once it's been
// charging for Description.ExecTimeSeconds, it reaches maximum charge. If the player is attacked by an enemy, that
// also immediately stops the charge-up, but also cancels firing.
//
// Once charge-up stops, the projectile is fired (unless it was stopped due to being attacked.)
//
// The projectile can have various stats depending on how "charged up" the attack was. The ActionDescription's
// Projectiles array should contain each tier of projectile, sorted from weakest to strongest.
// </remarks>
// <remarks>
// 플레이어는 이 능력을 충전하기 위해 버튼을 눌러 이를 더욱 효과적으로 만들 수 있습니다. 충전이
// Description.ExecTimeSeconds 동안 지속되면 최대 충전 상태에 도달합니다. 플레이어가 적에게 공격당하면,
// 충전이 즉시 중단되며, 발사도 취소됩니다.
//
// 충전이 중단되면, 투사체가 발사됩니다 (단, 공격을 받아 중단된 경우는 제외).
//
// 투사체는 공격이 얼마나 "충전"되었는지에 따라 다양한 능력치를 가질 수 있습니다. ActionDescription의
// Projectiles 배열에는 각 레벨의 투사체가 포함되어 있으며, 이는 가장 약한 것부터 가장 강한 것까지 정렬되어야 합니다.
// </remarks>

using System;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// A version of LaunchProjectileAction that can be "powered up" by holding down the attack key.
    /// </summary>
    /// <summary>
    /// 공격 키를 눌러 충전할 수 있는 LaunchProjectileAction의 버전입니다.
    /// </summary>
    [CreateAssetMenu(menuName = "BossRoom/Actions/Charged Launch Projectile Action")]
    public partial class ChargedLaunchProjectileAction : LaunchProjectileAction
    {
        /// <summary>
        /// Set once we've stopped charging up, for any reason:
        /// - the player has let go of the button,
        /// - we were attacked,
        /// - or the maximum charge was reached.
        /// </summary>
        /// <summary>
        /// 충전을 중단한 이유가 무엇이든지 간에 설정됩니다:
        /// - 플레이어가 버튼을 놓았을 때,
        /// - 공격을 받았을 때,
        /// - 또는 최대 충전이 도달했을 때.
        /// </summary>
        private float m_StoppedChargingUpTime = 0;

        /// <summary>
        /// Were we attacked while charging up? (If so, we won't actually fire.)
        /// </summary>
        /// <summary>
        /// 충전 중에 공격을 받았나요? (그렇다면 실제로 발사되지 않습니다.)
        /// </summary>
        private bool m_HitByAttack = false;

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            // if we have an explicit target, make sure we're aimed at them.
            // (But if the player just clicked on an attack button, there won't be an explicit target, so we should stay facing however we're facing.)
            // 명확한 타겟이 있다면, 그 타겟을 향해 바라보도록 합니다.
            // (하지만 플레이어가 공격 버튼을 클릭했다면 명확한 타겟이 없으므로 현재 바라보고 있는 방향을 유지합니다.)
            if (m_Data.TargetIds != null && m_Data.TargetIds.Length > 0)
            {
                NetworkObject initialTarget = NetworkManager.Singleton.SpawnManager.SpawnedObjects[m_Data.TargetIds[0]];
                if (initialTarget)
                {
                    // face our target
                    serverCharacter.physicsWrapper.Transform.LookAt(initialTarget.transform.position);
                }
            }

            serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);

            // start the "charging up" ActionFX
            serverCharacter.clientCharacter.ClientPlayActionRpc(Data);

            // sanity-check our data a bit
            Debug.Assert(Config.Projectiles.Length > 1, $"Action {name} has {Config.Projectiles.Length} Projectiles. Expected at least 2!");
            foreach (var projectileInfo in Config.Projectiles)
            {
                Debug.Assert(projectileInfo.ProjectilePrefab, $"Action {name}: one of the Projectiles is missing its prefab!");
                Debug.Assert(projectileInfo.Range > 0, $"Action {name}: one of the Projectiles has invalid Range!");
                Debug.Assert(projectileInfo.Speed_m_s > 0, $"Action {name}: one of the Projectiles has invalid Speed_m_s!");
            }
            return true;
        }

        public override void Reset()
        {
            base.Reset();
            m_ChargeEnded = false;
            m_StoppedChargingUpTime = 0;
            m_HitByAttack = false;
            m_Graphics.Clear();
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            if (m_StoppedChargingUpTime == 0 && GetPercentChargedUp() >= 1)
            {
                // we haven't explicitly stopped charging up... but we've reached max charge, so that implicitly stops us
                // 충전을 명시적으로 중단하지 않았지만, 최대 충전 상태에 도달했으므로 사실상 충전이 중단됩니다.
                StopChargingUp(clientCharacter);
            }

            // we end as soon as we've stopped charging up (and have fired the projectile)
            return m_StoppedChargingUpTime == 0;
        }

        public override void OnGameplayActivity(ServerCharacter serverCharacter, GameplayActivity activityType)
        {
            if (activityType == GameplayActivity.AttackedByEnemy)
            {
                // if we get attacked while charging up, we don't actually get to shoot!
                // 충전 중에 공격을 받으면 실제로 발사되지 않습니다!
                m_HitByAttack = true;
                StopChargingUp(serverCharacter);
            }
            else if (activityType == GameplayActivity.StoppedChargingUp)
            {
                StopChargingUp(serverCharacter);
            }
        }

        public override void Cancel(ServerCharacter serverCharacter)
        {
            StopChargingUp(serverCharacter);
        }

        public override void End(ServerCharacter serverCharacter)
        {
            StopChargingUp(serverCharacter);
        }

        private void StopChargingUp(ServerCharacter parent)
        {
            if (m_StoppedChargingUpTime == 0)
            {
                m_StoppedChargingUpTime = Time.time;

                if (!string.IsNullOrEmpty(Config.Anim2))
                {
                    parent.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim2);
                }

                parent.clientCharacter.ClientStopChargingUpRpc(GetPercentChargedUp());
                if (!m_HitByAttack)
                {
                    LaunchProjectile(parent);
                }
            }
        }

        private float GetPercentChargedUp()
        {
            return ActionUtils.GetPercentChargedUp(m_StoppedChargingUpTime, TimeRunning, TimeStarted, Config.ExecTimeSeconds);
        }

        /// <summary>
        /// Overridden from base-class to choose a different projectile depending on how "charged up" we got.
        /// To do this, we assume that the Projectiles list is ordered from weakest to strongest.
        /// </summary>
        /// <remarks>
        /// To reward players that fully charge-up their attack, we only return the strongest projectile when the
        /// charge-up is at 100%. The other tiers of projectile are used for lesser charge-up amounts.
        /// </remarks>
        /// <returns>the projectile that should be used</returns>
        /// <returns>사용할 투사체를 반환합니다.</returns>
        protected override ProjectileInfo GetProjectileInfo()
        {
            if (Config.Projectiles.Length == 0) // uh oh, this is bad data
                throw new System.Exception($"Action {name} has no Projectiles!");

            // choose which prefab to use based on how charged-up we got.
            // Note how we cast the result to an int, which implicitly rounds down.
            // Thus, only a 100% maxed charge can return the most powerful prefab.
            // 어떤 프리팹을 사용할지 선택합니다. 결과를 int로 변환하는 점에 유의하세요. 이 과정은 암시적으로 내림을 수행합니다.
            // 따라서 100% 최대 충전 상태에서만 가장 강력한 프리팹이 반환됩니다.
            int projectileIdx = (int)(GetPercentChargedUp() * (Config.Projectiles.Length - 1));

            return Config.Projectiles[projectileIdx];
        }
    }
}
