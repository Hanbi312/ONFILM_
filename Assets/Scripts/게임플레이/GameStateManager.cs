using System.Collections;
using Fusion;
using UnityEngine;
using TMPro;

/// <summary>
/// GameStateManager - 게임 전체 상태 관리
/// </summary>
public class GameStateManager : NetworkBehaviour
{
    public static GameStateManager Instance;

    [Header("F키 힌트 UI")]
    [SerializeField] private GameObject fKeyHint;
    [SerializeField] private TMP_Text actionText;

    [Header("문")]
    [SerializeField] private GameObject exitDoor;
    [SerializeField] private float doorInteractRange = 2f;
    [SerializeField] private float doorInteractTime = 3f;
    [SerializeField] private Animator[] doorAnimators;

    [Header("게임 설정")]
    [SerializeField] private int requiredCameraOffCount = 4;

    [Networked] public NetworkBool HasScript { get; set; }
    [Networked] public NetworkBool HasFilm { get; set; } // 팀 전체 공유
    [Networked] public int CameraOffCount { get; set; }
    [Networked] public NetworkBool IsDoorActivated { get; set; }
    [Networked] public NetworkBool IsGameClear { get; set; }
    [Networked] public int TragedyPoint { get; set; }

    private float doorProgress = 0f;
    private bool isOpeningDoor = false;
    private ActorController localActor;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void Spawned()
    {
        if (exitDoor != null) exitDoor.SetActive(false);
        if (fKeyHint != null) fKeyHint.SetActive(false);
    }

    public override void Render()
    {
        if (exitDoor != null)
            exitDoor.SetActive(IsDoorActivated);
    }

    private void Update()
    {
        if (Object == null || !Object.IsValid) return;
        if (IsGameClear || !IsDoorActivated) return;

        if (localActor == null) FindLocalActor();
        if (localActor == null) return;

        float dist = Vector3.Distance(exitDoor.transform.position, localActor.transform.position);
        bool inRange = dist <= doorInteractRange;

        if (inRange)
            ShowFKeyHint("문 열기");
        else
        {
            HideFKeyHint();
            ResetDoorProgress();
        }

        if (inRange && Input.GetKey(KeyCode.F))
        {
            doorProgress += Time.deltaTime;
            Debug.Log($"[GameStateManager] 문 진행도: {doorProgress:F1}/{doorInteractTime}");

            if (!isOpeningDoor)
            {
                isOpeningDoor = true;
                localActor.RPC_PlayEmotion("OpenDoor");
            }

            if (doorProgress >= doorInteractTime)
            {
                Debug.Log("[GameStateManager] 게이지 완료 - RPC_GameClear 호출!");
                RPC_GameClear();
            }
        }
        else if (!Input.GetKey(KeyCode.F))
        {
            if (isOpeningDoor)
            {
                isOpeningDoor = false;
                localActor.RPC_ReturnToIdle();
            }
            ResetDoorProgress();
        }
    }

    private void ResetDoorProgress()
    {
        doorProgress = 0f;
    }

    private void FindLocalActor()
    {
        var actors = FindObjectsByType<ActorController>(FindObjectsSortMode.None);
        foreach (var actor in actors)
        {
            if (actor.HasInputAuthority)
            {
                localActor = actor;
                return;
            }
        }
    }

    public void ShowFKeyHint(string action)
    {
        if (fKeyHint == null) return;
        fKeyHint.SetActive(true);
        if (actionText != null) actionText.text = action;
    }

    public void HideFKeyHint()
    {
        if (fKeyHint != null) fKeyHint.SetActive(false);
    }

    public void OnFilmPickedUp()
    {
        if (!HasStateAuthority) return;
        HasFilm = true;
        Debug.Log("[GameStateManager] 필름 획득 - 모든 연기자 각본 획득 가능");
    }

    public void OnScriptPickedUp()
    {
        if (!HasStateAuthority) return;
        HasScript = true;
        Debug.Log("[GameStateManager] 각본 획득");
    }

