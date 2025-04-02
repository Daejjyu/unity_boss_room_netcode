using System;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameState
{
  public enum WinState
  {
    Invalid,
    Win,
    Lose
  }

  public class PersistentGameState
  {
    public WinState WinState { get; private set; }

    public void SetWinState(WinState winState)
    {
      WinState = winState;
    }

    public void Reset()
    {
      WinState = WinState.Invalid;
    }
  }
}