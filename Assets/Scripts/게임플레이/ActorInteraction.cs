using Fusion;
using UnityEngine;

/// <summary>
/// ActorInteraction - 연기자 상호작용
/// ★ MonoBehaviour로 변경 - ActorController.HasInputAuthority 사용
/// RPC는 ActorController를 통해 전송
/// </summary>
public class ActorInteraction : MonoBehaviour
{
    [Header("상호작용 범위")]
    [SerializeField] private float interactRange = 2f;

    [Header("미니게임 타이밍")]
    [SerializeField] private float minTriggerTime = 2f;
    [SerializeField] private float maxTriggerTime = 5f;

    private FilmItem nearFilm;
    private ScriptItem nearScript;
    private SecurityCamera nearCamera;
    private SecurityCamera activeCamera;     // 현재 작업 중인 카메라
    private SecurityCamera linkedCamera;     // 각본으로 연결된 카메라 (완료 전까지 유지)

    private float actPoint = 0f; // 사용 안 함 - 카메라별 actPoint는 activeCamera.actPoint 사용
    private bool isActing = false;
    private float timer = 0f;
    private float nextTriggerTime;
    private bool isMiniGameActive = false;
    private bool hasPickedUpFilm = false;
    private bool hasScript = false;          // 각본 보유 여부
    private float spawnDelayTimer = 0f;

    private ActorController actorController;
    private NetworkObject networkObject;

    private void Start()
    {
        actorController = GetComponent<ActorController>();
        networkObject = GetComponent<NetworkObject>();
        SetNextTriggerTime();
    }

    private void Update()
    {
        if (actorController == null)
        {
            actorController = GetComponent<ActorController>();
            return;
        }
        if (!actorController.HasInputAuthority && !actorController.HasStateAuthority) return;

        Detect();
        HandleInput();

        // miniGamePanel 활성화 중 스폰 위치 벗어나면 전부 종료
        if (isActing && activeCamera?.spawnPoint != null)
        {
            spawnDelayTimer += Time.deltaTime;

            // 작업 시작 후 1초 뒤부터 거리 체크 (텔레포트 안착 대기)
            if (spawnDelayTimer > 1f)
            {
                float dist = Vector3.Distance(transform.position, activeCamera.spawnPoint.position);
                if (dist > 1f)
                {
                    if (isMiniGameActive)
                        MiniGameManager.Instance?.poseGameScript?.OnTimeout();

                    isActing = false;
                    isMiniGameActive = false;
                    activeCamera = null;
                    timer = 0f;
                    spawnDelayTimer = 0f;
                    MiniGameManager.Instance?.HideAll();
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    actorController.RPC_ReturnToIdle();
                }
            }
        }
        else
        {
            spawnDelayTimer = 0f;
        }

        // 미니게임 타임아웃 체크
        if (isMiniGameActive && activeCamera != null)
        {
            activeCamera.miniGameTime += Time.deltaTime;
            if (activeCamera.miniGameTime >= 2f)
            {
                MiniGameManager.Instance?.poseGameScript?.OnTimeout();
            }
        }

        // 작업 중 + 미니게임 아닐 때 진행도 쌓기 (activeCamera 기준)
        if (isActing && !isMiniGameActive && activeCamera != null)
        {
            // 카메라별로 actPoint 따로 관리 (activeCamera.actPoint 직접 사용)
            activeCamera.actPoint += activeCamera.actSpeed * Time.deltaTime;

            CheckMiniGameTrigger();

            if (activeCamera.actPoint >= activeCamera.maxActPoint)
            {
                activeCamera.actPoint = 0f;
                isActing = false;
                timer = 0f;
                linkedCamera = null; // 카메라 완료 → 다음 카메라 하려면 새 각본 필요
                MiniGameManager.Instance?.HideAll();
                var camId = activeCamera.GetComponent<NetworkObject>().Id;
                activeCamera = null;
                actorController.RPC_TurnOffCamera(camId);
                actorController.RPC_ReturnToIdle();
            }
        }
    }

    // ─── 주변 감지 ─────────────────────────────
    private void Detect()
    {
        nearFilm = null;
        nearScript = null;
        nearCamera = null;

        float closestDist = interactRange;
        string hint = null;

        // 1. 필름 감지
        var films = FindObjectsByType<FilmItem>(FindObjectsSortMode.None);
        foreach (var film in films)
        {
            if (!film.gameObject.activeSelf || film.IsPickedUp) continue;
            float dist = Vector3.Distance(transform.position, film.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                nearFilm = film;
                hint = "필름 획득";
            }
        }

        if (nearFilm != null)
        {
            GameStateManager.Instance?.ShowFKeyHint(hint);
            return;
        }

        // 2. 각본 감지 (필름 획득 후에만 가능)
        if (!hasPickedUpFilm) { GameStateManager.Instance?.HideFKeyHint(); return; }

        var scripts = FindObjectsByType<ScriptItem>(FindObjectsSortMode.None);
        foreach (var s in scripts)
        {
            if (!s.gameObject.activeSelf || s.IsPickedUp) continue;
            float dist = Vector3.Distance(transform.position, s.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                nearScript = s;
                hint = "각본 획득";
            }
        }

        if (nearScript != null)
        {
            GameStateManager.Instance?.ShowFKeyHint(hint);
            return;
        }

        // 3. 카메라 감지
        // linkedCamera 있으면 그 카메라만 감지, 없으면 각본 보유 시 감지
        bool canInteractCamera = linkedCamera != null || hasScript;
        if (canInteractCamera)
        {
            var cameras = FindObjectsByType<SecurityCamera>(FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam.IsCameraOff || !cam.IsLightOn) continue;
                // linkedCamera가 있으면 그 카메라만 감지
                if (linkedCamera != null && cam != linkedCamera) continue;
                float dist = Vector3.Distance(transform.position, cam.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    nearCamera = cam;
                    hint = "카메라 끄기";
                }
            }
        }