    public void AddTragedyPoint()
    {
        if (!HasStateAuthority) return;
        TragedyPoint++;
        Debug.Log($"[GameStateManager] 비극 포인트 +1 | 총 {TragedyPoint}포인트");

        // 비극 포인트가 카메라 수 이상이면 악역 승리 (베드엔딩)
        if (TragedyPoint >= requiredCameraOffCount)
        {
            if (IsGameClear) return;
            IsGameClear = true;
            RPC_OnGameOver();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnGameOver()
    {
        Debug.Log("[GameStateManager] 게임 오버 - 악역 승리!");
        ResultData.IsActorWin = false;
        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        yield return new WaitForSeconds(2f);
        UnityEngine.SceneManagement.SceneManager.LoadScene("ResultScene");
    }

    public void OnCameraOff()
    {
        if (!HasStateAuthority) return;
        CameraOffCount++;
        Debug.Log($"[GameStateManager] 카메라 꺼짐 | {CameraOffCount}/{requiredCameraOffCount}");

        if (CameraOffCount >= requiredCameraOffCount)
        {
            IsDoorActivated = true;
            Debug.Log("[GameStateManager] 문 활성화!");

            // 씬의 남은 모든 카메라 라이트 강제 종료 + 이후 상호작용 차단
            var allCameras = FindObjectsByType<SecurityCamera>(FindObjectsSortMode.None);
            foreach (var cam in allCameras)
            {
                if (!cam.IsCameraOff)
                {
                    cam.TurnOff();
                    Debug.Log($"[GameStateManager] 남은 카메라 강제 종료: {cam.gameObject.name}");
                }
            }
        }
    }

    /// <summary>
    /// 플레이어가 퇴장했을 때 FusionLobbyManager에서 호출
    /// localActor가 퇴장한 플레이어의 것이라면 재탐색
    /// </summary>
    public void OnPlayerLeft(PlayerRef player)
    {
        // localActor가 퇴장한 플레이어 소유인지 확인
        if (localActor != null && localActor.Object != null &&
            localActor.Object.InputAuthority == player)
        {
            Debug.Log($"[GameStateManager] localActor의 플레이어 퇴장 - 재탐색 | Player={player}");
            localActor = null;
        }

        // F키 진행 중이었다면 초기화
        if (isOpeningDoor)
        {
            isOpeningDoor = false;
            doorProgress = 0f;
            HideFKeyHint();
        }

        Debug.Log($"[GameStateManager] 플레이어 퇴장 처리 완료 | Player={player}");
    }

    /// <summary>
    /// Host Migration 후 씬 오브젝트 재연결
    /// </summary>
    public void RebuildAfterMigration()
    {
        Debug.Log("[GameStateManager] Migration 후 재연결 시작");

        // localActor 재탐색
        localActor = null;
        FindLocalActor();

        // 문 상태 재반영 (Render()가 처리하지만 명시적으로 호출)
        if (exitDoor != null)
            exitDoor.SetActive(IsDoorActivated);

        // 진행 중이던 문 열기 상태 초기화 (Migration 중 끊겼을 수 있음)
        isOpeningDoor = false;
        doorProgress = 0f;

        Debug.Log($"[GameStateManager] Migration 재연결 완료 | IsDoorActivated={IsDoorActivated} | localActor={localActor?.name ?? "null"}");
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_GameClear()
    {
        if (IsGameClear) return;
        IsGameClear = true;
        RPC_OnGameClear();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnGameClear()
    {
        Debug.Log("[GameStateManager] 게임 클리어!");

        if (doorAnimators != null && doorAnimators.Length > 0)
        {
            foreach (var anim in doorAnimators)
            {
                if (anim != null)
                    anim.SetTrigger("Open");
                else
                    Debug.LogError("[GameStateManager] doorAnimators 배열에 null 있음!");
            }
        }
        else
        {
            Debug.LogError("[GameStateManager] doorAnimators 배열이 비어있음! Inspector에서 연결 필요");
        }

        // 연기자 승리 (게임 클리어 = 해피엔딩)
        ResultData.IsActorWin = true;
        StartCoroutine(GameClearRoutine());
    }

    private IEnumerator GameClearRoutine()
    {
        yield return new WaitForSeconds(2f); // 문 열리는 연출 대기
        UnityEngine.SceneManagement.SceneManager.LoadScene("ResultScene");
    }
}