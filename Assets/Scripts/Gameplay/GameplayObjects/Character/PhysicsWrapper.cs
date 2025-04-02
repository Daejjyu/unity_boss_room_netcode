using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// Wrapper class for direct references to components relevant to physics.
    /// Each instance of a PhysicsWrapper is registered to a static dictionary, indexed by the NetworkObject's ID.
    /// </summary>
    /// <summary>
    /// 물리와 관련된 컴포넌트에 대한 직접적인 참조를 제공하는 래퍼 클래스입니다.  
    /// 각 PhysicsWrapper 인스턴스는 NetworkObject의 ID를 키로 하는 정적 딕셔너리에 등록됩니다.
    /// </summary>
    /// <remarks>
    /// The root GameObject of PCs & NPCs is not the object which will move through the world, so other classes will
    /// need a quick reference to a PC's/NPC's in-game position.
    /// </remarks>
    /// <remarks>
    /// PC 및 NPC의 루트 GameObject는 실제로 월드를 이동하는 객체가 아니므로,  
    /// 다른 클래스에서 PC/NPC의 게임 내 위치를 빠르게 참조할 필요가 있습니다.
    /// </remarks>
    public class PhysicsWrapper : NetworkBehaviour
    {
        static Dictionary<ulong, PhysicsWrapper> m_PhysicsWrappers = new Dictionary<ulong, PhysicsWrapper>();

        [SerializeField]
        Transform m_Transform;

        public Transform Transform => m_Transform;

        [SerializeField]
        Collider m_DamageCollider;

        public Collider DamageCollider => m_DamageCollider;

        ulong m_NetworkObjectID;

        public override void OnNetworkSpawn()
        {
            m_PhysicsWrappers.Add(NetworkObjectId, this);

            m_NetworkObjectID = NetworkObjectId;
        }

        public override void OnNetworkDespawn()
        {
            RemovePhysicsWrapper();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            RemovePhysicsWrapper();
        }

        void RemovePhysicsWrapper()
        {
            m_PhysicsWrappers.Remove(m_NetworkObjectID);
        }

        public static bool TryGetPhysicsWrapper(ulong networkObjectID, out PhysicsWrapper physicsWrapper)
        {
            return m_PhysicsWrappers.TryGetValue(networkObjectID, out physicsWrapper);
        }
    }
}