        if (nearCamera != null)
            GameStateManager.Instance?.ShowFKeyHint(hint);
        else
            GameStateManager.Instance?.HideFKeyHint();
    }

    // ─── F키 입력 처리 ─────────────────────────
    private void HandleInput()
    {
        // 필름 획득
        if (nearFilm != null && Input.GetKeyDown(KeyCode.F))
        {
            hasPickedUpFilm = true;
            // ★ ActorController를 통해 RPC 전송
            actorController.RPC_PickUpFilm(nearFilm.GetComponent<NetworkObject>().Id);
            return;
        }

        // 각본 획득
        if (nearScript != null && Input.GetKeyDown(KeyCode.F))
        {
            hasScript = true;
            actorController.RPC_PickUpScript(nearScript.GetComponent<NetworkObject>().Id);
            return;
        }

        // 카메라 작업
        if (nearCamera != null && !actorController.IsDead)
        {
            if (Input.GetKeyDown(KeyCode.F) && !isMiniGameActive)
            {
                if (!isActing)
                {
                    // linkedCamera가 있으면 그 카메라만 상호작용 가능
                    if (linkedCamera != null && nearCamera != linkedCamera)
                    {
                        Debug.Log("[ActorInteraction] 이미 연결된 카메라가 있음. 먼저 완료해야 함.");
                        return;
                    }

                    // linkedCamera 없으면 각본 필요
                    if (linkedCamera == null && !hasScript)
                    {
                        Debug.Log("[ActorInteraction] 각본이 없어 카메라 상호작용 불가");
                        return;
                    }

                    // 첫 상호작용 시 각본 소모 + linkedCamera 연결
                    if (linkedCamera == null)
                    {
                        hasScript = false;
                        linkedCamera = nearCamera;
                        Debug.Log($"[ActorInteraction] 각본 소모 → {nearCamera.name} 연결");
                    }

                    isActing = true;
                    activeCamera = nearCamera;
                    spawnDelayTimer = 0f;
                    if (nearCamera.spawnPoint != null)
                        actorController.RPC_Teleport(nearCamera.spawnPoint.position, nearCamera.spawnPoint.rotation);
                    MiniGameManager.Instance?.ShowWorkingUI();
                    MiniGameManager.Instance?.RegisterCamera(nearCamera);
                }
                else
                {
                    // 작업 중단
                    isActing = false;
                    activeCamera = null;
                    timer = 0f;
                    MiniGameManager.Instance?.HideAll();
                }
            }
        }
        else if (!isMiniGameActive && !isActing)
        {
            activeCamera = null;
            timer = 0f;
        }
    }

    // ─── 텔레포트 ───────────────────────────────
    private void TeleportTo(Transform target)
    {
        if (target == null) return;

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.position = target.position;
        transform.rotation = target.rotation;

        if (cc != null) cc.enabled = true;

        Debug.Log($"[ActorInteraction] 스폰 위치로 이동: {target.position}");
    }

    // ─── 미니게임 ───────────────────────────────
    private void CheckMiniGameTrigger()
    {
        timer += Time.deltaTime;
        if (timer >= nextTriggerTime)
        {
            TriggerMiniGame();
            SetNextTriggerTime();
        }
    }

    private void SetNextTriggerTime()
    {
        timer = 0f;
        nextTriggerTime = Random.Range(minTriggerTime, maxTriggerTime);
    }

    private void TriggerMiniGame()
    {
        if (activeCamera == null) return;

        isMiniGameActive = true;
        activeCamera.miniGameTime = 0f;
        activeCamera.isMiniGameActive = true;

        var mgm = MiniGameManager.Instance;
        if (mgm == null)
        {
            Debug.LogError("[ActorInteraction] MiniGameManager 없음!");
            isMiniGameActive = false;
            return;
        }

        var poseGame = mgm.poseGameScript;
        mgm.ShowMiniGame();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (poseGame != null)
        {
            SecurityCamera cam = activeCamera;
            poseGame.SetupMiniGame(cam,
                successCallback: (animTrigger) =>
                {
                    isMiniGameActive = false;
                    mgm.HideMiniGame();
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;

                    // 모든 클라이언트에 감정 애니메이션 동기화
                    if (!string.IsNullOrEmpty(animTrigger))
                        actorController.RPC_PlayEmotion(animTrigger);
                },
                failCallback: () =>
                {
                    isMiniGameActive = false;
                    isActing = false;
                    activeCamera = null;
                    timer = 0f;
                    mgm.HideAll();
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    actorController.RPC_PlayFail(); // 실패 애니메이션 재생
                });
            Debug.Log("[ActorInteraction] 미니게임 발생!");
        }
        else
        {
            Debug.LogError("[ActorInteraction] poseGameScript null!");
            isMiniGameActive = false;
            mgm.HideMiniGame();
        }
    }
}
