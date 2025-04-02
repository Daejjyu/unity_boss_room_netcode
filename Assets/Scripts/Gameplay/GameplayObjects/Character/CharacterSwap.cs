using System;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects.AnimationCallbacks;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
  public class CharacterSwap : MonoBehaviour
  {
    [System.Serializable]
    public class CharacterModelSet
    {
      public GameObject ears;
      public GameObject head;
      public GameObject mouth;
      public GameObject hair;
      public GameObject eyes;
      public GameObject torso;
      public GameObject gearRightHand;
      public GameObject gearLeftHand;
      public GameObject handRight;
      public GameObject handLeft;
      public GameObject shoulderRight;
      public GameObject shoulderLeft;
      public GameObject handSocket;
      public AnimatorTriggeredSpecialFX specialFx;
      public AnimatorOverrideController animatorOverrides; // references a separate stand-alone object in the project
    }
  }
}