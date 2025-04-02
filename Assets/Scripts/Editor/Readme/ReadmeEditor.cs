using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Unity.BossRoom.Editor
{
    /// <remarks>
    /// Custom readme editor window based on the readme created for URP. For more context, see:
    /// https://github.com/Unity-Technologies/Graphics/tree/master/com.unity.template-universal
    /// </remarks>
    /// <remarks>
    /// URP용으로 생성된 README를 기반으로 한 사용자 지정 에디터 창입니다. 더 많은 정보는 다음을 참조하십시오:
    /// https://github.com/Unity-Technologies/Graphics/tree/master/com.unity.template-universal
    /// </remarks>
    [CustomEditor(typeof(Readme))]
    [InitializeOnLoad]
    public class ReadmeEditor : UnityEditor.Editor
    {
        const string k_ShowedReadmeSessionStateName = "ReadmeEditor.showedReadme";

        const float k_Space = 16f;

        bool m_Initialized;

        [SerializeField]
        GUIStyle m_LinkStyle;

        GUIStyle LinkStyle
        {
            get { return m_LinkStyle; }
        }

        [SerializeField]
        GUIStyle m_TitleStyle;

        GUIStyle TitleStyle
        {
            get { return m_TitleStyle; }
        }

        [SerializeField]
        GUIStyle m_HeadingStyle;

        GUIStyle HeadingStyle
        {
            get { return m_HeadingStyle; }
        }

        [SerializeField]
        GUIStyle m_BodyStyle;

        GUIStyle BodyStyle
        {
            get { return m_BodyStyle; }
        }

        static ReadmeEditor()
        {
            EditorApplication.delayCall += SelectReadmeAutomatically;
        }

        /// <summary>
        /// 자동으로 README를 선택하는 함수
        /// </summary>
        static void SelectReadmeAutomatically()
        {
            if (!SessionState.GetBool(k_ShowedReadmeSessionStateName, false))
            {
                var readme = SelectReadme();
                SessionState.SetBool(k_ShowedReadmeSessionStateName, true);

                if (readme && !readme.loadedLayout)
                {
                    LoadLayout();
                    readme.loadedLayout = true;
                }
            }
        }

        /// <summary>
        /// 레이아웃을 로드하는 함수
        /// </summary>
        static void LoadLayout()
        {
            var assembly = typeof(EditorApplication).Assembly;
            var windowLayoutType = assembly.GetType("UnityEditor.WindowLayout", true);
            var method = windowLayoutType.GetMethod("LoadWindowLayout", BindingFlags.Public | BindingFlags.Static);
            method?.Invoke(null, new object[] { Path.Combine(Application.dataPath, "TutorialInfo/Layout.wlt"), false });
        }

        /// <summary>
        /// 메뉴 항목에서 README를 선택하는 함수
        /// </summary>
        /// <returns>선택된 Readme 객체</returns>
        [MenuItem("Boss Room/Show Sample Instructions")]
        static Readme SelectReadme()
        {
            var ids = AssetDatabase.FindAssets("Readme t:Readme");
            if (ids.Length == 1)
            {
                var readmeObject = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(ids[0]));

                Selection.objects = new UnityEngine.Object[] { readmeObject };

                return (Readme)readmeObject;
            }
            else
            {
                Debug.Log("Couldn't find a readme");
                return null;
            }
        }

        protected override void OnHeaderGUI()
        {
            var readme = (Readme)target;
            Init();

            var iconWidth = Mathf.Min(EditorGUIUtility.currentViewWidth / 3f - 20f, 128f);

            GUILayout.BeginHorizontal("In BigTitle");
            {
                GUILayout.Label(readme.icon, GUILayout.Width(iconWidth), GUILayout.Height(iconWidth));
                GUILayout.Label(readme.title, TitleStyle);
            }
            GUILayout.EndHorizontal();
        }

        public override void OnInspectorGUI()
        {
            var readme = (Readme)target;
            Init();

            foreach (var section in readme.sections)
            {
                if (!string.IsNullOrEmpty(section.heading))
                {
                    GUILayout.Label(section.heading, HeadingStyle);
                }

                if (!string.IsNullOrEmpty(section.text))
                {
                    GUILayout.Label(section.text, BodyStyle);
                }

                if (!string.IsNullOrEmpty(section.linkText))
                {
                    if (LinkLabel(new GUIContent(section.linkText)))
                    {
                        Application.OpenURL(section.url);
                    }
                }

                GUILayout.Space(k_Space);
            }
        }

        void Init()
        {
            if (m_Initialized)
                return;
            m_BodyStyle = new GUIStyle(EditorStyles.label);
            m_BodyStyle.wordWrap = true;
            m_BodyStyle.fontSize = 14;

            m_TitleStyle = new GUIStyle(m_BodyStyle);
            m_TitleStyle.fontSize = 26;

            m_HeadingStyle = new GUIStyle(m_BodyStyle);
            m_HeadingStyle.fontSize = 18;

            m_LinkStyle = new GUIStyle(m_BodyStyle);
            m_LinkStyle.wordWrap = false;

            // Match selection color which works nicely for both light and dark skins
            // 라이트 및 다크 스킨 모두에 잘 작동하는 선택 색상 일치
            m_LinkStyle.normal.textColor = new Color(0x00 / 255f, 0x78 / 255f, 0xDA / 255f, 1f);
            m_LinkStyle.stretchWidth = false;

            m_Initialized = true;
        }

        bool LinkLabel(GUIContent label, params GUILayoutOption[] options)
        {
            var position = GUILayoutUtility.GetRect(label, LinkStyle, options);

            Handles.BeginGUI();
            Handles.color = LinkStyle.normal.textColor;
            Handles.DrawLine(new Vector3(position.xMin, position.yMax), new Vector3(position.xMax, position.yMax));
            Handles.color = Color.white;
            Handles.EndGUI();

            EditorGUIUtility.AddCursorRect(position, MouseCursor.Link);

            return GUI.Button(position, label, LinkStyle);
        }
    }
}
