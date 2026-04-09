using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ResultUIController - 결과 씬 UI
/// 연기자 승리 → 해피엔딩
/// 악역 승리 → 베드엔딩
/// </summary>
public class ResultUIController : MonoBehaviour
{
    [Header("결과 패널")]
    [SerializeField] private GameObject happyEndingPanel;  // 해피엔딩 패널
    [SerializeField] private GameObject badEndingPanel;    // 베드엔딩 패널

    [Header("텍스트")]
    [SerializeField] private TMP_Text happyEndingText;
    [SerializeField] private TMP_Text badEndingText;

    private void Start()
    {
        if (ResultData.IsActorWin)
        {
            // 연기자 승리 - 해피엔딩
            if (happyEndingPanel != null) happyEndingPanel.SetActive(true);
            if (badEndingPanel != null) badEndingPanel.SetActive(false);
            if (happyEndingText != null) happyEndingText.text = "해피엔딩";
            Debug.Log("[ResultUIController] 해피엔딩!");
        }
        else
        {
            // 악역 승리 - 베드엔딩
            if (happyEndingPanel != null) happyEndingPanel.SetActive(false);
            if (badEndingPanel != null) badEndingPanel.SetActive(true);
            if (badEndingText != null) badEndingText.text = "베드엔딩";
            Debug.Log("[ResultUIController] 베드엔딩!");
        }
    }

    // 다시하기 버튼
    public void OnClickReplay()
    {
        SceneManager.LoadScene("LobbyScene");
    }

    // 메인으로 버튼
    public void OnClickMainMenu()
    {
        SceneManager.LoadScene("InfoScene");
    }
}
