using System;
using System.Collections;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.VisualEffects;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

namespace Unity.BossRoom.Gameplay.GameplayObjects.AnimationCallbacks
{
  /// <summary>
  /// Instantiates and maintains graphics prefabs and sound effects. They're triggered by entering
  /// (or exiting) specific nodes in an Animator. (Each relevant Animator node must have an
  /// AnimationNodeHook component attached.)
  /// </summary>
  /// <summary>
  /// 그래픽 프리팹과 사운드 효과를 인스턴스화하고 유지합니다. 
  /// 이들은 Animator의 특정 노드를 들어가거나 나가면서 트리거됩니다. 
  /// (각 관련된 Animator 노드에는 AnimationNodeHook 컴포넌트가 첨부되어야 합니다.)
  /// </summary>
  public class AnimatorTriggeredSpecialFX : MonoBehaviour
  {
    [SerializeField]
    [Tooltip("Unused by the game and provided only for internal dev comments; put whatever you want here")]
    [TextArea]
    private string DevNotes;
    // e.g. "this is for the tank class". Documentation for the artists, because all 4 class's AnimatorTriggeredSpecialFX components are on the same GameObject. Can remove later if desired
    // 예: "이것은 탱크 클래스용입니다". 모든 4개의 클래스의 
    // AnimatorTriggeredSpecialFX 컴포넌트가 동일한 GameObject에 있기 때문에 
    // 아티스트를 위한 문서입니다. 원할 경우 나중에 제거할 수 있습니다
    [Serializable]
    internal class AnimatorNodeEntryEvent
    {
      [Tooltip("The name of a node in the Animator's state machine.")]
      public string m_AnimatorNodeName;
      [HideInInspector]
      public int m_AnimatorNodeNameHash; // this is maintained via OnValidate() in the editor

      [Header("Particle Prefab")]
      [Tooltip("The prefab that should be instantiated when we enter an Animator node with this name")]
      public SpecialFXGraphic m_Prefab;
      [Tooltip("Wait this many seconds before instantiating the Prefab. (If we leave the animation node before this point, no FX are played.)")]
      public float m_PrefabSpawnDelaySeconds;
      [Tooltip("If we leave the AnimationNode, should we shutdown the fx or let it play out? 0 = never cancel. Any other time = we can cancel up until this amount of time has elapsed... after that, we just let it play out. So a really big value like 9999 effectively means 'always cancel'")]
      public float m_PrefabCanBeAbortedUntilSecs;
      [Tooltip("If the particle should be parented to a specific bone, link that bone here. (If null, plays at character's feet.)")]
      public Transform m_PrefabParent;
      [Tooltip("Prefab will be spawned with this local offset from the parent (Remember, it's a LOCAL offset, so it's affected by the parent transform's scale and rotation!)")]
      public Vector3 m_PrefabParentOffset;
      [Tooltip("Should we disconnect the prefab from the character? (So the prefab's transform has no parent)")]
      public bool m_DeParentPrefab;

      [Header("Sound Effect")]
      [Tooltip("If we want to use a sound effect that's not in the prefab, specify it here")]
      public AudioClip m_SoundEffect;
      [Tooltip("Time (in seconds) before we start playing this sound. If we leave the animation node before this time, no sound plays")]
      public float m_SoundStartDelaySeconds;
      [Tooltip("Relative volume to play at.")]
      public float m_VolumeMultiplier = 1;
      [Tooltip("Should we loop the sound for as long as we're in the animation node?")]
      public bool m_LoopSound = false;
    }
    [SerializeField]
    internal AnimatorNodeEntryEvent[] m_EventsOnNodeEntry;

    /// <summary>
    /// These are the AudioSources we'll use to play sounds. For non-looping sounds we only need one,
    /// but to play multiple looping sounds we need additional AudioSources, since each one can only
    /// play one looping sound at a time.
    /// (These AudioSources are typically on the same GameObject as us, but they don't have to be.)
    /// </summary>
    /// <summary>
    /// 이는 우리가 사운드를 재생하는 데 사용할 AudioSource입니다. 반복되지 않는 사운드에는 하나만 필요하지만,
    /// 여러 개의 반복되는 사운드를 재생하려면 추가적인 AudioSource가 필요합니다. 
    /// 각 AudioSource는 한 번에 하나의 반복되는 사운드만 재생할 수 있기 때문입니다.
    /// (이 AudioSource들은 일반적으로 우리와 같은 GameObject에 있지만, 반드시 그럴 필요는 없습니다.)
    /// </summary>
    [SerializeField]
    internal AudioSource[] m_AudioSources;

