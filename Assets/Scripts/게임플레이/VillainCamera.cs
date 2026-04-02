using Fusion;
using UnityEngine;

/// <summary>
/// VillainCamera - 악역용 카메라
/// 악역이 연기자를 들고 다가가면 F 힌트 표시
/// F 누르면 연기자를 spawnPoint로 보내고 악역 미니게임 시작
/// </summary>
public class VillainCamera : NetworkBehaviour
{
    [Header("스폰 포인트 (연기자가 보내질 위치)")]
    public Transform spawnPoint;

    [Header("상호작용 범위")]
    public float interactRange = 2f;

    [Header("미니게임 설정")]
    public float minTriggerTime = 2f;
    public float maxTriggerTime = 5f;
    public float actSpeed = 1f;
    public float maxActPoint = 10f;

    // 미니게임 상태
    public float actPoint = 0f;
    public float miniGameTime = 0f;
    public bool isMiniGameActive = false;
    public PoseGame poseGameScript;

    public void EndMiniGame()
    {
        isMiniGameActive = false;
        miniGameTime = 0f;
        MiniGameManager.Instance?.HideMiniGame();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
