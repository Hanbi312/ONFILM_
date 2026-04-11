using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TabButtonGroup - 탭 버튼 그룹
/// 버튼 클릭 시 해당 버튼은 선택 이미지, 나머지는 기본 이미지로 변경
/// </summary>
public class TabButtonGroup : MonoBehaviour
{
    [System.Serializable]
    public class TabButton
    {
        public Button button;
        public Image buttonImage;
    }

    [Header("탭 버튼 목록")]
    [SerializeField] private TabButton[] tabButtons;

    [Header("이미지")]
    [SerializeField] private Sprite normalSprite;   // 기본 이미지
    [SerializeField] private Sprite selectedSprite; // 선택된 이미지

    private void Start()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i;
            tabButtons[i].button.onClick.AddListener(() => OnTabClicked(index));
        }

        // 첫 번째 버튼 선택 상태로 초기화
        if (tabButtons.Length > 0)
            OnTabClicked(0);
    }

    public void OnTabClicked(int selectedIndex)
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i].buttonImage == null) continue;
            tabButtons[i].buttonImage.sprite = i == selectedIndex ? selectedSprite : normalSprite;
        }
    }
}
