using System;
using UnityEngine;

// [CreateAssetMenu(fileName = "README", menuName = "Scriptable Object/README", order = int.MaxValue)]
public class Readme : ScriptableObject
{
    public Texture2D icon;          // 아이콘 이미지
    public string title;            // 제목
    public Section[] sections;      // 여러 섹션 (각 섹션은 Section 클래스 사용)
    public bool loadedLayout;       // 레이아웃이 로드됐는지 여부

    [Serializable]
    public class Section
    {
        public string heading;      // 섹션의 제목
        public string text;         // 섹션의 내용
        public string linkText;     // 링크 텍스트
        public string url;          // 링크 URL
    }
}
