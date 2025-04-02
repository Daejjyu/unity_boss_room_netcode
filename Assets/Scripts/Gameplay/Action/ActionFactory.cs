/// <summary>
/// Action을 생성하는 팩토리 메소드입니다. 요청된 데이터를 기반으로 Action을 만듭니다.
/// </summary>
/// <param name="data">이 스킬을 생성할 데이터를 나타냅니다.</param>
/// <returns>새로 생성된 액션을 반환합니다.</returns>
using System;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace Unity.BossRoom.Gameplay.Actions
{
  public static class ActionFactory
  {
    private static Dictionary<ActionID, ObjectPool<Action>> s_ActionPools = new Dictionary<ActionID, ObjectPool<Action>>();

    private static ObjectPool<Action> GetActionPool(ActionID actionID)
    {
      if (!s_ActionPools.TryGetValue(actionID, out var actionPool))
      {
        actionPool = new ObjectPool<Action>(
            createFunc: () => Object.Instantiate(GameDataSource.Instance.GetActionPrototypeByID(actionID)),
            actionOnRelease: action => action.Reset(),
            actionOnDestroy: Object.Destroy);

        s_ActionPools.Add(actionID, actionPool);
      }

      return actionPool;
    }

    /// <summary>
    /// 요청된 데이터를 기반으로 Action을 생성하는 팩토리 메소드입니다.
    /// </summary>
    /// <param name="data">이 스킬을 생성할 데이터를 나타냅니다.</param>
    /// <returns>새로 생성된 액션을 반환합니다.</returns>
    public static Action CreateActionFromData(ref ActionRequestData data)
    {
      var ret = GetActionPool(data.ActionID).Get();
      ret.Initialize(ref data);
      return ret;
    }

    public static void ReturnAction(Action action)
    {
      var pool = GetActionPool(action.ActionID);
      pool.Release(action);
    }

    public static void PurgePooledActions()
    {
      foreach (var actionPool in s_ActionPools.Values)
      {
        actionPool.Clear();
      }
    }
  }
}
