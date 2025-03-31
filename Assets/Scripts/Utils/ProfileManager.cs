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
    public const string AuthProfileCommandLineArg = "-AuthProfile";

    string m_Profile;

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

    public void CreateProfile(string profile)
    {
      m_AvailableProfiles.Add(profile);
      SaveProfiles();
    }

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
      var hashedBytes = new MD5CryptoServiceProvider()
          .ComputeHash(Encoding.UTF8.GetBytes(Application.dataPath));
      Array.Resize(ref hashedBytes, 16);
      return new Guid(hashedBytes).ToString("N")[..30];
#else
      return "";
#endif
    }

    void LoadProfiles()
    {
      m_AvailableProfiles = new List<string>();
      var loadedProfiles = ClientPrefs.GetAvailableProfiles();
      foreach (var profile in loadedProfiles.Split(','))
      {
        if (profile.Length > 0)
        {
          m_AvailableProfiles.Add(profile);
        }
      }
    }

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