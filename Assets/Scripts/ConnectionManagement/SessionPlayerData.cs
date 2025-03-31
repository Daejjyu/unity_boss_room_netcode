using Unity.BossRoom.Infrastructure;
using Unity.Multiplayer.Samples.BossRoom;
using UnityEngine;

namespace Unity.BossRoom.ConnectionManagement
{
  public struct SessionPlayerData : ISessionPlayerData
  {
    public string PlayerName;
    public int PlayerNumber;
    public Vector3 PlayerPosition;
    public Quaternion PlayerRotation;
    /// Instead of using a NetworkGuid (two ulongs) we could just use an int or even a byte-sized index into an array of possible avatars defined in our game data source
    /// NetworkGuid (두 개의 ulong)을 사용하는 대신, 게임 데이터 소스에서 정의된 가능한 아바타 배열에 대한 int 또는 바이트 크기 인덱스를 사용할 수 있습니다.
    public NetworkGuid AvatarNetworkGuid;
    public int CurrentHitPoints;
    public bool HasCharacterSpawned;

    public SessionPlayerData(ulong clientID, string name, NetworkGuid avatarNetworkGuid, int currentHitPoints = 0, bool isConnected = false, bool hasCharacterSpawned = false)
    {
      ClientID = clientID;
      PlayerName = name;
      PlayerNumber = -1;
      PlayerPosition = Vector3.zero;
      PlayerRotation = Quaternion.identity;
      AvatarNetworkGuid = avatarNetworkGuid;
      CurrentHitPoints = currentHitPoints;
      IsConnected = isConnected;
      HasCharacterSpawned = hasCharacterSpawned;
    }

    public bool IsConnected { get; set; }
    public ulong ClientID { get; set; }

    public void Reinitialize()
    {
      HasCharacterSpawned = false;
    }
  }
}
