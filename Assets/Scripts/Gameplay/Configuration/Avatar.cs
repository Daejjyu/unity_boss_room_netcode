using System;
using Unity.BossRoom.Infrastructure;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Configuration
{
  [CreateAssetMenu]
  [Serializable]
  public sealed class Avatar : GuidScriptableObject
  {
    public CharacterClass CharacterClass;

    public GameObject Graphics;

    public GameObject GraphicsCharacterSelect;

    public Sprite Portrait;
  }
}