    /// <summary>
    /// cached reference to our Animator.
    /// </summary>
    /// <summary>
    /// Animator에 대한 캐시된 참조입니다.
    /// </summary>
    [SerializeField]
    private Animator m_Animator;

    /// <summary>
    /// contains the shortNameHash of all the active animation nodes right now
    /// </summary>
    /// <summary>
    /// 현재 활성화된 모든 애니메이션 노드의 shortNameHash를 포함합니다.
    /// </summary>
    private HashSet<int> m_ActiveNodes = new HashSet<int>();

    [FormerlySerializedAs("m_ClientCharacterVisualization")]
    [SerializeField]
    ClientCharacter m_ClientCharacter;

    private void Awake()
    {
      Debug.Assert(m_AudioSources != null && m_AudioSources.Length > 0, "No AudioSource plugged into AnimatorTriggeredSpecialFX!", gameObject);

      if (!m_ClientCharacter)
      {
        m_ClientCharacter = GetComponentInParent<ClientCharacter>();

        m_Animator = m_ClientCharacter.OurAnimator;
      }
    }

    public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
      Debug.Assert(m_Animator == animator); // just a sanity check

      m_ActiveNodes.Add(stateInfo.shortNameHash);

      // figure out which of our on-node-enter events (if any) should be triggered, and trigger it
      // 어떤 on-node-enter 이벤트 (있다면)가 트리거되어야 할지 확인하고, 이를 트리거합니다
      foreach (var info in m_EventsOnNodeEntry)
      {
        if (info.m_AnimatorNodeNameHash == stateInfo.shortNameHash)
        {
          if (info.m_Prefab)
          {
            StartCoroutine(CoroPlayStateEnterFX(info));
          }
          if (info.m_SoundEffect)
          {
            StartCoroutine(CoroPlayStateEnterSound(info));
          }
        }
      }
    }

    // creates and manages the graphics prefab (but not the sound effect) of an on-enter event
    // on-enter 이벤트의 그래픽 프리팹을 생성하고 관리합니다 (하지만 사운드 효과는 제외)
    private IEnumerator CoroPlayStateEnterFX(AnimatorNodeEntryEvent eventInfo)
    {
      // FX 생성 전에 일정 시간 대기 (예: 0.5초 뒤에 생성되도록 설정 가능)
      if (eventInfo.m_PrefabSpawnDelaySeconds > 0)
        yield return new WaitForSeconds(eventInfo.m_PrefabSpawnDelaySeconds);

      // 현재 애니메이터 노드가 활성 상태가 아니라면 FX를 생성하지 않음
      if (!m_ActiveNodes.Contains(eventInfo.m_AnimatorNodeNameHash))
        yield break;

      // FX의 부모를 설정 (설정된 부모가 없으면 캐릭터의 Transform을 사용)
      Transform parent = eventInfo.m_PrefabParent != null ? eventInfo.m_PrefabParent : m_ClientCharacter.transform;
      // FX 인스턴스를 생성하고 위치 보정
      var instantiatedFX = Instantiate(eventInfo.m_Prefab, parent);
      instantiatedFX.transform.localPosition += eventInfo.m_PrefabParentOffset;

      // 부모에서 분리할 옵션이 설정되었다면 부모를 해제
      // should we have no parent transform at all? (Note that we're de-parenting AFTER applying
      // the PrefabParent, so that PrefabParent can still be used to determine the initial position/rotation/scale.)
      // 아예 부모 변환을 없앨까요? (PrefabsParent를 적용한 후에 부모를 분리하고 있으므로, 
      // PrefabParent가 여전히 초기 위치/회전/스케일을 결정하는 데 사용될 수 있습니다.)
      if (eventInfo.m_DeParentPrefab)
      {
        instantiatedFX.transform.SetParent(null);
      }

      // 특정 시간이 지나기 전까지 FX를 중단할 수 있는지 체크
      // now we just need to watch and see if we end up needing to prematurely end these new graphics
      // 이제 우리는 새로운 그래픽을 조기에 종료해야 하는지 확인하기만 하면 됩니다
      if (eventInfo.m_PrefabCanBeAbortedUntilSecs > 0)
      {
        float timeRemaining = eventInfo.m_PrefabCanBeAbortedUntilSecs - eventInfo.m_PrefabSpawnDelaySeconds;
        while (timeRemaining > 0 && instantiatedFX)
        {
          yield return new WaitForFixedUpdate();
          timeRemaining -= Time.fixedDeltaTime;
          if (!m_ActiveNodes.Contains(eventInfo.m_AnimatorNodeNameHash))
          {
            // 현재 애니메이터 노드가 비활성화되면 FX 중단
            // the node we were in has ended! Shut down the FX
            if (instantiatedFX)
            {
              instantiatedFX.Shutdown();
            }
          }
        }
      }
    }

