using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Utility menus to easily create our builds for our playtests. If you're just exploring this project, you shouldn't need those. They are mostly to make
/// multiplatform build creation easier and is meant for internal usage.
/// </summary>
/// <summary>
/// 플레이테스트를 위한 빌드를 쉽게 생성할 수 있는 유틸리티 메뉴입니다. 이 프로젝트를 탐색 중이라면 이 메뉴가 필요하지 않을 수 있습니다.
/// 주로 멀티플랫폼 빌드 생성을 더 쉽게 만들기 위한 내부 용도로 사용됩니다.
/// </summary>
internal static class BuildHelpers
{
    const string k_MenuRoot = "Boss Room/Playtest Builds/";
    const string k_Build = k_MenuRoot + "Build";
    const string k_DeleteBuilds = k_MenuRoot + "Delete All Builds (keeps cache)";
    const string k_AllToggleName = k_MenuRoot + "Toggle All";
    const string k_MobileToggleName = k_MenuRoot + "Toggle Mobile";
    const string k_IOSToggleName = k_MenuRoot + "Toggle iOS";
    const string k_AndroidToggleName = k_MenuRoot + "Toggle Android";
    const string k_DesktopToggleName = k_MenuRoot + "Toggle Desktop";
    const string k_MacOSToggleName = k_MenuRoot + "Toggle MacOS";
    const string k_WindowsToggleName = k_MenuRoot + "Toggle Windows";
    const string k_DisableProjectIDToggleName = k_MenuRoot + "Skip Project ID Check"; // double negative in the name since menu is unchecked by default
    // 기본적으로 메뉴가 선택되지 않으므로 이름에 이중 부정이 포함되어 있습니다.
    const string k_SkipAutoDeleteToggleName = k_MenuRoot + "Skip Auto Delete Builds";

    const int k_MenuGroupingBuild = 0; // to add separator in menus
    // 메뉴에 구분선을 추가하기 위한 그룹화
    const int k_MenuGroupingPlatforms = 11;
    const int k_MenuGroupingOtherToggles = 22;

    static BuildTarget s_CurrentEditorBuildTarget;
    static BuildTargetGroup s_CurrentEditorBuildTargetGroup;
    static int s_NbBuildsDone;

    static string BuildPathRootDirectory => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", "Playtest");
    // 빌드의 루트 디렉터리 경로를 반환합니다.
    static string BuildPathDirectory(string platformName) => Path.Combine(BuildPathRootDirectory, platformName);
    // 특정 플랫폼 이름에 대한 빌드 디렉터리 경로를 반환합니다.
    public static string BuildPath(string platformName) => Path.Combine(BuildPathDirectory(platformName), "BossRoomPlaytest");
    // 특정 플랫폼 이름에 대한 빌드 경로를 반환합니다.

