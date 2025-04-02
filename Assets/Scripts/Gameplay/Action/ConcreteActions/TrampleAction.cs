using System;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// This represents a "charge-across-the-screen" attack. The character deals damage to every enemy hit.
    /// </summary>
    /// <remarks>
    /// It's called "Trample" instead of "Charge" because we already use the word "charge"
    /// to describe "charging up" an attack.
    /// </remarks>
    /// <summary>
    /// 이것은 "화면을 가로질러 돌진하는" 공격을 나타냅니다. 캐릭터는 맞은 모든 적에게 피해를 입힙니다.
    /// </summary>
    /// <remarks>
    /// "Charge" 대신 "Trample"이라고 불리는 이유는 이미 "charge"라는 단어를 사용하여 공격을 "충전"하는 것을 설명하기 때문입니다.
    /// </remarks>
    [CreateAssetMenu(menuName = "BossRoom/Actions/Trample Action")]
    public partial class TrampleAction : Action
    {
        public StunnedAction StunnedActionPrototype;

        /// <summary>
        /// This is an internal indicator of which stage of the Action we're in.
        /// </summary>
        /// <summary>
        /// 이것은 액션이 어떤 단계에 있는지를 나타내는 내부 지표입니다.
        /// </summary>
        private enum ActionStage
        {
            Windup,     // performing animations prior to actually moving
            Charging,   // running across the screen and hitting characters
            Complete,   // ending action
        }

        /// <summary>
        /// When we begin our charge-attack, anyone within this range is treated as having already been touching us.
        /// </summary>
        /// <summary>
        /// 충전 공격을 시작할 때 이 범위 내에 있는 사람은 이미 우리와 접촉한 것으로 처리됩니다.
        /// </summary>
        private const float k_PhysicalTouchDistance = 1;

        /// <summary>
        /// Our ActionStage, as of last Update
        /// </summary>
        /// <summary>
        /// 마지막 업데이트 시점의 액션 단계
        /// </summary>
        private ActionStage m_PreviousStage;

        /// <summary>
        /// Keeps track of which Colliders we've already hit, so that our attack doesn't hit the same character twice.
        /// </summary>
        /// <summary>
        /// 이미 충돌한 Collider를 추적하여, 우리의 공격이 같은 캐릭터를 두 번 이상 맞히지 않도록 합니다.
        /// </summary>
        private HashSet<Collider> m_CollidedAlready = new HashSet<Collider>();

        /// <summary>
        /// Set to true in the special-case scenario where we are stunned by one of the characters we tried to trample
        /// </summary>
        /// <summary>
        /// 우리가 트램플 하려던 캐릭터에게 기절된 특수한 경우에 true로 설정됩니다.
        /// </summary>
        private bool m_WasStunned;

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            m_PreviousStage = ActionStage.Windup;

            if (m_Data.TargetIds != null && m_Data.TargetIds.Length > 0)
            {
                NetworkObject initialTarget = NetworkManager.Singleton.SpawnManager.SpawnedObjects[m_Data.TargetIds[0]];
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
                    // 우리의 타겟을 향하도록 맞추세요! 이 방향으로 공격을 하게 됩니다.
                    serverCharacter.physicsWrapper.Transform.LookAt(lookAtPosition);
                }
            }

            // reset our "stop" trigger (in case the previous run of the trample action was aborted due to e.g. being stunned)
            if (!string.IsNullOrEmpty(Config.Anim2))
            {
                serverCharacter.serverAnimationHandler.NetworkAnimator.ResetTrigger(Config.Anim2);
            }
            // start the animation sequence!
            if (!string.IsNullOrEmpty(Config.Anim))
            {
                serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);
            }

            serverCharacter.clientCharacter.ClientPlayActionRpc(Data);
            return true;
        }

        public override void Reset()
        {
            base.Reset();
            m_PreviousStage = default;
            m_CollidedAlready.Clear();
            m_SpawnedGraphics = null;
            m_WasStunned = false;
        }

        private ActionStage GetCurrentStage()
        {
            float timeSoFar = Time.time - TimeStarted;
            if (timeSoFar < Config.ExecTimeSeconds)
            {
                return ActionStage.Windup;
            }
            if (timeSoFar < Config.DurationSeconds)
            {
                return ActionStage.Charging;
            }
            return ActionStage.Complete;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            ActionStage newState = GetCurrentStage();
            if (newState != m_PreviousStage && newState == ActionStage.Charging)
            {
                // we've just started to charge across the screen! Anyone currently touching us gets hit
                // 우리는 이제 화면을 가로지르는 돌진을 시작했습니다! 현재 우리와 접촉한 모든 적이 맞습니다.
                SimulateCollisionWithNearbyFoes(clientCharacter);
                clientCharacter.Movement.StartForwardCharge(Config.MoveSpeed, Config.DurationSeconds - Config.ExecTimeSeconds);
            }

            m_PreviousStage = newState;
            return newState != ActionStage.Complete && !m_WasStunned;
        }

        /// <summary>
        /// We've crashed into a victim! This function determines what happens to them... and to us!
        /// It's possible for us to be stunned by our victim if they have a special power that allows that.
        /// This function checks for that special case; if we become stunned, the victim is entirely unharmed,
        /// and further collisions with other victims will also have no effect.
        /// </summary>
        /// <param name="victim">The character we've collided with</param>
        /// <summary>
        /// 우리는 희생자와 충돌했습니다! 이 함수는 그들에게 일어날 일을 결정합니다... 그리고 우리에게 일어날 일도 결정합니다!
        /// 희생자가 특별한 능력으로 우리를 기절시킬 수 있는 경우가 있을 수 있습니다.
        /// 이 함수는 그런 특수한 경우를 체크합니다. 우리가 기절하면 희생자는 전혀 다치지 않으며,
        /// 다른 희생자와의 추가 충돌도 효과가 없습니다.
        /// </summary>
        private void CollideWithVictim(ServerCharacter parent, ServerCharacter victim)
        {
            if (victim == parent)
            {
                // can't collide with ourselves!
                // 우리는 자신과 충돌할 수 없습니다!
                return;
            }

            if (m_WasStunned)
            {
                // someone already stunned us, so no further damage can happen
                // 이미 누군가가 우리를 기절시켰습니다, 그래서 더 이상의 피해는 일어나지 않습니다
                return;
            }

            // if we collide with allies, we don't want to hurt them (but we do knock them back, see below)
            if (parent.IsNpc != victim.IsNpc)
            {
                // first see if this victim has the special ability to stun us!
                float chanceToStun = victim.GetBuffedValue(BuffableValue.ChanceToStunTramplers);
                if (chanceToStun > 0 && Random.Range(0, 1) < chanceToStun)
                {
                    // we're stunned! No collision behavior for the victim. Stun ourselves and abort.
                    // 우리는 기절했습니다! 희생자는 더 이상 충돌하지 않습니다. 자신을 기절시키고 중단합니다.
                    StunSelf(parent);
                    return;
                }

                // We deal a certain amount of damage to our "initial" target and a different amount to all other victims.
                // 우리는 "초기" 타겟에게 일정량의 피해를 주고, 다른 모든 희생자에게는 다른 양의 피해를 줍니다.
                int damage;
                if (m_Data.TargetIds != null && m_Data.TargetIds.Length > 0 && m_Data.TargetIds[0] == victim.NetworkObjectId)
                {
                    damage = Config.Amount;
                }
                else
                {
                    damage = Config.SplashDamage;
                }

                if (victim.gameObject.TryGetComponent(out IDamageable damageable))
                {
                    damageable.ReceiveHP(parent, -damage);
                }
            }

            var victimMovement = victim.Movement;
            victimMovement.StartKnockback(parent.physicsWrapper.Transform.position, Config.KnockbackSpeed, Config.KnockbackDuration);
        }

        // called by owning class when parent's Collider collides with stuff
        public override void CollisionEntered(ServerCharacter serverCharacter, Collision collision)
        {
            // we only detect other possible victims when we start charging
            if (GetCurrentStage() != ActionStage.Charging)
                return;

            Collide(serverCharacter, collision.collider);
        }

        // here we handle colliding with anything (whether a victim or not)
        private void Collide(ServerCharacter parent, Collider collider)
        {
            if (m_CollidedAlready.Contains(collider))
                return; // already hit them! 

            m_CollidedAlready.Add(collider);

            var victim = collider.gameObject.GetComponentInParent<ServerCharacter>();
            if (victim)
            {
                CollideWithVictim(parent, victim);
            }
            else if (!m_WasStunned)
            {
                // they aren't a living, breathing victim, but they might still be destructible...
                // 그들은 살아있는 희생자가 아니지만 여전히 파괴 가능할 수 있습니다...
                var damageable = collider.gameObject.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.ReceiveHP(parent, -Config.SplashDamage);

                    // lastly, a special case: if the trampler runs into certain breakables, they are stunned!
                    // 마지막으로, 특별한 경우: 만약 트램플러가 특정 파괴 가능한 물체에 충돌하면, 그들은 기절합니다!
                    if ((damageable.GetSpecialDamageFlags() & IDamageable.SpecialDamageFlags.StunOnTrample) == IDamageable.SpecialDamageFlags.StunOnTrample)
                    {
                        StunSelf(parent);
                    }
                }
            }
        }

        private void SimulateCollisionWithNearbyFoes(ServerCharacter parent)
        {
            // 충전이 시작되면 이미 충돌한 것들에 대해 OnCollisionEnter() 호출이 이루어지지 않기 때문에
            // 우리는 화면을 가로질러 충전하는 동안 이미 접촉한 객체를 확인하고 그것을 충돌로 처리합니다.
            RaycastHit[] results;
            int numResults = ActionUtils.DetectNearbyEntities(true, true, parent.physicsWrapper.DamageCollider, k_PhysicalTouchDistance, out results);
            for (int i = 0; i < numResults; i++)
            {
                Collide(parent, results[i].collider);
            }
        }

        private void StunSelf(ServerCharacter parent)
        {
            if (!m_WasStunned)
            {
                parent.Movement.CancelMove();
                parent.clientCharacter.ClientCancelAllActionsRpc();
            }
            m_WasStunned = true;
        }

        public override bool ChainIntoNewAction(ref ActionRequestData newAction)
        {
            if (m_WasStunned)
            {
                newAction = ActionRequestData.Create(StunnedActionPrototype);
                newAction.ShouldQueue = false;
                return true;
            }
            return false;
        }

        public override void Cancel(ServerCharacter serverCharacter)
        {
            if (!string.IsNullOrEmpty(Config.Anim2))
            {
                serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim2);
            }
        }
    }
}
