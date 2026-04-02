using Fusion;
using UnityEngine;

/// <summary>
/// KillerInteraction - 악역 상호작용 전체 관리
///
/// [흐름]
/// 1. 사망한 연기자 근처 → F 힌트 → F 누르면 들기
/// 2. 든 상태에서 이동 → 들고 걷는 애니메이션
/// 3. 든 상태에서 F → 내려놓기
/// 4. 든 상태에서 VillainCamera 근처 → F 힌트 → F 누르면
///    연기자 spawnPoint로 텔레포트 + 악역 미니게임 시작
/// 5. 미니게임 성공 → 1초 멈춤 후 게이지 계속
/// 6. 미니게임 실패 → 게이지 3% 증가
/// 7. 게이지 100% → 비극포인트 +1, 연기자 초기 스폰으로 리스폰
/// </summary>
public class KillerInteraction : MonoBehaviour
{
    [Header("상호작용 범위")]
    [SerializeField] private float interactRange = 2f;

    [Header("미니게임 타이밍")]
    [SerializeField] private float minTriggerTime = 2f;
    [SerializeField] private float maxTriggerTime = 5f;

    private KillerController killerController;
    private ActorController carriedActor = null;
    private bool isCarrying = false;

    // 악역 미니게임 상태
    private VillainCamera activeVillainCam = null;
    private bool isVillainActing = false;
    private bool isVillainMiniGameActive = false;
    private float villainActPoint = 0f;
    private float villainTimer = 0f;
    private float villainNextTriggerTime;
    private float pauseTimer = 0f;
    private bool isPaused = false; // 성공 후 1초 멈춤

    private void Start()
    {
        killerController = GetComponent<KillerController>();
        SetNextTriggerTime();
    }

    private void Update()
    {
        if (killerController == null) return;
        if (!killerController.HasInputAuthority && !killerController.HasStateAuthority) return;

        // ─── 악역 미니게임 진행 중 ───
        if (isVillainActing)
        {
            HandleVillainMiniGame();
            return;
        }

        // ─── 연기자 들고 있는 상태 ───
        if (isCarrying)
        {
            // VillainCamera 감지
            VillainCamera nearCam = DetectVillainCamera();
            if (nearCam != null)
            {
                GameStateManager.Instance?.ShowFKeyHint("카메라 상호작용");
                if (Input.GetKeyDown(KeyCode.F))
                {
                    StartVillainAct(nearCam);
                    return;
                }
            }
            else
            {
                GameStateManager.Instance?.ShowFKeyHint("내려놓기");
                if (Input.GetKeyDown(KeyCode.F))
                    StopCarrying();
            }
            return;
        }

        // ─── 기본 상태: 사망한 연기자 감지 ───
        ActorController nearActor = DetectDeadActor();
        if (nearActor != null)
        {
            GameStateManager.Instance?.ShowFKeyHint("들기");
            if (Input.GetKeyDown(KeyCode.F))
                StartCarrying(nearActor);
        }
        else
        {
            GameStateManager.Instance?.HideFKeyHint();
        }
    }

    // ─── 연기자 감지 ───────────────────────────
    private ActorController DetectDeadActor()
    {
        var actors = FindObjectsByType<ActorController>(FindObjectsSortMode.None);
        ActorController closest = null;
        float closestDist = interactRange;

        foreach (var actor in actors)
        {
            if (!actor.IsDead) continue;
            if (actor.IsCarried) continue;

            float dist = Vector3.Distance(transform.position, actor.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = actor;
            }
        }
        return closest;
    }

    // ─── VillainCamera 감지 ────────────────────
    private VillainCamera DetectVillainCamera()
    {
        var cams = FindObjectsByType<VillainCamera>(FindObjectsSortMode.None);
        VillainCamera closest = null;
        float closestDist = interactRange;

        foreach (var cam in cams)
        {
            float dist = Vector3.Distance(transform.position, cam.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = cam;
            }
        }
        return closest;
    }

    // ─── 들기 ──────────────────────────────────
    private void StartCarrying(ActorController actor)
    {
        isCarrying = true;
        carriedActor = actor;
        killerController.RPC_StartCarry(actor.GetComponent<NetworkObject>().Id);
        GameStateManager.Instance?.HideFKeyHint();
    }

    private void StopCarrying()
    {
        if (carriedActor == null) return;
        killerController.RPC_StopCarry(carriedActor.GetComponent<NetworkObject>().Id);
        carriedActor = null;
        isCarrying = false;
    }

