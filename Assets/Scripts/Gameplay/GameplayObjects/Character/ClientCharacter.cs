using System;
using Unity.BossRoom.CameraUtils;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Gameplay.UserInput;
using Unity.BossRoom.Utils;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
  public class ClientCharacter : NetworkBehaviour
  {
    [SerializeField]
    Animator m_ClientVisualsAnimator;

    [SerializeField]
    VisualizationConfiguration m_VisualizationConfiguration;

    public Animator OurAnimator => m_ClientVisualsAnimator;
    public GameObject TargetReticulePrefab => m_VisualizationConfiguration.TargetReticule;
    public Material ReticuleHostileMat => m_VisualizationConfiguration.ReticuleHostileMat;
    public Material ReticuleFriendlyMat => m_VisualizationConfiguration.ReticuleFriendlyMat;


    CharacterSwap m_CharacterSwapper;
  }
}