    // the node we were in has ended! Shut down the FX
    // 우리가 있던 노드가 끝났습니다! FX를 종료합니다
    private IEnumerator CoroPlayStateEnterSound(AnimatorNodeEntryEvent eventInfo)
    {
      if (eventInfo.m_SoundStartDelaySeconds > 0)
        yield return new WaitForSeconds(eventInfo.m_SoundStartDelaySeconds);

      if (!m_ActiveNodes.Contains(eventInfo.m_AnimatorNodeNameHash))
        yield break;

      if (!eventInfo.m_LoopSound)
      {
        m_AudioSources[0].PlayOneShot(eventInfo.m_SoundEffect, eventInfo.m_VolumeMultiplier);
      }
      else
      {
        AudioSource audioSource = GetAudioSourceForLooping();
        // we're using all our audio sources already. just give up
        // 이미 모든 오디오 소스를 사용 중입니다. 그냥 포기합니다
        if (!audioSource)
          yield break;
        audioSource.volume = eventInfo.m_VolumeMultiplier;
        audioSource.loop = true;
        audioSource.clip = eventInfo.m_SoundEffect;
        audioSource.Play();
        while (m_ActiveNodes.Contains(eventInfo.m_AnimatorNodeNameHash) && audioSource.isPlaying)
        {
          yield return new WaitForFixedUpdate();
        }
        audioSource.Stop();
      }
    }

    /// <summary>
    /// retrieves an available AudioSource that isn't currently playing a looping sound, or null if none are currently available
    /// </summary>
    /// <summary>
    /// 현재 반복되는 사운드를 재생하지 않고 사용 가능한 AudioSource를 반환하며, 
    /// 현재 사용 가능한 AudioSource가 없다면 null을 반환합니다
    /// </summary>
    private AudioSource GetAudioSourceForLooping()
    {
      foreach (var audioSource in m_AudioSources)
      {
        if (audioSource && !audioSource.isPlaying)
          return audioSource;
      }
      Debug.LogWarning($"{name} doesn't have enough AudioSources to loop all desired sound effects. (Have {m_AudioSources.Length}, need at least 1 more)", gameObject);
      return null;
    }

    public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
      Debug.Assert(m_Animator == animator); // just a sanity check

      m_ActiveNodes.Remove(stateInfo.shortNameHash);
    }

    /// <summary>
    /// Precomputes the hashed values for the animator-tags we care about.
    /// (This way we don't have to call Animator.StringToHash() at runtime.)
    /// Also auto-initializes variables when possible.
    /// </summary>
    /// <summary>
    /// 우리가 관심 있는 애니메이터 태그의 해시 값을 미리 계산합니다.
    /// (이렇게 하면 런타임에서 Animator.StringToHash()를 호출할 필요가 없습니다.)
    /// 또한 가능한 경우 변수들을 자동으로 초기화합니다.
    /// </summary>
    private void OnValidate()
    {
      if (m_EventsOnNodeEntry != null)
      {
        for (int i = 0; i < m_EventsOnNodeEntry.Length; ++i)
        {
          m_EventsOnNodeEntry[i].m_AnimatorNodeNameHash = Animator.StringToHash(m_EventsOnNodeEntry[i].m_AnimatorNodeName);
        }
      }

      if (m_AudioSources == null || m_AudioSources.Length == 0) // if we have AudioSources handy, plug them in automatically
      {
        m_AudioSources = GetComponents<AudioSource>();
      }
    }

  }