    // ─── 악역 미니게임 시작 ────────────────────
    private void StartVillainAct(VillainCamera cam)
    {
        activeVillainCam = cam;
        isVillainActing = true;
        isCarrying = false; // 들기 상태 해제

        // 연기자를 spawnPoint로 텔레포트
        if (carriedActor != null && cam.spawnPoint != null)
        {
            var actorCtrl = carriedActor.GetComponent<ActorController>();
            actorCtrl?.RPC_Teleport(cam.spawnPoint.position, cam.spawnPoint.rotation);

            // 내려놓기 RPC
            killerController.RPC_StopCarry(carriedActor.GetComponent<NetworkObject>().Id);
        }

        carriedActor = null;
        MiniGameManager.Instance?.ShowWorkingUI();
        MiniGameManager.Instance?.RegisterCamera(null); // ProgressBar 연결용 (VillainCamera 기반)
        SetNextTriggerTime();

        GameStateManager.Instance?.HideFKeyHint();
        Debug.Log("[KillerInteraction] 악역 미니게임 시작!");
    }

    // ─── 악역 미니게임 진행 ────────────────────
    private void HandleVillainMiniGame()
    {
        // 미니게임 패널 중 타임아웃 체크
        if (isVillainMiniGameActive && activeVillainCam != null)
        {
            activeVillainCam.miniGameTime += Time.deltaTime;
            if (activeVillainCam.miniGameTime >= 2f)
                MiniGameManager.Instance?.poseGameScript?.OnTimeout();
        }

        // 성공 후 1초 멈춤
        if (isPaused)
        {
            pauseTimer += Time.deltaTime;
            if (pauseTimer >= 1f)
            {
                isPaused = false;
                pauseTimer = 0f;
            }
            return;
        }

        if (isVillainMiniGameActive) return;

        // 게이지 증가
        villainActPoint += activeVillainCam.actSpeed * Time.deltaTime;
        // ProgressBar 업데이트
        if (activeVillainCam != null)
            activeVillainCam.actPoint = villainActPoint;

        // 미니게임 랜덤 트리거
        villainTimer += Time.deltaTime;
        if (villainTimer >= villainNextTriggerTime)
            TriggerVillainMiniGame();

        // 게이지 100% → 완료
        if (villainActPoint >= activeVillainCam.maxActPoint)
            CompleteVillainAct();
    }

    private void SetNextTriggerTime()
    {
        villainTimer = 0f;
        villainNextTriggerTime = Random.Range(minTriggerTime, maxTriggerTime);
    }

    private void TriggerVillainMiniGame()
    {
        if (activeVillainCam == null) return;

        isVillainMiniGameActive = true;
        activeVillainCam.miniGameTime = 0f;
        activeVillainCam.isMiniGameActive = true;

        var mgm = MiniGameManager.Instance;
        if (mgm == null) return;

        mgm.ShowMiniGame();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var poseGame = mgm.poseGameScript;
        if (poseGame != null)
        {
            poseGame.SetupMiniGame(activeVillainCam,
                successCallback: (animTrigger) =>
                {
                    // 성공: 1초 멈춤 후 계속
                    isVillainMiniGameActive = false;
                    isPaused = true;
                    pauseTimer = 0f;
                    mgm.HideMiniGame();
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    SetNextTriggerTime();
                },
                failCallback: () =>
                {
                    // 실패: 게이지 3% 증가
                    isVillainMiniGameActive = false;
                    villainActPoint += activeVillainCam.maxActPoint * 0.03f;
                    activeVillainCam.actPoint = villainActPoint;
                    mgm.HideMiniGame();
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    SetNextTriggerTime();
                });
        }
    }

    private void CompleteVillainAct()
    {
        villainActPoint = 0f;
        isVillainActing = false;
        isVillainMiniGameActive = false;

        MiniGameManager.Instance?.HideAll();

        // 비극 포인트 +1
        GameStateManager.Instance?.AddTragedyPoint();

        // 연기자 초기 스폰 위치로 리스폰
        RespawnActor();

        Debug.Log("[KillerInteraction] 악역 미니게임 완료! 비극 포인트 +1");
    }

    private void RespawnActor()
    {
        // 씬의 ActorController 찾아서 초기 스폰 위치로 보내기
        var actors = FindObjectsByType<ActorController>(FindObjectsSortMode.None);
        foreach (var actor in actors)
        {
            if (!actor.HasStateAuthority) continue;
            // GameplayManager의 actorSpawnPoint로 텔레포트
            var gm = FindObjectOfType<GameplayManager>();
            if (gm != null && gm.actorSpawnPoint != null)
                actor.RPC_Teleport(gm.actorSpawnPoint.position, gm.actorSpawnPoint.rotation);
        }
    }
}
