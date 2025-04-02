using Unity.BossRoom.Utils;
using UnityEngine;
using UnityEngine.Audio;

namespace Unity.BossRoom.Audio
{
    /// <summary>
    /// Initializes the game's AudioMixer to use volumes stored in preferences. Provides
    /// a public function that can be called when these values change.
    /// </summary>
    /// <summary>
    /// 게임의 AudioMixer를 초기화하여 설정된 볼륨 값을 사용합니다. 이 값들이 변경될 때 호출할 수 있는
    /// 공개 함수도 제공합니다.
    /// </summary>
    public class AudioMixerConfigurator : MonoBehaviour
    {
        [SerializeField]
        private AudioMixer m_Mixer;

        [SerializeField]
        private string m_MixerVarMainVolume = "OverallVolume";

        [SerializeField]
        private string m_MixerVarMusicVolume = "MusicVolume";

        public static AudioMixerConfigurator Instance { get; private set; }

        /// <summary>
        /// The audio sliders use a value between 0.0001 and 1, but the mixer works in decibels -- by default, -80 to 0.
        /// To convert, we use log10(slider) multiplied by 20. Why 20? because log10(.0001)*20=-80, which is the
        /// bottom range for our mixer, meaning it's disabled.
        /// </summary>
        /// <summary>
        /// 오디오 슬라이더는 0.0001과 1 사이의 값을 사용하지만, 믹서는 데시벨로 작동합니다. 기본적으로 -80에서 0까지입니다.
        /// 이를 변환하기 위해서는 log10(슬라이더 값)에 20을 곱합니다. 왜 20일까요? log10(.0001)*20=-80이기 때문인데,
        /// 이는 믹서의 하한 범위로, 비활성화된 상태를 의미합니다.
        /// </summary>
        private const float k_VolumeLog10Multiplier = 20;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // note that trying to configure the AudioMixer during Awake does not work, must be initialized in Start
            // Awake에서 AudioMixer를 설정하려고 하면 작동하지 않습니다. 반드시 Start에서 초기화해야 합니다.
            Configure();
        }

        public void Configure()
        {
            m_Mixer.SetFloat(m_MixerVarMainVolume, GetVolumeInDecibels(ClientPrefs.GetMasterVolume()));
            m_Mixer.SetFloat(m_MixerVarMusicVolume, GetVolumeInDecibels(ClientPrefs.GetMusicVolume()));
        }

        private float GetVolumeInDecibels(float volume)
        {
            if (volume <= 0) // sanity-check in case we have bad prefs data
            // 잘못된 환경설정 데이터가 있는 경우를 대비한 유효성 검사
            {
                volume = 0.0001f;
            }
            return Mathf.Log10(volume) * k_VolumeLog10Multiplier;
        }
    }
}
