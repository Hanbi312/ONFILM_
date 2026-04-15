using Fusion;
using UnityEngine;

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
    private SecurityCamera activeCamera;
    private SecurityCamera linkedCamera;
    private ActorController nearInjuredActor; // 타인 치료 대상

    [SerializeField] private float healOtherRange = 2f; // 타인 치료 가능 범위
    private bool isHealingOther = false;      // 타인 치료 중 여부
    private ActorController lastHealedActor = null; // nearInjuredActor가 null이 돼도 RPC 호출하기 위한 캐시

    // HealGaugeHUD에서 치료 진행도 UI를 표시하기 위해 외부 노출
    public bool IsHealingOther => isHealingOther;
    public ActorController HealTarget  => lastHealedActor;

    private float actPoint = 0f;
    private bool isActing = false;
    private float timer = 0f;
    private float nextTriggerTime;
    private bool isMiniGameActive = false;
    private bool hasPickedUpFilm = false;
    // 미니게임 진행 중 여부 (ActorController에서 이동 잠금용)
    public bool IsInMiniGame => isActing || isMiniGameActive || isVillainMiniGameActive || activeVillainCam != null;

    // 연기자용 미니게임(카메라 작업) 중인지 여부 - WASD로 취소 가능한 상태
    public bool IsInActorMiniGame => isActing || isMiniGameActive;

    // WASD 입력 시 연기자용 미니게임 취소 (악역 카메라 미니게임은 취소 불가)
    public void CancelActorMiniGame()
    {
        if (!IsInActorMiniGame) return;

        if (isMiniGameActive)
            MiniGameManager.Instance?.poseGameScript?.OnTimeout();

        isActing = false;
        isMiniGameActive = false;
        activeCamera = null;
        linkedCamera = null;
        timer = 0f;
        spawnDelayTimer = 0f;
        MiniGameManager.Instance?.HideAll();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        actorController.RPC_ReturnToIdle();
        Debug.Log("[ActorInteraction] WASD 입력으로 연기자 미니게임 취소");
    }
    private int scriptCount = 0;           // 각본 보유 수 (최대 3개)
    private const int maxScriptCount = 3;
    public bool HasScript => scriptCount > 0;
    public int ScriptCount => scriptCount; // InventoryHUD용
    private float spawnDelayTimer = 0f;

    // ─── 악역 카메라 미니게임 ─────────────────
    private VillainCamera activeVillainCam = null;
    private bool isVillainMiniGameActive = false;
    private float villainMiniGameTime = 0f;
    private float villainPauseTimer = 0f;
    private bool villainPaused = false; // 성공 후 1초 멈춤

    private ActorController actorController;
    private NetworkObject networkObject;

    // 초기 스폰 위치 저장
    private Vector3 initialSpawnPos;
    private Quaternion initialSpawnRot;
    private bool spawnSaved = false;

    private void Start()
    {
        actorController = GetComponent<ActorController>();
        networkObject = GetComponent<NetworkObject>();
        SetNextTriggerTime();

        // 초기 스폰 위치 저장
        initialSpawnPos = transform.position;
        initialSpawnRot = transform.rotation;
        spawnSaved = true;
        Debug.Log($"[ActorInteraction] 초기 스폰 위치 저장: {initialSpawnPos}");
    }

    private void Update()
    {
        if (actorController == null)
        {
            actorController = GetComponent<ActorController>();
            return;
        }
        if (!actorController.HasInputAuthority) return;

        // ─── 악역 카메라 미니게임 처리 ───────────
        if (activeVillainCam != null)
        {
            HandleVillainCameraGame();
            return;
        }

        // 이 연기자가 VillainCamera 미니게임의 타겟인지 TargetActorId로 확인
        // (IsCarried 의존 제거 - RPC 타이밍 레이스 컨디션 해소)
        var myNetObj = actorController.GetComponent<NetworkObject>();
        if (myNetObj != null)
        {
            var cams = FindObjectsByType<VillainCamera>(FindObjectsSortMode.None);
            foreach (var cam in cams)
            {
                if (cam.IsMiniGameActiveNet && cam.TargetActorId == myNetObj.Id)
                {
                    activeVillainCam = cam;
                    StartVillainMiniGameLoop();
                    return;
                }
            }
        }

        Detect();
        HandleInput();

        // 스폰 위치 벗어나면 작업 종료
        if (isActing && activeCamera?.spawnPoint != null)
        {
            spawnDelayTimer += Time.deltaTime;
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

        // 미니게임 타임아웃
        if (isMiniGameActive && activeCamera != null)
        {
            activeCamera.miniGameTime += Time.deltaTime;
            if (activeCamera.miniGameTime >= 2f)
                MiniGameManager.Instance?.poseGameScript?.OnTimeout();
        }

        // 진행도 쌓기
        if (isActing && !isMiniGameActive && activeCamera != null)
        {
            activeCamera.actPoint += activeCamera.actSpeed * Time.deltaTime;
            CheckMiniGameTrigger();

            if (activeCamera.actPoint >= activeCamera.maxActPoint)
            {
                activeCamera.actPoint = 0f;
                isActing = false;
                timer = 0f;
                linkedCamera = null;
                MiniGameManager.Instance?.HideAll();
                var camId = activeCamera.GetComponent<NetworkObject>().Id;
                activeCamera = null;
                actorController.RPC_TurnOffCamera(camId);
                actorController.RPC_ReturnToIdle();
            }
        }
    }

    // ─── 악역 카메라 미니게임 처리 ─────────────
    // 연기자가 잡힌 상태에서 악역이 VillainCamera에 상호작용하면
    // 연기자 화면에 미니게임 패널이 뜨고 연기자가 진행
    private void HandleVillainCameraGame()
    {
        // activeVillainCam이 null이면 이미 Update에서 탐지해 세팅되므로 여기선 도달 안 함
        if (activeVillainCam == null) return;

        // 미니게임 타임아웃
        if (isVillainMiniGameActive)
        {
            villainMiniGameTime += Time.deltaTime;
            if (villainMiniGameTime >= 2f)
                MiniGameManager.Instance?.poseGameScript?.OnTimeout();
            return;
        }

        // 성공 후 1초 멈춤
        if (villainPaused)
        {
            villainPauseTimer += Time.deltaTime;
            if (villainPauseTimer >= 1f)
            {
                villainPaused = false;
                villainPauseTimer = 0f;
            }
            return;
        }

        // 게이지 쌓기
        activeVillainCam.actPoint += activeVillainCam.actSpeed * Time.deltaTime;

        // 미니게임 랜덤 트리거
        timer += Time.deltaTime;
        if (timer >= nextTriggerTime)
            TriggerVillainMiniGame();

        // 게이지 100% → 악역 승리 포인트 +1, 연기자 리스폰
        if (activeVillainCam.actPoint >= activeVillainCam.maxActPoint)
        {
            activeVillainCam.actPoint = 0f;
            activeVillainCam.RPC_StopMiniGame();
            activeVillainCam = null;
            isVillainMiniGameActive = false;
            MiniGameManager.Instance?.HideAll();

            actorController.RPC_SetLockedByVillain(false);
            GameStateManager.Instance?.AddTragedyPoint();

            if (spawnSaved)
            {
                actorController.RPC_Respawn(initialSpawnPos, initialSpawnRot);
                Debug.Log($"[ActorInteraction] 리스폰 요청: {initialSpawnPos}");
            }
            else
            {
                Debug.LogWarning("[ActorInteraction] 초기 스폰 위치 저장 안됨!");
            }

            Debug.Log("[ActorInteraction] 악역 카메라 완료! 비극 포인트 +1");
            return;
        }

        // VillainCamera 미니게임이 서버에서 종료됐으면 정리
        if (!activeVillainCam.IsMiniGameActiveNet)
        {
            activeVillainCam = null;
            isVillainMiniGameActive = false;
            MiniGameManager.Instance?.HideAll();
            actorController.RPC_SetLockedByVillain(false);
        }
    }

    private void StartVillainMiniGameLoop()
    {
        SetNextTriggerTime();
        MiniGameManager.Instance?.ShowWorkingUI();
        MiniGameManager.Instance?.RegisterVillainCamera(activeVillainCam);
        // 이동 잠금 - RPC로 서버(StateAuthority) 경유하여 [Networked] 변수 세팅
        actorController.RPC_SetLockedByVillain(true);
        Debug.Log("[ActorInteraction] 악역 카메라 미니게임 루프 시작");
    }

    private void TriggerVillainMiniGame()
    {
        if (activeVillainCam == null) return;

        isVillainMiniGameActive = true;
        villainMiniGameTime = 0f;
        SetNextTriggerTime();

        var mgm = MiniGameManager.Instance;
        if (mgm == null) return;

        mgm.ShowMiniGame();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var poseGame = mgm.poseGameScript;
        if (poseGame != null)
        {
            VillainCamera cam = activeVillainCam;
            poseGame.SetupMiniGame(cam,
                successCallback: (animTrigger) =>
                {
                    // 성공: 1초 멈춤
                    isVillainMiniGameActive = false;
                    villainPaused = true;
                    villainPauseTimer = 0f;
                    mgm.HideMiniGame();
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                },
                failCallback: () =>
                {
                    // 실패: 게이지 3% 상승
                    isVillainMiniGameActive = false;
                    if (cam != null)
                        cam.actPoint += cam.maxActPoint * 0.03f;
                    mgm.HideMiniGame();
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                });
        }
    }

    private void Detect()
    {
        nearFilm = null;
        nearScript = null;
        nearCamera = null;

        float closestDist = interactRange;
        string hint = null;

        var films = FindObjectsByType<FilmItem>(FindObjectsSortMode.None);
        foreach (var film in films)
        {
            if (!film.gameObject.activeSelf || film.IsPickedUp) continue;
            float dist = Vector3.Distance(transform.position, film.transform.position);
            if (dist < closestDist) { closestDist = dist; nearFilm = film; hint = "필름 획득"; }
        }

        if (nearFilm != null) { GameStateManager.Instance?.ShowFKeyHint(hint); }
        else
        {
            bool teamHasFilm = GameStateManager.Instance != null && GameStateManager.Instance.HasFilm;
            if (teamHasFilm)
            {
                var scripts = FindObjectsByType<ScriptItem>(FindObjectsSortMode.None);
                foreach (var s in scripts)
                {
                    if (!s.gameObject.activeSelf || s.IsPickedUp) continue;
                    float dist = Vector3.Distance(transform.position, s.transform.position);
                    if (dist < closestDist) { closestDist = dist; nearScript = s; hint = "각본 획득"; }
                }

                if (nearScript != null) { GameStateManager.Instance?.ShowFKeyHint(hint); }
                else
                {
                    bool canInteractCamera = linkedCamera != null || scriptCount > 0;
                    if (canInteractCamera)
                    {
                        var cameras = FindObjectsByType<SecurityCamera>(FindObjectsSortMode.None);
                        foreach (var cam in cameras)
                        {
                            if (cam.IsCameraOff || !cam.IsLightOn) continue;
                            if (linkedCamera != null && cam != linkedCamera) continue;
                            float dist = Vector3.Distance(transform.position, cam.transform.position);
                            if (dist < closestDist) { closestDist = dist; nearCamera = cam; hint = "카메라 끄기"; }
                        }
                    }

                    if (nearCamera != null) GameStateManager.Instance?.ShowFKeyHint(hint);
                    else GameStateManager.Instance?.HideFKeyHint();
                }
            }
            else
            {
                GameStateManager.Instance?.HideFKeyHint();
            }
        }

        // 타인 치료 감지 - 필름 획득 여부와 무관하게 항상 실행
        nearInjuredActor = null;
        if (!actorController.IsInjury && !actorController.IsDead)
        {
            var actors = FindObjectsByType<ActorController>(FindObjectsSortMode.None);
            float healClosestDist = healOtherRange;
            foreach (var actor in actors)
            {
                if (actor == actorController) continue;
                if (!actor.IsInjury || actor.IsDead) continue;
                float dist = Vector3.Distance(transform.position, actor.transform.position);
                if (dist < healClosestDist) { healClosestDist = dist; nearInjuredActor = actor; }
            }
        }
    }

    // ─── F키 입력 처리 ─────────────────────────
    private void HandleInput()
    {
        if (nearFilm != null && Input.GetKeyDown(KeyCode.F))
        {
            hasPickedUpFilm = true;
            actorController.RPC_PickUpFilm(nearFilm.GetComponent<NetworkObject>().Id);
            actorController.RPC_NotifyFilmPickedUp();
            return;
        }

        if (nearScript != null && Input.GetKeyDown(KeyCode.F))
        {
            if (scriptCount >= maxScriptCount)
            {
                Debug.Log("[ActorInteraction] 각본 최대 보유 수 초과");
                return;
            }
            scriptCount++;
            actorController.RPC_PickUpScript(nearScript.GetComponent<NetworkObject>().Id);
            actorController.RPC_PlayEmotion("item");
            return;
        }

        if (nearCamera != null && !actorController.IsDead)
        {
            if (Input.GetKeyDown(KeyCode.F) && !isMiniGameActive)
            {
                if (!isActing)
                {
                    if (linkedCamera != null && nearCamera != linkedCamera) return;
                    if (linkedCamera == null && scriptCount <= 0) return;

                    if (linkedCamera == null)
                    {
                        scriptCount--;
                        linkedCamera = nearCamera;
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

        // ─── 타인 치료 (H키) ─────────────────────────
        HandleHealOther();
    }

    private void HandleHealOther()
    {
        if (nearInjuredActor == null)
        {
            if (isHealingOther)
            {
                isHealingOther = false;
                actorController.LocalIsHealingOther = false; // 즉시 반영
                lastHealedActor?.RPC_StopHealByOther();
                lastHealedActor = null;
                actorController.RPC_SetHealingOther(false);
                // RPC_ReturnToIdle 제거 - IsHealingOther 동기화 전 아이들→치료→아이들 사이클 원인
            }
            return;
        }

        GameStateManager.Instance?.ShowFKeyHint("친구 치료하기 [H]");

        bool hPressed = Input.GetKey(KeyCode.H);

        if (hPressed && !actorController.IsInjury && !actorController.IsDead)
        {
            if (!isHealingOther)
            {
                isHealingOther = true;
                actorController.LocalIsHealingOther = true; // 즉시 반영
                lastHealedActor = nearInjuredActor;
                actorController.RPC_SetHealingOther(true);
            }
            float healRate = actorController.selfHealTime / actorController.otherHealTime;
            nearInjuredActor.RPC_HealByOther(Time.deltaTime * healRate);
        }
        else if (!hPressed && isHealingOther)
        {
            isHealingOther = false;
            actorController.LocalIsHealingOther = false; // 즉시 반영
            nearInjuredActor.RPC_StopHealByOther();
            lastHealedActor = null;
            actorController.RPC_SetHealingOther(false);
            // RPC_ReturnToIdle 제거 - isRecovering이 false되면 Animator가 자연 전환
        }
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
        if (mgm == null) { isMiniGameActive = false; return; }

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
                    actorController.RPC_PlayFail();
                });
        }
        else
        {
            isMiniGameActive = false;
            mgm.HideMiniGame();
        }
    }
}