    [MenuItem(k_Build, false, k_MenuGroupingBuild)]
    static async void Build()
    {
        s_NbBuildsDone = 0;
        bool buildiOS = Menu.GetChecked(k_IOSToggleName);
        bool buildAndroid = Menu.GetChecked(k_AndroidToggleName);
        bool buildMacOS = Menu.GetChecked(k_MacOSToggleName);
        bool buildWindows = Menu.GetChecked(k_WindowsToggleName);

        bool skipAutoDelete = Menu.GetChecked(k_SkipAutoDeleteToggleName);

        Debug.Log($"Starting build: buildiOS?:{buildiOS} buildAndroid?:{buildAndroid} buildMacOS?:{buildMacOS} buildWindows?:{buildWindows}");
        // 빌드 시작 로그 출력: iOS, Android, MacOS, Windows 빌드 여부

        if (string.IsNullOrEmpty(CloudProjectSettings.projectId) && !Menu.GetChecked(k_DisableProjectIDToggleName))
        {
            string errorMessage = $"Project ID was supposed to be setup and wasn't, make sure to set it up or disable project ID check with the [{k_DisableProjectIDToggleName}] menu";
            // 프로젝트 ID가 설정되지 않았을 경우 오류 메시지 출력
            EditorUtility.DisplayDialog("Error Custom Build", errorMessage, "ok");
            throw new Exception(errorMessage);
        }

        SaveCurrentBuildTarget();

        try
        {
            // deleting so we don't end up testing on outdated builds if there's a build failure
            // 빌드 실패 시 오래된 빌드를 테스트하지 않도록 삭제
            if (!skipAutoDelete) DeleteBuilds();

            if (buildiOS) await BuildPlayerUtilityAsync(BuildTarget.iOS, "", true);
            if (buildAndroid) await BuildPlayerUtilityAsync(BuildTarget.Android, ".apk", true); // there's the possibility of an error where it
            // Android 빌드 시 NDK 누락 오류가 발생할 가능성이 있습니다. 수동으로 빌드한 후 다시 시도하면 해결될 수 있습니다.

            if (buildMacOS) await BuildPlayerUtilityAsync(BuildTarget.StandaloneOSX, ".app", true);
            if (buildWindows) await BuildPlayerUtilityAsync(BuildTarget.StandaloneWindows64, ".exe", true);
        }
        catch
        {
            EditorUtility.DisplayDialog("Exception while building", "See console for details", "ok");
            // 빌드 중 예외 발생 시 대화 상자 표시
            throw;
        }
        finally
        {
            Debug.Log($"Count builds done: {s_NbBuildsDone}");
            // 완료된 빌드 수 로그 출력
            RestoreBuildTarget();
        }
    }

    [MenuItem(k_Build, true)]
    static bool CanBuild()
    {
        return Menu.GetChecked(k_IOSToggleName) ||
            Menu.GetChecked(k_AndroidToggleName) ||
            Menu.GetChecked(k_MacOSToggleName) ||
            Menu.GetChecked(k_WindowsToggleName);
        // 빌드 가능 여부 확인
    }

    static void RestoreBuildTarget()
    {
        Debug.Log($"restoring editor to initial build target {s_CurrentEditorBuildTarget}");
        // 초기 빌드 대상 복원 로그 출력
        EditorUserBuildSettings.SwitchActiveBuildTarget(s_CurrentEditorBuildTargetGroup, s_CurrentEditorBuildTarget);
    }

    static void SaveCurrentBuildTarget()
    {
        s_CurrentEditorBuildTarget = EditorUserBuildSettings.activeBuildTarget;
        s_CurrentEditorBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        // 현재 빌드 대상 저장
    }

    [MenuItem(k_AllToggleName, false, k_MenuGroupingPlatforms)]
    static void ToggleAll()
    {
        var newValue = ToggleMenu(k_AllToggleName);
        ToggleMenu(k_DesktopToggleName, newValue);
        ToggleMenu(k_MacOSToggleName, newValue);
        ToggleMenu(k_WindowsToggleName, newValue);
        ToggleMenu(k_MobileToggleName, newValue);
        ToggleMenu(k_IOSToggleName, newValue);
        ToggleMenu(k_AndroidToggleName, newValue);
        // 모든 플랫폼 토글 상태 변경
    }

    [MenuItem(k_MobileToggleName, false, k_MenuGroupingPlatforms)]
    static void ToggleMobile()
    {
        var newValue = ToggleMenu(k_MobileToggleName);
        ToggleMenu(k_IOSToggleName, newValue);
        ToggleMenu(k_AndroidToggleName, newValue);
        // 모바일 플랫폼 토글 상태 변경
    }

    [MenuItem(k_IOSToggleName, false, k_MenuGroupingPlatforms)]
    static void ToggleiOS()
    {
        ToggleMenu(k_IOSToggleName);
        // iOS 토글 상태 변경
    }

    [MenuItem(k_AndroidToggleName, false, k_MenuGroupingPlatforms)]
    static void ToggleAndroid()
    {
        ToggleMenu(k_AndroidToggleName);
        // Android 토글 상태 변경
    }

