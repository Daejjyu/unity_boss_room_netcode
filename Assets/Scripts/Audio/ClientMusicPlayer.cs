using UnityEngine;

namespace Unity.BossRoom.Audio
{
    /// <summary>
    /// Music player that handles start of boss battle, victory and restart
    /// </summary>
    /// <summary>
    /// 보스 배틀 시작, 승리 및 재시작을 처리하는 음악 플레이어
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ClientMusicPlayer : MonoBehaviour
    {
        [SerializeField]
        private AudioClip m_ThemeMusic;

        [SerializeField]
        private AudioClip m_BossMusic;

        [SerializeField]
        private AudioClip m_VictoryMusic;

        [SerializeField]
        private AudioSource m_source;

        /// <summary>
        /// static accessor for ClientMusicPlayer
        /// </summary>
        /// <summary>
        /// ClientMusicPlayer에 대한 정적 접근자
        /// </summary>
        public static ClientMusicPlayer Instance { get; private set; }

        public void PlayThemeMusic(bool restart)
        {
            PlayTrack(m_ThemeMusic, true, restart);
        }

        public void PlayBossMusic()
        {
            // this can be called multiple times - play with restart = false
            // 여러 번 호출할 수 있습니다 - restart = false로 재생
            PlayTrack(m_BossMusic, true, false);
        }

        public void PlayVictoryMusic()
        {
            PlayTrack(m_VictoryMusic, false, false);
        }

        private void PlayTrack(AudioClip clip, bool looping, bool restart)
        {
            if (m_source.isPlaying)
            {
                // if we don't want to restart the clip, do nothing if it is playing
                // 클립을 재시작하고 싶지 않으면 재생 중이면 아무것도 하지 않음
                if (!restart && m_source.clip == clip) { return; }
                m_source.Stop();
            }
            m_source.clip = clip;
            m_source.loop = looping;
            m_source.time = 0;
            m_source.Play();
        }

        private void Awake()
        {
            m_source = GetComponent<AudioSource>();

            if (Instance != null)
            {
                throw new System.Exception("Multiple ClientMuscPlayers!");
            }
            DontDestroyOnLoad(gameObject);
            Instance = this;
        }
    }
}
