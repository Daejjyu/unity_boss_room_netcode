using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// Component that spawns a PlayerAvatar's Avatar. It does this in two places:
    /// 1) either inside OnNetworkSpawn() or
    /// 2) inside NetworkAnimator's OnSynchronize method.
    /// The latter is necessary for clients receiving initial synchronizing data, where the Animator needs to be present
    /// and bound (Animator.Bind()) *before* the incoming animation data is applied.
    /// </summary>
    /// <summary>
    /// PlayerAvatar의 아바타를 생성하는 컴포넌트입니다. 두 가지 경우에 수행됩니다:
    /// 1) OnNetworkSpawn() 내부에서 실행되거나
    /// 2) NetworkAnimator의 OnSynchronize 메서드 내부에서 실행됩니다.
    /// 후자의 경우, 클라이언트가 초기 동기화 데이터를 받을 때 Animator가 존재하고 
    /// (Animator.Bind())를 통해 바인딩된 상태여야 합니다. 이는 들어오는 애니메이션 데이터가 적용되기 *이전*에 필요합니다.
    /// </summary>
    public class ClientPlayerAvatarNetworkAnimator : NetworkAnimator
    {
        [SerializeField]
        NetworkAvatarGuidState m_NetworkAvatarGuidState;

        bool m_AvatarInstantiated;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsClient || m_AvatarInstantiated)
            {
                return;
            }

            InstantiateAvatar();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            m_AvatarInstantiated = false;
            var avatarGraphics = Animator.transform.GetChild(0);
            if (avatarGraphics != null)
            {
                Destroy(avatarGraphics.gameObject);
            }
        }

        protected override void OnSynchronize<T>(ref BufferSerializer<T> serializer)
        {
            if (NetworkManager.Singleton.IsClient && !m_AvatarInstantiated)
            {
                InstantiateAvatar();
            }

            base.OnSynchronize(ref serializer);
        }

        void InstantiateAvatar()
        {
            if (Animator.transform.childCount > 0)
            {
                // we may receive a NetworkVariable's OnValueChanged callback more than once as a client
                // this makes sure we don't spawn a duplicate graphics GameObject
                // 클라이언트에서 NetworkVariable의 OnValueChanged 콜백을 여러 번 받을 수 있습니다.
                // 이를 방지하여 중복된 그래픽 GameObject가 생성되지 않도록 합니다.
                return;
            }

            // spawn avatar graphics GameObject
            // 아바타 그래픽 GameObject를 생성합니다.
            Instantiate(m_NetworkAvatarGuidState.RegisteredAvatar.Graphics, Animator.transform);

            Animator.Rebind();

            m_AvatarInstantiated = true;
        }
    }
}