    [MenuItem(k_DesktopToggleName, false, k_MenuGroupingPlatforms)]
    static void ToggleDesktop()
    {
        var newValue = ToggleMenu(k_DesktopToggleName);
        ToggleMenu(k_MacOSToggleName, newValue);
        ToggleMenu(k_WindowsToggleName, newValue);
        // 데스크톱 플랫폼 토글 상태 변경
    }

    [MenuItem(k_MacOSToggleName, false, k_MenuGroupingPlatforms)]
    static void ToggleMacOS()
    {
        ToggleMenu(k_MacOSToggleName);
        // MacOS 토글 상태 변경
    }

    [MenuItem(k_WindowsToggleName, false, k_MenuGroupingPlatforms)]
    static void ToggleWindows()
    {
        ToggleMenu(k_WindowsToggleName);
        // Windows 토글 상태 변경
    }

    [MenuItem(k_DisableProjectIDToggleName, false, k_MenuGroupingOtherToggles)]
    static void ToggleProjectID()
    {
        ToggleMenu(k_DisableProjectIDToggleName);
        // 프로젝트 ID 확인 토글 상태 변경
    }

    [MenuItem(k_SkipAutoDeleteToggleName, false, k_MenuGroupingOtherToggles)]
    static void ToggleAutoDelete()
    {
        ToggleMenu(k_SkipAutoDeleteToggleName);
        // 자동 삭제 건너뛰기 토글 상태 변경
    }

    static bool ToggleMenu(string menuName, bool? valueToSet = null)
    {
        bool toSet = !Menu.GetChecked(menuName);
        if (valueToSet != null)
        {
            toSet = valueToSet.Value;
        }

        Menu.SetChecked(menuName, toSet);
        return toSet;
        // 메뉴 토글 상태 변경
    }

    static async Task BuildPlayerUtilityAsync(BuildTarget buildTarget = BuildTarget.NoTarget, string buildPathExtension = null, bool buildDebug = false)
    {
        s_NbBuildsDone++;
        Debug.Log($"Starting build for {buildTarget.ToString()}");
        // 특정 빌드 대상에 대한 빌드 시작 로그 출력

        await Task.Delay(100); // skipping some time to make sure debug logs are flushed before we build
        // 디버그 로그가 플러시되도록 약간의 대기 시간 추가

        var buildPathToUse = BuildPath(buildTarget.ToString());
        buildPathToUse += buildPathExtension;

        var buildPlayerOptions = new BuildPlayerOptions();

        List<string> scenesToInclude = new List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                scenesToInclude.Add(scene.path);
            }
        }

        buildPlayerOptions.scenes = scenesToInclude.ToArray();
        buildPlayerOptions.locationPathName = buildPathToUse;
        buildPlayerOptions.target = buildTarget;
        var buildOptions = BuildOptions.None;
        if (buildDebug)
        {
            buildOptions |= BuildOptions.Development;
        }

        buildOptions |= BuildOptions.StrictMode;
        buildPlayerOptions.options = buildOptions;

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build succeeded: {summary.totalSize} bytes at {summary.outputPath}");
            // 빌드 성공 로그 출력
        }
        else
        {
            string debugString = buildDebug ? "debug" : "release";
            throw new Exception($"Build failed for {debugString}:{buildTarget}! {report.summary.totalErrors} errors");
            // 빌드 실패 시 예외 발생
        }
    }

    [MenuItem(k_DeleteBuilds, false, k_MenuGroupingBuild)]
    public static void DeleteBuilds()
    {
        if (Directory.Exists(BuildPathRootDirectory))
        {
            Directory.Delete(BuildPathRootDirectory, recursive: true);
            Debug.Log($"deleted {BuildPathRootDirectory}");
            // 빌드 디렉터리 삭제 로그 출력
        }
        else
        {
            Debug.Log($"Build directory does not exist ({BuildPathRootDirectory}). No cleanup to do");
            // 빌드 디렉터리가 존재하지 않을 경우 로그 출력
        }
    }
}
