using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.BossRoom.VisualEffects
{
  /// <summary>
  /// Utility script attached to special-effects prefabs. These prefabs are
  /// used by various ActionFX that need to show special short-lived graphics
  /// such as "charging up" particles, ground path indicators, etc.
  /// 
  /// There are two different conceptual "modes":
  /// - keep running until somebody explicitly calls Shutdown() (this is used by Actions with indeterminate durations; set m_AutoShutdownTime to -1)
  /// - automatically call Shutdown() after a fixed amount of time (set m_AutoShutdownTime to the number of seconds)
  /// 
  /// Note that whichever mode is used, Shutdown() may be called prematurely by whoever owns this graphic
  /// in the case of aborted actions.
  /// 
  /// Once Shutdown() is called (one way or another), the object self-destructs after the particles end
  /// (or after a specific additional amount of time).
  /// </summary>
  /// <remarks>
  /// When a particle system ends, it usually needs to stick around for a little while
  /// to let the last remaining particles finish rendering. Shutdown() turns off particles,
  /// and then self-destructs after the particles are all gone. ParticleSystems can technically
  /// self-destruct on their own after being stopped: see the "Stop Action" field in the
  /// ParticleSystem's inspector. But this script also acts as a way to self-destruct non-particle
  /// graphics, and if you're implementing object pooling (for improved mobile performance), this
  /// class can be refactored to move itself into an object pool instead of self-destructing.
  /// </remarks>
  /// <summary>
  /// 특수 효과 프리팹에 첨부된 유틸리티 스크립트입니다. 
  /// 이 프리팹은 "충전 중" 파티클, 지면 경로 표시기 등과 같은
  /// 짧은 시간 동안만 표시되는 특수 그래픽을 보여야 하는 
  /// 다양한 ActionFX에 의해 사용됩니다.
  /// 
  /// 두 가지 다른 개념적 "모드"가 있습니다:
  /// - 누군가 명시적으로 Shutdown()을 호출할 때까지 계속 실행 
  ///    (불확실한 기간을 가진 액션에서 사용; m_AutoShutdownTime을 -1로 설정)
  /// - 일정 시간이 지나면 자동으로 Shutdown()을 호출 
  ///  (m_AutoShutdownTime을 초 단위로 설정)
  /// 
  /// 어떤 모드를 사용하든, Shutdown()은 중단된 액션의 경우 
  /// 해당 그래픽을 소유한 사람이 미리 호출할 수 있습니다.
  /// 
  /// Shutdown()이 호출되면 (어쨌든) 파티클이 끝난 후 객체는 자동으로 파괴됩니다
  /// (또는 특정 추가 시간이 지난 후).
  /// </summary>
  /// <remarks>
  /// 파티클 시스템이 종료될 때, 마지막 남은 파티클들이 렌더링을 마칠 수 있도록 
  /// 잠시 대기해야 하는 경우가 많습니다.
  /// Shutdown()은 파티클을 끄고, 모든 파티클이 사라진 후 자동으로 삭제됩니다. 
  /// 파티클 시스템은 기술적으로
  /// 정지된 후 스스로 삭제될 수 있습니다: 파티클 시스템 검사기의 "Stop Action" 
  /// 필드를 참조하십시오.
  /// 그러나 이 스크립트는 비파티클 그래픽도 자가 삭제하는 방법으로 작동하며, 
  /// 객체 풀링을 구현하는 경우
  /// (모바일 성능 향상 목적) 이 클래스는 자가 삭제 대신 객체 풀로 
  /// 자신을 이동하도록 리팩터링할 수 있습니다.
  /// </remarks>
  public class SpecialFXGraphic : MonoBehaviour
  {
    [SerializeField]
    [Tooltip("Particles that should be stopped on Shutdown")]
    public List<ParticleSystem> m_ParticleSystemsToTurnOffOnShutdown;

    [SerializeField]
    [Tooltip("If this graphic should automatically Shutdown after a certain time, set it here (in seconds). -1 means no auto-shutdown.")]
    private float m_AutoShutdownTime = -1;

    [SerializeField]
    [Tooltip("After Shutdown, how long before we self-destruct? 0 means no self destruct. -1 means self-destruct after ALL particles have disappeared")]
    private float m_PostShutdownSelfDestructTime = -1;

    [SerializeField]
    [Tooltip("If this graphic should keep its spawn rotation during its lifetime.")]
    bool m_StayAtSpawnRotation;

    // track when Shutdown() is called so we don't try to do it twice
    private bool m_IsShutdown = false;

    // we keep a reference to our self-destruction coroutine in case we need to abort it prematurely
    private Coroutine coroWaitForSelfDestruct = null;

    Quaternion m_StartRotation;

    private void Start()
    {
      m_StartRotation = transform.rotation;

      if (m_AutoShutdownTime != -1)
      {
        coroWaitForSelfDestruct = StartCoroutine(CoroWaitForSelfDestruct());
      }
    }

    public void Shutdown()
    {
      if (!m_IsShutdown)
      {
        foreach (var particleSystem in m_ParticleSystemsToTurnOffOnShutdown)
        {
          if (particleSystem)
          {
            particleSystem.Stop();
          }
        }

        // now, when and how do we fully destroy ourselves?
        if (m_PostShutdownSelfDestructTime >= 0)
        {
          // we have a fixed-time, so just destroy ourselves after that time
          Destroy(gameObject, m_PostShutdownSelfDestructTime);
        }
        else if (m_PostShutdownSelfDestructTime == -1)
        {
          // special case! It means "keep checking the particles and self-destruct when they're all fully done"
          StartCoroutine(CoroWaitForParticlesToEnd());
        }

        m_IsShutdown = true;
      }
    }

    private IEnumerator CoroWaitForParticlesToEnd()
    {
      bool foundAliveParticles;
      do
      {
        yield return new WaitForEndOfFrame();
        foundAliveParticles = false;
        foreach (var particleSystem in m_ParticleSystemsToTurnOffOnShutdown)
        {
          if (particleSystem.IsAlive())
          {
            foundAliveParticles = true;
          }
        }
      } while (foundAliveParticles);

      if (coroWaitForSelfDestruct != null)
      {
        StopCoroutine(coroWaitForSelfDestruct);
      }

      Destroy(gameObject);
      yield break;
    }

    private IEnumerator CoroWaitForSelfDestruct()
    {
      yield return new WaitForSeconds(m_AutoShutdownTime);
      coroWaitForSelfDestruct = null;
      if (!m_IsShutdown)
      {
        Shutdown();
      }
    }

    void Update()
    {
      if (m_StayAtSpawnRotation)
      {
        transform.rotation = m_StartRotation;
      }
    }
  }

#if UNITY_EDITOR
  /// <summary>
  /// A custom editor that provides a button in the Inspector to auto-add all the
  /// particle systems in a SpecialFXGraphic (so we don't have to manually maintain the list).
  /// </summary>
  /// <summary>
  /// SpecialFXGraphic에서 모든 파티클 시스템을 자동으로 추가할 수 있는 버튼을 검사기에서 제공하는
  /// 사용자 정의 편집기입니다 (리스트를 수동으로 유지할 필요가 없습니다).
  /// </summary>
  [CustomEditor(typeof(SpecialFXGraphic))]
  public class SpecialFXGraphicEditor : UnityEditor.Editor
  {
    public override void OnInspectorGUI()
    {
      DrawDefaultInspector();
      if (GUILayout.Button("Auto-Add All Particle Systems"))
      {
        AddAllParticleSystems((SpecialFXGraphic)target);
      }
    }

    private void AddAllParticleSystems(SpecialFXGraphic specialFxGraphic)
    {
      if (specialFxGraphic.m_ParticleSystemsToTurnOffOnShutdown == null)
      {
        specialFxGraphic.m_ParticleSystemsToTurnOffOnShutdown = new List<ParticleSystem>();
      }

      specialFxGraphic.m_ParticleSystemsToTurnOffOnShutdown.Clear();
      foreach (var particleSystem in specialFxGraphic.GetComponentsInChildren<ParticleSystem>())
      {
        specialFxGraphic.m_ParticleSystemsToTurnOffOnShutdown.Add(particleSystem);
      }
    }
  }
#endif

}