#if UNITY_EDITOR
  /// <summary>
  /// This adds a button in the Inspector. Pressing it validates that all the
  /// animator node names we reference are actually used by our Animator. We
  /// can also show informational messages about problems with the configuration.
  /// </summary>
  /// <summary>
  /// 이것은 인스펙터에 버튼을 추가합니다. 이 버튼을 누르면 우리가 참조하는 모든 애니메이터 노드 이름이 실제로 
  /// 우리의 애니메이터에서 사용되는지 검증합니다. 또한 구성에 문제가 있는 경우 정보 메시지를 표시할 수 있습니다.
  /// </summary>
  [CustomEditor(typeof(AnimatorTriggeredSpecialFX))]
  [CanEditMultipleObjects]
  public class AnimatorTriggeredSpecialFXEditor : UnityEditor.Editor
  {
    private GUIStyle m_ErrorStyle = null;
    public override void OnInspectorGUI()
    {
      // let Unity do all the normal Inspector stuff...
      // Unity가 모든 기본 인스펙터 작업을 하도록 합니다
      DrawDefaultInspector();

      // ... then we tack extra stuff on the bottom
      // ... 그런 다음 아래에 추가적인 항목을 덧붙입니다
      var fx = (AnimatorTriggeredSpecialFX)target;
      if (!HasAudioSource(fx))
      {
        GUILayout.Label("No Audio Sources Connected!", GetErrorStyle());
      }

      if (GUILayout.Button("Validate Node Names"))
      {
        ValidateNodeNames(fx);
      }

      // it's really hard to follow the inspector when there's a lot of these components on the same GameObject... so let's add a bit of whitespace
      // 동일한 GameObject에 이러한 컴포넌트가 많이 있을 때 인스펙터를 따라가기가 정말 어려우므로, 
      // 약간의 공백을 추가합니다
      EditorGUILayout.Space(50);
    }

    private GUIStyle GetErrorStyle()
    {
      if (m_ErrorStyle == null)
      {
        m_ErrorStyle = new GUIStyle(EditorStyles.boldLabel);
        m_ErrorStyle.normal.textColor = Color.red;
        m_ErrorStyle.fontSize += 5;
      }
      return m_ErrorStyle;
    }

    private bool HasAudioSource(AnimatorTriggeredSpecialFX fx)
    {
      if (fx.m_AudioSources == null)
        return false;
      foreach (var audioSource in fx.m_AudioSources)
      {
        if (audioSource != null)
          return true;
      }
      return false;
    }

    private void ValidateNodeNames(AnimatorTriggeredSpecialFX fx)
    {
      Animator animator = fx.GetComponent<Animator>();
      if (!animator)
      {
        // should be impossible because we explicitly RequireComponent the Animator
        // Animator가 이 GameObject에서 없을 경우! (이건 불가능해야 하므로)
        EditorUtility.DisplayDialog("Error", "No Animator found on this GameObject!?", "OK");
        return;
      }
      if (animator.runtimeAnimatorController == null)
      {
        // perfectly normal user error: they haven't plugged a controller into the Animator
        // 일반적인 사용자 오류: Animator에 AnimatorController가 연결되지 않았습니다
        EditorUtility.DisplayDialog("Error", "The Animator does not have an AnimatorController in it!", "OK");
        return;
      }

      // make sure there aren't any duplicated event entries!
      // 이벤트 항목이 중복되지 않았는지 확인합니다!
      int totalErrors = 0;
      for (int i = 0; i < fx.m_EventsOnNodeEntry.Length; ++i)
      {
        for (int j = i + 1; j < fx.m_EventsOnNodeEntry.Length; ++j)
        {
          if (fx.m_EventsOnNodeEntry[i].m_AnimatorNodeNameHash == fx.m_EventsOnNodeEntry[j].m_AnimatorNodeNameHash
              && fx.m_EventsOnNodeEntry[i].m_AnimatorNodeNameHash != 0
              && fx.m_EventsOnNodeEntry[i].m_Prefab == fx.m_EventsOnNodeEntry[j].m_Prefab
              && fx.m_EventsOnNodeEntry[i].m_SoundEffect == fx.m_EventsOnNodeEntry[j].m_SoundEffect)
          {
            ++totalErrors;
            Debug.LogError($"Entries {i} and {j} in EventsOnNodeEntry refer to the same node name ({fx.m_EventsOnNodeEntry[i].m_AnimatorNodeName}) and have the same prefab/sounds! This is probably a copy-paste error.");
          }
        }
      }

      // create a map of nameHash -> useful debugging information (which we display in the log if there's a problem)
      // nameHash -> 유용한 디버깅 정보를 매핑합니다 (문제가 있을 경우 로그에서 표시)
      Dictionary<int, string> usedNames = new Dictionary<int, string>();
      for (int i = 0; i < fx.m_EventsOnNodeEntry.Length; ++i)
      {
        usedNames[fx.m_EventsOnNodeEntry[i].m_AnimatorNodeNameHash] = $"{fx.m_EventsOnNodeEntry[i].m_AnimatorNodeName} (EventsOnNodeEntry index {i})";
      }

      int totalUsedNames = usedNames.Count;

      // now remove all the hashes that are actually used by the controller
      // 이제 컨트롤러에서 실제로 사용되는 해시를 제거합니다
      AnimatorController controller = GetAnimatorController(animator);
      foreach (var layer in controller.layers)
      {
        foreach (var state in layer.stateMachine.states)
        {
          usedNames.Remove(state.state.nameHash);
        }
      }

      // anything that hasn't gotten removed from usedNames isn't actually valid!
      // usedNames에서 제거되지 않은 항목은 실제로 유효하지 않습니다!
      foreach (var hash in usedNames.Keys)
      {
        Debug.LogError("Could not find Animation node named " + usedNames[hash]);
      }
      totalErrors += usedNames.Keys.Count;

      if (totalErrors == 0)
      {
        EditorUtility.DisplayDialog("Success", $"All {totalUsedNames} referenced node names were found in the Animator. No errors found!", "OK!");
      }
      else
      {
        EditorUtility.DisplayDialog("Errors", $"Found {totalErrors} errors. See the log in the Console tab for more information.", "OK");
      }
    }

    /// <summary>
    /// Pulls the AnimatorController out of an Animator. Important: this technique can only work
    /// in the editor. You can never reference an AnimatorController directly at runtime! (It might
    /// seem to work while you're running the game in the editor, but it won't compile when you
    /// try to build a standalone client, because AnimatorController is in an editor-only namespace.)
    /// </summary>
    /// <summary>
    /// Animator에서 AnimatorController를 가져옵니다. 중요: 이 기법은 에디터에서만 작동할 수 있습니다.
    /// 런타임에서는 AnimatorController를 직접 참조할 수 없습니다! (에디터에서 게임을 실행하는 동안에는 
    /// 작동하는 것처럼 보일 수 있지만, 독립 실행형 클라이언트를 빌드할 때는 컴파일되지 않으며, 
    /// AnimatorController는 에디터 전용 네임스페이스에 있기 때문입니다.)
    /// </summary>
    private AnimatorController GetAnimatorController(Animator animator)
    {
      Debug.Assert(animator); // already pre-checked
      Debug.Assert(animator.runtimeAnimatorController); // already pre-checked

      // we need the AnimatorController, but there's no direct way to retrieve it from the Animator, because
      // at runtime the actual AnimatorController doesn't exist! Only a runtime representation does. (That's why
      // AnimatorController is in the UnityEditor namespace.) But this *isn't* runtime, so when we retrieve the
      // runtime controller, it will actually be a reference to our real AnimatorController.
      // 우리는 AnimatorController가 필요하지만, Animator에서 직접적으로 이를 가져올 수는 없습니다.
      // 왜냐하면 실행 중에는 실제 AnimatorController가 존재하지 않기 때문입니다! 오직 실행 시간에 해당하는 표현만 존재합니다. (그래서
      // AnimatorController는 UnityEditor 네임스페이스에 있습니다.) 하지만 여기는 실행 시간이 아니므로,
      // 실행 중인 컨트롤러를 가져오면 실제 AnimatorController에 대한 참조가 반환됩니다.
      AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
      if (controller == null)
      {
        // if it's not an AnimatorController, it must be an AnimatorOverrideController (because those are currently the only two on-disk representations)
        // AnimatorController가 아니면, AnimatorOverrideController일 것입니다.
        var overrideController = animator.runtimeAnimatorController as AnimatorOverrideController;
        if (overrideController)
        {
          // override controllers are not allowed to be nested, so the thing it's overriding has to be our real AnimatorController
          // 오버라이드 컨트롤러는 중첩될 수 없으므로, 덮어쓰는 대상이 우리의 실제 AnimatorController여야 합니다.
          controller = overrideController.runtimeAnimatorController as AnimatorController;
        }
      }
      if (controller == null)
      {
        // It's neither of the two standard disk representations! ... it must be a new Unity feature or a custom variation
        // Either way, we don't know how to get the real AnimatorController out of it, so we have to stop
        // 두 가지 표준 디스크 표현 방식 중 하나가 아닙니다! ... 새로운 Unity 기능이거나 사용자 정의된 변형일 것입니다.
        // 어쨌든, 우리가 그것에서 실제 AnimatorController를 어떻게 가져올지 모르기 때문에, 그래서 중지해야 합니다.
        throw new System.Exception($"Unrecognized class derived from RuntimeAnimatorController! {animator.runtimeAnimatorController.GetType().FullName}");
      }
      return controller;
    }

  }
#endif
}
