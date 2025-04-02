using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.BossRoom.Editor
{
    /// <summary>
    /// Class that permits auto-loading a bootstrap scene when the editor switches play state. This class is
    /// initialized when Unity is opened and when scripts are recompiled. This is to be able to subscribe to
    /// EditorApplication's playModeStateChanged event, which is when we wish to open a new scene.
    /// </summary>
    /// <summary>
    /// 에디터가 플레이 상태를 변경할 때 자동으로 부트스트랩 씬을 로드할 수 있도록 하는 클래스입니다. 이 클래스는
    /// Unity가 열릴 때와 스크립트가 재컴파일될 때 초기화됩니다. 이는 EditorApplication의 playModeStateChanged 이벤트에
    /// 구독할 수 있도록 하기 위함입니다. 이 이벤트가 발생할 때 새 씬을 열기를 원합니다.
    /// </summary>
    /// <remarks>
    /// A critical edge case scenario regarding NetworkManager is accounted for here.
    /// A NetworkObject's GlobalObjectIdHash value is currently generated in OnValidate() which is invoked during a
    /// build and when the asset is loaded/viewed in the editor.
    /// If we were to manually open Bootstrap scene via EditorSceneManager.OpenScene(...) as the editor is exiting play
    /// mode, Bootstrap scene would be entering play mode within the editor prior to having loaded any assets, meaning
    /// NetworkManager itself has no entry within the AssetDatabase cache. As a result of this, any referenced Network
    /// Prefabs wouldn't have any entry either.
    /// To account for this necessary AssetDatabase step, whenever we're redirecting from a new scene, or a scene
    /// existing in our EditorBuildSettings, we forcefully stop the editor, open Bootstrap scene, and re-enter play
    /// mode. This provides the editor the chance to create AssetDatabase cache entries for the Network Prefabs assigned
    /// to the NetworkManager.
    /// If we are entering play mode directly from Bootstrap scene, no additional steps need to be taken and the scene
    /// is loaded normally.
    /// </remarks>
    /// <remarks>
    /// 여기서 NetworkManager와 관련된 중요한 엣지 케이스 시나리오를 다룹니다.
    /// NetworkObject의 GlobalObjectIdHash 값은 현재 OnValidate()에서 생성되며, 이는 빌드 중과 에디터에서
    /// 에셋이 로드되거나 뷰어로 열릴 때 호출됩니다.
    /// 만약 에디터가 플레이 모드를 종료하면서 EditorSceneManager.OpenScene(...)을 통해 부트스트랩 씬을 수동으로 연다면,
    /// 부트스트랩 씬은 에셋을 로드하기 전에 에디터 내에서 플레이 모드를 시작하게 되어, NetworkManager는
    /// AssetDatabase 캐시에 아무런 항목도 없게 됩니다. 이로 인해 참조된 네트워크 프리팹들도 항목이 없게 됩니다.
    /// 이 필수 AssetDatabase 단계를 처리하기 위해, 새 씬으로 리디렉션하거나 EditorBuildSettings에 있는 씬으로 리디렉션할 때마다
    /// 우리는 에디터를 강제로 종료하고, 부트스트랩 씬을 열고, 다시 플레이 모드로 진입합니다. 이렇게 하면 에디터가
    /// NetworkManager에 할당된 네트워크 프리팹에 대해 AssetDatabase 캐시 항목을 생성할 기회를 제공합니다.
    /// 만약 부트스트랩 씬에서 바로 플레이 모드로 진입하는 경우, 추가적인 단계를 수행할 필요 없이 씬이 정상적으로 로드됩니다.
    /// </remarks>

    [InitializeOnLoad]
    public class SceneBootstrapper
    {
        const string k_PreviousSceneKey = "PreviousScene";
        const string k_ShouldLoadBootstrapSceneKey = "LoadBootstrapScene";

        const string k_LoadBootstrapSceneOnPlay = "Boss Room/Load Bootstrap Scene On Play";
        const string k_DoNotLoadBootstrapSceneOnPlay = "Boss Room/Don't Load Bootstrap Scene On Play";

        const string k_TestRunnerSceneName = "InitTestScene";

        static bool s_RestartingToSwitchScene;

        static string BootstrapScene => EditorBuildSettings.scenes[0].path;

        // to track where to go back to
        // 돌아갈 위치를 추적하기 위해
        static string PreviousScene
        {
            get => EditorPrefs.GetString(k_PreviousSceneKey);
            set => EditorPrefs.SetString(k_PreviousSceneKey, value);
        }

        static bool ShouldLoadBootstrapScene
        {
            get
            {
                if (!EditorPrefs.HasKey(k_ShouldLoadBootstrapSceneKey))
                {
                    EditorPrefs.SetBool(k_ShouldLoadBootstrapSceneKey, true);
                }

                return EditorPrefs.GetBool(k_ShouldLoadBootstrapSceneKey, true);
            }
            set => EditorPrefs.SetBool(k_ShouldLoadBootstrapSceneKey, value);
        }

        static SceneBootstrapper()
        {
            EditorApplication.playModeStateChanged += EditorApplicationOnplayModeStateChanged;
        }

        [MenuItem(k_LoadBootstrapSceneOnPlay, true)]
        static bool ShowLoadBootstrapSceneOnPlay()
        {
            return !ShouldLoadBootstrapScene;
        }

        [MenuItem(k_LoadBootstrapSceneOnPlay)]
        static void EnableLoadBootstrapSceneOnPlay()
        {
            ShouldLoadBootstrapScene = true;
        }

        [MenuItem(k_DoNotLoadBootstrapSceneOnPlay, true)]
        static bool ShowDoNotLoadBootstrapSceneOnPlay()
        {
            return ShouldLoadBootstrapScene;
        }

        [MenuItem(k_DoNotLoadBootstrapSceneOnPlay)]
        static void DisableDoNotLoadBootstrapSceneOnPlay()
        {
            ShouldLoadBootstrapScene = false;
        }

        static void EditorApplicationOnplayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if (IsTestRunnerActive())
            {
                return;
            }

            if (!ShouldLoadBootstrapScene)
            {
                return;
            }

            if (s_RestartingToSwitchScene)
            {
                if (playModeStateChange == PlayModeStateChange.EnteredPlayMode)
                {
                    // for some reason there's multiple start and stops events happening while restarting the editor playmode. We're making sure to
                    // set stoppingAndStarting only when we're done and we've entered playmode. This way we won't corrupt "activeScene" with the multiple
                    // start and stop and will be able to return to the scene we were editing at first
                    // 어떤 이유로 인해 에디터의 플레이 모드를 재시작할 때 여러 번의 시작과 정지 이벤트가 발생합니다. 우리는 "stoppingAndStarting"을
                    // 완료된 후, 플레이모드로 진입했을 때만 설정하도록 보장합니다. 이렇게 하면 여러 번의 시작과 정지로 인해 "activeScene"이 손상되지 않고,
                    // 처음 편집하던 씬으로 돌아갈 수 있습니다.
                    s_RestartingToSwitchScene = false;
                }
                return;
            }

            if (playModeStateChange == PlayModeStateChange.ExitingEditMode)
            {
                // cache previous scene so we return to this scene after play session, if possible
                // 플레이 세션 후 이 씬으로 돌아갈 수 있도록 이전 씬을 캐시합니다.
                PreviousScene = EditorSceneManager.GetActiveScene().path;

                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    // user either hit "Save" or "Don't Save"; open bootstrap scene
                    // 사용자가 "저장" 또는 "저장 안 함"을 클릭한 경우; 부트스트랩 씬을 엽니다.

                    if (!string.IsNullOrEmpty(BootstrapScene) &&
                    System.Array.Exists(EditorBuildSettings.scenes, scene => scene.path == BootstrapScene))
                    {
                        var activeScene = EditorSceneManager.GetActiveScene();

                        s_RestartingToSwitchScene = activeScene.path == string.Empty || !BootstrapScene.Contains(activeScene.path);

                        // we only manually inject Bootstrap scene if we are in a blank empty scene,
                        // or if the active scene is not already BootstrapScene
                        // 빈 씬에 있거나 활성 씬이 이미 부트스트랩 씬이 아닐 경우에만 부트스트랩 씬을 수동으로 삽입합니다.
                        if (s_RestartingToSwitchScene)
                        {
                            EditorApplication.isPlaying = false;

                            // scene is included in build settings; open it
                            // 씬이 빌드 설정에 포함되어 있으면 씬을 엽니다.
                            EditorSceneManager.OpenScene(BootstrapScene);

                            EditorApplication.isPlaying = true;
                        }
                    }
                }
                else
                {
                    // user either hit "Cancel" or exited window; don't open bootstrap scene & return to editor
                    // 사용자가 "취소"를 클릭했거나 창을 닫은 경우; 부트스트랩 씬을 열지 않고 에디터로 돌아갑니다.
                    EditorApplication.isPlaying = false;
                }
            }
            else if (playModeStateChange == PlayModeStateChange.EnteredEditMode)
            {
                if (!string.IsNullOrEmpty(PreviousScene))
                {
                    EditorSceneManager.OpenScene(PreviousScene);
                }
            }
        }

        static bool IsTestRunnerActive()
        {
            return EditorSceneManager.GetActiveScene().name.StartsWith(k_TestRunnerSceneName);
        }
    }
}