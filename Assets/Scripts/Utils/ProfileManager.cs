/// <summary>
/// This class manages profiles, including loading, saving, creating, and deleting profiles.
/// </summary>
/// <summary>
/// 이 클래스는 프로필을 관리하며, 프로필을 로드하고 저장하고 생성하고 삭제하는 기능을 포함합니다.
/// </summary>
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#if UNITY_EDITOR
using System.Security.Cryptography;
using System.Text;
#endif

using UnityEngine;

namespace Unity.BossRoom.Utils
{
    public class ProfileManager
    {
        /// <summary>
        /// The command line argument used to specify the authentication profile.
        /// </summary>
        /// <summary>
        /// 인증 프로필을 지정하는 데 사용되는 명령줄 인수입니다.
        /// </summary>
        public const string AuthProfileCommandLineArg = "-AuthProfile";

        string m_Profile;

        /// <summary>
        /// Gets or sets the current profile.
        /// </summary>
        /// <summary>
        /// 현재 프로필을 가져오거나 설정합니다.
        /// </summary>
        public string Profile
        {
            get
            {
                if (m_Profile == null)
                {
                    m_Profile = GetProfile();
                }

                return m_Profile;
            }
            set
            {
                m_Profile = value;
                onProfileChanged?.Invoke();
            }
        }

        public event Action onProfileChanged;

        List<string> m_AvailableProfiles;

        /// <summary>
        /// Gets the list of available profiles.
        /// </summary>
        /// <summary>
        /// 사용 가능한 프로필 목록을 가져옵니다.
        /// </summary>
        public ReadOnlyCollection<string> AvailableProfiles
        {
            get
            {
                if (m_AvailableProfiles == null)
                {
                    LoadProfiles();
                }

                return m_AvailableProfiles.AsReadOnly();
            }
        }

        /// <summary>
        /// Creates a new profile and adds it to the available profiles list.
        /// </summary>
        /// <summary>
        /// 새 프로필을 생성하고 사용 가능한 프로필 목록에 추가합니다.
        /// </summary>
        public void CreateProfile(string profile)
        {
            m_AvailableProfiles.Add(profile);
            SaveProfiles();
        }

        /// <summary>
        /// Deletes a profile from the available profiles list.
        /// </summary>
        /// <summary>
        /// 사용 가능한 프로필 목록에서 프로필을 삭제합니다.
        /// </summary>
        public void DeleteProfile(string profile)
        {
            m_AvailableProfiles.Remove(profile);
            SaveProfiles();
        }

        /// <summary>
        /// Gets the profile from the command line arguments or generates one if not specified.
        /// </summary>
        /// <summary>
        /// 명령줄 인수에서 프로필을 가져오거나 지정되지 않으면 새로 생성합니다.
        /// </summary>
        static string GetProfile()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] == AuthProfileCommandLineArg)
                {
                    var profileId = arguments[i + 1];
                    return profileId;
                }
            }

#if UNITY_EDITOR

            // When running in the Editor make a unique ID from the Application.dataPath.
            // This will work for cloning projects manually, or with Virtual Projects.
            // Since only a single instance of the Editor can be open for a specific
            // dataPath, uniqueness is ensured.
            // 에디터에서 실행할 때는 Application.dataPath에서 고유한 ID를 만듭니다.
            // 이는 프로젝트를 수동으로 복제하거나 가상 프로젝트와 함께 사용할 수 있습니다.
            // 특정 dataPath에 대해 에디터 인스턴스는 하나만 열 수 있으므로 고유성이 보장됩니다.
            var hashedBytes = new MD5CryptoServiceProvider()
                .ComputeHash(Encoding.UTF8.GetBytes(Application.dataPath));
            Array.Resize(ref hashedBytes, 16);
            // Authentication service only allows profile names of maximum 30 characters. We're generating a GUID based
            // on the project's path. Truncating the first 30 characters of said GUID string suffices for uniqueness.
            // 인증 서비스는 최대 30자의 프로필 이름만 허용합니다. 우리는 프로젝트 경로를 기준으로 GUID를 생성하고,
            // 해당 GUID 문자열의 처음 30자를 잘라내면 고유성이 보장됩니다.
            return new Guid(hashedBytes).ToString("N")[..30];
#else
            return "";
#endif
        }

        /// <summary>
        /// Loads the available profiles from the client preferences.
        /// </summary>
        /// <summary>
        /// 클라이언트 환경 설정에서 사용 가능한 프로필을 로드합니다.
        /// </summary>
        void LoadProfiles()
        {
            m_AvailableProfiles = new List<string>();
            var loadedProfiles = ClientPrefs.GetAvailableProfiles();
            foreach (var profile in loadedProfiles.Split(',')) // this works since we're sanitizing our input strings
            {
                if (profile.Length > 0)
                {
                    m_AvailableProfiles.Add(profile);
                }
            }
        }

        /// <summary>
        /// Saves the current list of available profiles to client preferences.
        /// </summary>
        /// <summary>
        /// 현재 사용 가능한 프로필 목록을 클라이언트 환경 설정에 저장합니다.
        /// </summary>
        void SaveProfiles()
        {
            var profilesToSave = "";
            foreach (var profile in m_AvailableProfiles)
            {
                profilesToSave += profile + ",";
            }
            ClientPrefs.SetAvailableProfiles(profilesToSave);
        }

    }
}
