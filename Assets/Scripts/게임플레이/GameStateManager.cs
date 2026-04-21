using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    [Header("문 (두 개 지원)")]
    [SerializeField] private GameObject[] exitDoors;          // 문 오브젝트 배열 (2개)
    [SerializeField] private Transform[]  doorInteractPoints; // 각 문 앞 상호작용 지정 위치 (2개)
    [SerializeField] private float doorInteractRange = 2f;
    [SerializeField] private float doorInteractTime  = 3f;
    [SerializeField] private Animator[] doorAnimators;

    [Header("게임 설정")]
    [SerializeField] private int requiredCameraOffCount = 4;
    [SerializeField] private int requiredTragedyPoint = 4;
    [SerializeField] private int resultSceneBuildIndex = 5;  // Build Settings에서 ResultScene 인덱스

    [Networked] public NetworkBool HasScript { get; set; }
    [Networked] public NetworkBool HasFilm { get; set; } // 팀 전체 공유
    [Networked] public int CameraOffCount { get; set; }
    [Networked] public NetworkBool IsDoorActivated { get; set; }
    [Networked] public NetworkBool IsGameClear { get; set; }
    [Networked] public int TragedyPoint { get; set; }

    private float doorProgress = 0f;
    private bool isOpeningDoor = false;
    private bool isDoorLocked  = false;
    private Transform activeInteractPoint = null;
    private ActorController localActor;
    private KillerController localKiller; // 악역도 문 상호작용 가능

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            // ★ 수정: NetworkBehaviour에서 Destroy(gameObject)는 Fusion 크래시 유발
            // 중복 인스턴스는 컴포넌트만 비활성화해서 Fusion이 Object를 계속 관리하게 둠
            Debug.LogWarning("[GameStateManager] 중복 인스턴스 감지 - 이 컴포넌트를 비활성화합니다.");
            enabled = false;
        }
    }

    // ★ 추가: OnDestroy에서 Instance를 정리해야 에디터 재실행 시 stale 참조 문제 방지
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void Spawned()
    {
        // 서버(호스트)가 모든 게임 상태를 명시적으로 0으로 초기화
        // Fusion 씬 기반 NetworkObject는 씬 재로드 후에도 [Networked] 변수가
        // 이전 값을 유지할 수 있으므로 Spawned()에서 반드시 리셋해야 함
        if (HasStateAuthority)
        {
            CameraOffCount  = 0;
            TragedyPoint    = 0;
            HasFilm         = false;
            HasScript       = false;
            IsDoorActivated = false;
            IsGameClear     = false;
        }

        // 문은 항상 보임 — 조건 미충족 시 상호작용만 차단 (Update에서 !IsDoorActivated 체크)
        if (fKeyHint != null) fKeyHint.SetActive(false);
    }

    public override void Render()
    {
        // 문 오브젝트 표시는 건들지 않음 (씬에서 항상 활성 상태 유지)
    }

    private void Update()
    {
        if (Object == null || !Object.IsValid) return;
        if (IsGameClear || !IsDoorActivated) return;

        if (localActor == null && localKiller == null) FindLocalPlayer();
        bool hasLocalPlayer = localActor != null || localKiller != null;
        if (!hasLocalPlayer) return;

        // 이미 애니메이션 재생 중이면 대기
        if (isOpeningDoor) return;

        Transform nearest = FindNearestInteractPoint(out float nearestDist);
        bool inRange = nearest != null && nearestDist <= doorInteractRange;

        Transform playerTransform = localActor != null
            ? localActor.transform
            : localKiller.transform;

        // 거리 재계산 (로컬 플레이어 위치 기준)
        inRange = nearest != null &&
                  Vector3.Distance(nearest.position, playerTransform.position) <= doorInteractRange;

        if (inRange)
            ShowFKeyHint("문 열기");
        else
        {
            ResetDoorProgress();
            return;
        }

        if (inRange && Input.GetKeyDown(KeyCode.F))
        {
            isOpeningDoor = true;
            activeInteractPoint = nearest;

            if (localActor != null)
            {
                localActor.RPC_Teleport(nearest.position, nearest.rotation);
                isDoorLocked = true;
                localActor.RPC_SetLockedByVillain(true);
                localActor.RPC_PlayEmotion("OpenDoor");
            }
            else if (localKiller != null)
            {
                localKiller.RPC_Teleport(nearest.position, nearest.rotation);
                isDoorLocked = true;
                localKiller.RPC_SetDoorLocked(true);
                localKiller.RPC_PlayDoorAnimation();
            }

            StartCoroutine(WaitForDoorAnimation());
        }
    }

    private IEnumerator WaitForDoorAnimation()
    {
        GameObject playerGO = localActor != null ? localActor.gameObject : localKiller?.gameObject;
        var anim = playerGO != null ? playerGO.GetComponentInChildren<Animator>() : null;

        if (anim == null)
        {
            yield return new WaitForSeconds(doorInteractTime);
            TriggerGameClear();
            yield break;
        }

        // 전환 시작 대기 (최대 0.5초)
        float elapsed = 0f;
        while (!anim.IsInTransition(0) && elapsed < 0.5f)
        { elapsed += Time.deltaTime; yield return null; }

        // 전환 완료 대기
        while (anim.IsInTransition(0)) yield return null;

        // 애니메이션 재생 완료 대기 (안전 상한: doorInteractTime)
        elapsed = 0f;
        while (elapsed < doorInteractTime)
        {
            if (!anim.IsInTransition(0) &&
                anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.95f)
                break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        TriggerGameClear();
    }

    private void TriggerGameClear()
    {
        if (IsGameClear) return;
        ReleaseDoorLock();
        Debug.Log("[GameStateManager] 문 애니메이션 완료 → RPC_GameClear");
        RPC_GameClear();
    }

    // 배열 내 가장 가까운 상호작용 포인트 반환
    private Transform FindNearestInteractPoint(out float minDist)
    {
        minDist = float.MaxValue;
        Transform nearest = null;

        if (doorInteractPoints == null || doorInteractPoints.Length == 0)
            return nearest;

        Transform playerTf = localActor != null ? localActor.transform : localKiller?.transform;
        if (playerTf == null) return nearest;

        foreach (var pt in doorInteractPoints)
        {
            if (pt == null) continue;
            float d = Vector3.Distance(pt.position, playerTf.position);
            if (d < minDist) { minDist = d; nearest = pt; }
        }
        return nearest;
    }

    // 문 이동 잠금 해제
    private void ReleaseDoorLock()
    {
        if (!isDoorLocked) return;
        isDoorLocked = false;
        isOpeningDoor = false;
        activeInteractPoint = null;
        localActor?.RPC_SetLockedByVillain(false);
        localKiller?.RPC_SetDoorLocked(false);
    }

    private void ResetDoorProgress()
    {
        doorProgress = 0f;
    }

    private void FindLocalPlayer()
    {
        var actors = FindObjectsByType<ActorController>(FindObjectsSortMode.None);
        foreach (var actor in actors)
        {
            if (actor.HasInputAuthority) { localActor = actor; return; }
        }

        var killers = FindObjectsByType<KillerController>(FindObjectsSortMode.None);
        foreach (var killer in killers)
        {
            if (killer.HasInputAuthority) { localKiller = killer; return; }
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
        if (TragedyPoint >= requiredTragedyPoint)
        {
            if (IsGameClear) return;
            IsGameClear = true;
            RPC_OnGameOver();
        }
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
        if (localActor != null && localActor.Object != null &&
            localActor.Object.InputAuthority == player)
        {
            ReleaseDoorLock();
            localActor = null;
        }
        if (localKiller != null && localKiller.Object != null &&
            localKiller.Object.InputAuthority == player)
        {
            ReleaseDoorLock();
            localKiller = null;
        }
        if (isOpeningDoor) { isOpeningDoor = false; doorProgress = 0f; HideFKeyHint(); }
        Debug.Log($"[GameStateManager] 플레이어 퇴장 처리 완료 | Player={player}");
    }

    public void RebuildAfterMigration()
    {
        Debug.Log("[GameStateManager] Migration 후 재연결 시작");

        ReleaseDoorLock();
        doorProgress = 0f;
        localActor = null;
        localKiller = null;
        FindLocalPlayer();

        if (Object == null || !Object.IsValid)
        {
            Debug.LogWarning("[GameStateManager] Migration 재연결 스킵 - Object 아직 준비 안 됨");
            return;
        }

        Debug.Log($"[GameStateManager] Migration 재연결 완료 | IsDoorActivated={IsDoorActivated}");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
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

        ReleaseDoorLock();
        isOpeningDoor = false;
        activeInteractPoint = null;

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

        ResultData.IsActorWin = true;
        StartCoroutine(GameClearRoutine());
    }

    private IEnumerator GameClearRoutine()
    {
        yield return new WaitForSeconds(2f);
        // Fusion Runner로 씬 전환 (서버만 LoadScene 호출, 클라이언트는 자동 따라옴)
        if (Runner != null && Runner.IsServer)
            Runner.LoadScene(SceneRef.FromIndex(resultSceneBuildIndex), LoadSceneMode.Single);
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
        if (Runner != null && Runner.IsServer)
            Runner.LoadScene(SceneRef.FromIndex(resultSceneBuildIndex), LoadSceneMode.Single);
    }
}