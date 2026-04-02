using UnityEngine;

/// <summary>
/// MiniGameManager - 씬에 빈 오브젝트로 배치
/// Inspector에서 직접 연결
/// 
/// [UI 구조]
///   miniGamePanel (반투명 배경) - F 누르면 활성화
///     ├── CameraProgressBar (진행바) - F 누르면 활성화
///     └── GamePanel (미니게임 UI) - 랜덤 타이밍에 활성화
/// </summary>
public class MiniGameManager : MonoBehaviour
{
    public static MiniGameManager Instance;

    [Header("Inspector에서 직접 연결")]
    public GameObject miniGamePanel;        // Canvas > miniGamePanel
    public GameObject gamePanel;            // Canvas > miniGamePanel > GamePanel
    public GameObject cameraProgressBar;    // Canvas > miniGamePanel > CameraProgressBar
    public PoseGame poseGameScript;         // GamePanel에 붙은 PoseGame 스크립트
    public ProgressBar progressBarScript;   // CameraProgressBar에 붙은 ProgressBar 스크립트

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 시작 시 전부 비활성화
        if (miniGamePanel != null) miniGamePanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(false);
        if (cameraProgressBar != null) cameraProgressBar.SetActive(false);

        if (miniGamePanel == null) Debug.LogError("[MiniGameManager] miniGamePanel 연결 필요!");
        if (gamePanel == null) Debug.LogError("[MiniGameManager] gamePanel 연결 필요!");
        if (cameraProgressBar == null) Debug.LogError("[MiniGameManager] cameraProgressBar 연결 필요!");
        if (poseGameScript == null) Debug.LogError("[MiniGameManager] poseGameScript 연결 필요!");
    }

    // F 눌렀을 때 - 반투명 배경 + 진행바 표시
    public void ShowWorkingUI()
    {
        if (miniGamePanel != null) miniGamePanel.SetActive(true);
        if (cameraProgressBar != null) cameraProgressBar.SetActive(true);
        if (gamePanel != null) gamePanel.SetActive(false);
    }

    // 랜덤 타이밍 - 미니게임 UI 추가로 표시
    public void ShowMiniGame()
    {
        if (miniGamePanel != null) miniGamePanel.SetActive(true);
        if (cameraProgressBar != null) cameraProgressBar.SetActive(true);
        if (gamePanel != null) gamePanel.SetActive(true);
    }

    // 미니게임만 닫기 (진행바는 유지)
    public void HideMiniGame()
    {
        if (gamePanel != null) gamePanel.SetActive(false);
    }

    // 전부 닫기
    public void HideAll()
    {
        if (miniGamePanel != null) miniGamePanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(false);
        if (cameraProgressBar != null) cameraProgressBar.SetActive(false);
    }

    // SecurityCamera 연결 (ProgressBar가 actPoint 읽도록)
    public void RegisterCamera(SecurityCamera cam)
    {
        if (progressBarScript != null)
            progressBarScript.SetSecurityCamera(cam);
    }
}
