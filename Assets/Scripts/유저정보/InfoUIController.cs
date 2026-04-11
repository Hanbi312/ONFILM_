using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// InfoUIController - 인포 씬 UI 컨트롤러
/// 로그인 후 표시되는 메인 화면
/// </summary>
public class InfoUIController : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject mainPanel;       // 현재 메인 패널
    [SerializeField] private GameObject characterPanel;  // 캐릭터 버튼 클릭 시 표시할 패널

    [Header("UI")]
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text welcomeText;

    private void Start()
    {
        // 커서 복구 (게임플레이 씬에서 잠길 수 있으므로)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 닉네임 표시
        if (PlayerSession.Instance != null && PlayerSession.Instance.IsLoggedIn)
        {
            string nickname = PlayerSession.Instance.Nickname;
            if (nicknameText != null)
                nicknameText.text = nickname;
            if (welcomeText != null)
                welcomeText.text = $"환영합니다, {nickname}님!";
        }

        // 초기 상태: 메인 패널만 활성화
        if (mainPanel != null) mainPanel.SetActive(true);
        if (characterPanel != null) characterPanel.SetActive(false);
    }

    // 캐릭터 버튼 클릭 → 메인 패널 비활성화, 캐릭터 패널 활성화
    public void OnClickCharacter()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (characterPanel != null) characterPanel.SetActive(true);
    }

    // 캐릭터 패널에서 뒤로가기 → 메인 패널 복귀
    public void OnClickBack()
    {
        if (characterPanel != null) characterPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    // 게임 시작 버튼 → LobbyScene 이동
    public void OnClickStartGame()
    {
        SceneManager.LoadScene("LobbyScene");
    }

    // 나가기 버튼 → 게임 종료
    public void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}