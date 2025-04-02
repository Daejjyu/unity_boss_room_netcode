using System;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.VisualEffects;
using Unity.Netcode;
using UnityEngine;
using BlockingMode = Unity.BossRoom.Gameplay.Actions.BlockingModeType;

namespace Unity.BossRoom.Gameplay.Actions
{
  public abstract class Action : ScriptableObject
  {
    [NonSerialized]
    public ActionID ActionID;

    public const string k_DefaultHitReact = "HitReact1";

    protected ActionRequestData m_Data;
    public ref ActionRequestData Data => ref m_Data;

    public float TimeStarted { get; set; }
    public float TimeRunning { get { return (Time.time - TimeStarted); } }

    public ActionConfig Config;

  }
}