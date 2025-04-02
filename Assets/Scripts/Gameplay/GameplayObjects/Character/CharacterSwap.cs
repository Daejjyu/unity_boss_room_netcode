using System;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects.AnimationCallbacks;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
  /// <summary>
  /// Responsible for storing of all of the pieces of a character, 
  /// and swapping the pieces out en masse when asked to.
  /// </summary>
  /// <summary>
  /// 캐릭터의 모든 조각들을 저장하고, 
  /// 요청 시 한 번에 모든 조각을 교체하는 역할을 합니다.
  /// </summary>
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
      private List<Renderer> m_CachedRenderers;

      public void SetFullActive(bool isActive)
      {
        ears.SetActive(isActive);
        head.SetActive(isActive);
        mouth.SetActive(isActive);
        hair.SetActive(isActive);
        eyes.SetActive(isActive);
        torso.SetActive(isActive);
        gearLeftHand.SetActive(isActive);
        gearRightHand.SetActive(isActive);
        handRight.SetActive(isActive);
        handLeft.SetActive(isActive);
        shoulderRight.SetActive(isActive);
        shoulderLeft.SetActive(isActive);
      }

      public List<Renderer> GetAllBodyParts()
      {
        if (m_CachedRenderers == null)
        {
          m_CachedRenderers = new List<Renderer>();
          AddRenderer(ref m_CachedRenderers, ears);
          AddRenderer(ref m_CachedRenderers, head);
          AddRenderer(ref m_CachedRenderers, mouth);
          AddRenderer(ref m_CachedRenderers, hair);
          AddRenderer(ref m_CachedRenderers, torso);
          AddRenderer(ref m_CachedRenderers, gearRightHand);
          AddRenderer(ref m_CachedRenderers, gearLeftHand);
          AddRenderer(ref m_CachedRenderers, handRight);
          AddRenderer(ref m_CachedRenderers, handLeft);
          AddRenderer(ref m_CachedRenderers, shoulderRight);
          AddRenderer(ref m_CachedRenderers, shoulderLeft);
        }
        return m_CachedRenderers;
      }

      private void AddRenderer(ref List<Renderer> rendererList, GameObject bodypartGO)
      {
        if (!bodypartGO) { return; }
        var bodyPartRenderer = bodypartGO.GetComponent<Renderer>();
        if (!bodyPartRenderer) { return; }
        rendererList.Add(bodyPartRenderer);
      }

    }

    [SerializeField]
    CharacterModelSet m_CharacterModel;

    public CharacterModelSet CharacterModel => m_CharacterModel;

    /// <summary>
    /// Reference to our shared-characters' animator.
    /// Can be null, but if so, animator overrides are not supported!
    /// </summary>
    /// <summary>
    /// 공유된 캐릭터들의 애니메이터에 대한 참조입니다.
    /// null일 수 있지만, 그렇다면 애니메이터 오버라이드는 지원되지 않습니다!
    /// </summary>
    [SerializeField]
    private Animator m_Animator;

    /// <summary>
    /// Reference to the original controller in our Animator.
    /// We switch back to this whenever we don't have an Override.
    /// </summary>
    /// <summary>
    /// 애니메이터에서 원본 컨트롤러에 대한 참조입니다.
    /// 오버라이드가 없을 때마다 이 컨트롤러로 돌아갑니다.
    /// </summary>
    private RuntimeAnimatorController m_OriginalController;

    [SerializeField]
    [Tooltip("Special Material we plug in when the local player is \"stealthy\"")]
    private Material m_StealthySelfMaterial;

    [SerializeField]
    [Tooltip("Special Material we plug in when another player is \"stealthy\"")]
    private Material m_StealthyOtherMaterial;

    public enum SpecialMaterialMode
    {
      None,
      StealthySelf,
      StealthyOther,
    }

    /// <summary>
    /// When we swap all our Materials out for a special material,
    /// we keep the old references here, so we can swap them back.
    /// </summary>
    /// <summary>
    /// 모든 재질을 특별한 재질로 교체할 때,
    /// 이전 재질 참조를 여기 보관하여 다시 교체할 수 있습니다.
    /// </summary>
    private Dictionary<Renderer, Material> m_OriginalMaterials = new Dictionary<Renderer, Material>();

    ClientCharacter m_ClientCharacter;

    void Awake()
    {
      m_ClientCharacter = GetComponentInParent<ClientCharacter>();
      m_Animator = m_ClientCharacter.OurAnimator;
      m_OriginalController = m_Animator.runtimeAnimatorController;
    }

    private void OnDisable()
    {
      // It's important that the original Materials that we pulled out of the renderers are put back.
      // Otherwise nothing will Destroy() them and they will leak! (Alternatively we could manually
      // Destroy() these in our OnDestroy(), but in this case it makes more sense just to put them back.)
      // 렌더러에서 뽑아낸 원본 재질들이 다시 넣어지는 것이 중요합니다.
      // 그렇지 않으면 아무것도 이 재질들을 Destroy()하지 않아서 메모리 누수가 발생할 수 있습니다! (대안으로는
      // OnDestroy()에서 이들을 수동으로 Destroy()할 수 있지만, 이 경우 재질을 다시 넣는 것이 더 합리적입니다.)
      ClearOverrideMaterial();
    }


    /// <summary>
    /// Swap the visuals of the character to the index passed in.
    /// </summary>
    /// <param name="specialMaterialMode">Special Material to apply to all body parts</param>
    /// /// <summary>
    /// 캐릭터의 비주얼을 전달된 인덱스에 맞게 교체합니다.
    /// </summary>
    /// <param name="specialMaterialMode">모든 신체 부위에 적용할 특별한 재질</param>
    public void SwapToModel(SpecialMaterialMode specialMaterialMode = SpecialMaterialMode.None)
    {
      ClearOverrideMaterial();

      if (m_CharacterModel.specialFx)
      {
        m_CharacterModel.specialFx.enabled = true;
      }

      if (m_Animator)
      {
        // plug in the correct animator override... or plug the original non - overridden version back in!
        // 올바른 애니메이터 오버라이드를 연결하거나, 원본 오버라이드되지 않은 버전을 다시 연결합니다!
        if (m_CharacterModel.animatorOverrides)
        {
          m_Animator.runtimeAnimatorController = m_CharacterModel.animatorOverrides;
        }
        else
        {
          m_Animator.runtimeAnimatorController = m_OriginalController;
        }
      }

      // lastly, now that we're all assembled, apply any override material.
      // 마지막으로, 이제 모든 것이 조립되었으므로, 오버라이드된 재질을 적용합니다.
      switch (specialMaterialMode)
      {
        case SpecialMaterialMode.StealthySelf:
          SetOverrideMaterial(m_StealthySelfMaterial);
          break;
        case SpecialMaterialMode.StealthyOther:
          SetOverrideMaterial(m_StealthyOtherMaterial);
          break;
      }
    }

    private void ClearOverrideMaterial()
    {
      foreach (var entry in m_OriginalMaterials)
      {
        if (entry.Key)
        {
          entry.Key.material = entry.Value;
        }
      }
      m_OriginalMaterials.Clear();
    }

    private void SetOverrideMaterial(Material overrideMaterial)
    {
      // just sanity-checking; this should already have been called!
      // 그냥 정상 확인 중; 이 함수는 이미 호출되었어야 합니다!
      ClearOverrideMaterial();
      foreach (var bodyPart in m_CharacterModel.GetAllBodyParts())
      {
        if (bodyPart)
        {
          m_OriginalMaterials[bodyPart] = bodyPart.material;
          bodyPart.material = overrideMaterial;
        }
      }
    }
  }
}
