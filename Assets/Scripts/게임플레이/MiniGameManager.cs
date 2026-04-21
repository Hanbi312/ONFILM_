using UnityEngine;

/// <summary>
/// MiniGameManager
///
/// [UI 구조]
///   Canvas
///     ├── miniGamePanel      - F키 누르면 항상 표시
///     │     ├── Game Panel Bg  - 랜덤 트리거 시에만 표시
///     │     └── GamePanel      - 랜덤 트리거 시에만 표시
///     └── CameraProgressBar  - F키 누르면 항상 표시
/// </summary>
public class MiniGameManager : MonoBehaviour
{
    public static MiniGameManager Instance;

    [Header("Inspector에서 직접 연결")]
    public GameObject miniGamePanel;     // F키 누르면 항상 표시
    public GameObject gamePanel;         // 랜덤 트리거 시에만 표시
    public GameObject gamePanelBg;       // 랜덤 트리거 시에만 표시 (gamePanel과 동시)
    public GameObject cameraProgressBar; // F키 누르면 항상 표시
    public PoseGame poseGameScript;
    public ProgressBar progressBarScript;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (miniGamePanel != null) miniGamePanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(false);
        if (gamePanelBg != null) gamePanelBg.SetActive(false);
        if (cameraProgressBar != null) cameraProgressBar.SetActive(false);

        if (miniGamePanel == null) Debug.LogError("[MiniGameManager] miniGamePanel 연결 필요!");
        if (gamePanel == null) Debug.LogError("[MiniGameManager] gamePanel 연결 필요!");
        if (gamePanelBg == null) Debug.LogError("[MiniGameManager] gamePanelBg 연결 필요!");
        if (cameraProgressBar == null) Debug.LogError("[MiniGameManager] cameraProgressBar 연결 필요!");
        if (poseGameScript == null) Debug.LogError("[MiniGameManager] poseGameScript 연결 필요!");
    }

    // F 눌렀을 때 - miniGamePanel + cameraProgressBar 표시
    // GamePanel / GamePanelBg는 아직 숨김 (랜덤 트리거 대기 중)
    public void ShowWorkingUI()
    {
        if (miniGamePanel != null) miniGamePanel.SetActive(true);
        if (cameraProgressBar != null) cameraProgressBar.SetActive(true);
        if (gamePanel != null) gamePanel.SetActive(false);
        if (gamePanelBg != null) gamePanelBg.SetActive(false);
    }

    // 랜덤 트리거 - GamePanel + GamePanelBg 추가 표시
    // miniGamePanel / cameraProgressBar는 이미 켜져 있음
    public void ShowMiniGame()
    {
        if (miniGamePanel != null) miniGamePanel.SetActive(true);
        if (cameraProgressBar != null) cameraProgressBar.SetActive(true);
        if (gamePanel != null) gamePanel.SetActive(true);
        if (gamePanelBg != null) gamePanelBg.SetActive(true);
    }

    // 랜덤 미니게임 종료 - GamePanel + GamePanelBg만 숨김
    // miniGamePanel / cameraProgressBar는 계속 표시
    public void HideMiniGame()
    {
        if (gamePanel != null) gamePanel.SetActive(false);
        if (gamePanelBg != null) gamePanelBg.SetActive(false);
    }

    // 전부 닫기 (작업 완료 / 취소)
    public void HideAll()
    {
        if (miniGamePanel != null) miniGamePanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(false);
        if (gamePanelBg != null) gamePanelBg.SetActive(false);
        if (cameraProgressBar != null) cameraProgressBar.SetActive(false);
    }

    // SecurityCamera 연결
    public void RegisterCamera(SecurityCamera cam)
    {
        if (progressBarScript != null)
            progressBarScript.SetSecurityCamera(cam);
    }

    // VillainCamera 연결
    public void RegisterVillainCamera(VillainCamera cam)
    {
        if (progressBarScript != null)
            progressBarScript.SetVillainCamera(cam);
    }
}