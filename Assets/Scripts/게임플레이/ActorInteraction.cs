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

    private float actPoint = 0f;
    private bool isActing = false;
    private float timer = 0f;
    private float nextTriggerTime;
    private bool isMiniGameActive = false;
    private bool hasPickedUpFilm = false;
    private int scriptCount = 0;           // 각본 보유 수 (최대 3개)
    private const int maxScriptCount = 3;
    public int ScriptCount => scriptCount; // InventoryHUD에서 읽기용
    public bool HasScript => scriptCount > 0;
    private float spawnDelayTimer = 0f;

    // ─── 악역 카메라 미니게임 ─────────────────
    private VillainCamera activeVillainCam = null;
    private bool isVillainMiniGameActive = false;
    private float villainMiniGameTime = 0f;
    private float villainPauseTimer = 0f;
    private bool villainPaused = false;

    // ─── 구출 시스템 ──────────────────────────
    private bool isRescuing = false;           // 구출 중 여부
    private float rescueTimer = 0f;            // 구출 진행 시간
    private float rescueDuration = 3f;         // 구출 완료까지 필요 시간
    private ActorController rescueTarget = null; // 구출 대상
    private Vector3 rescueStartPos;            // 구출 시작 위치 (움직임 감지용)
    private const float rescueRange = 2f;      // 구출 감지 범위 // 성공 후 1초 멈춤

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

        // 구출 진행 중
        if (isRescuing)
        {
            HandleRescue();
            return;
        }

        // ─── 악역 카메라 미니게임 처리 ───────────
        if (activeVillainCam != null)
        {
            HandleVillainCameraGame();
            return;
        }

        // 본인이 대상인 VillainCamera만 감지
        if (actorController.IsCarried)
        {
            var cams = FindObjectsByType<VillainCamera>(FindObjectsSortMode.None);
            foreach (var cam in cams)
            {
                if (!cam.IsMiniGameActiveNet) continue;

                // 대상 연기자가 본인인지 확인
                var myNetObj = actorController.GetComponent<NetworkObject>();
                if (myNetObj != null && cam.TargetActorId == myNetObj.Id)
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
        if (activeVillainCam == null)
        {
            var cams = FindObjectsByType<VillainCamera>(FindObjectsSortMode.None);
            foreach (var cam in cams)
            {
                if (cam.IsMiniGameActiveNet)
                {
                    activeVillainCam = cam;
                    StartVillainMiniGameLoop();
                    break;
                }
            }
            return;
        }

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

            // 이동 잠금 해제
            if (actorController.HasStateAuthority)
                actorController.IsLockedByVillain = false;

            // 악역 승리 포인트 +1
            GameStateManager.Instance?.AddTragedyPoint();

            // 연기자 체력 초기화 + 초기 스폰 위치로 리스폰
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
        }

        // 잡힌 상태 해제되면 초기화
        if (!actorController.IsCarried && activeVillainCam != null && !activeVillainCam.IsMiniGameActiveNet)
        {
            activeVillainCam = null;
            isVillainMiniGameActive = false;
            MiniGameManager.Instance?.HideAll();
            if (actorController.HasStateAuthority)
                actorController.IsLockedByVillain = false;
        }
    }

    private void StartVillainMiniGameLoop()
    {
        SetNextTriggerTime();
        MiniGameManager.Instance?.ShowWorkingUI();
        MiniGameManager.Instance?.RegisterVillainCamera(activeVillainCam);
        // 연기자 이동 잠금
        if (actorController.HasStateAuthority)
            actorController.IsLockedByVillain = true;
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

    // ─── 주변 감지 ─────────────────────────────
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

        if (nearFilm != null) { GameStateManager.Instance?.ShowFKeyHint(hint); return; }

        if (!hasPickedUpFilm) { GameStateManager.Instance?.HideFKeyHint(); return; }

        var scripts = FindObjectsByType<ScriptItem>(FindObjectsSortMode.None);
        foreach (var s in scripts)
        {
            if (!s.gameObject.activeSelf || s.IsPickedUp) continue;
            float dist = Vector3.Distance(transform.position, s.transform.position);
            if (dist < closestDist) { closestDist = dist; nearScript = s; hint = "각본 획득"; }
        }

        if (nearScript != null) { GameStateManager.Instance?.ShowFKeyHint(hint); return; }

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
        else
        {
            // 카메라 없을 때 잡힌 팀원 감지 (구출 가능)
            var actors = FindObjectsByType<ActorController>(FindObjectsSortMode.None);
            foreach (var actor in actors)
            {
                if (actor == actorController) continue; // 본인 제외
                if (!actor.IsCarried && actor.GetComponent<ActorInteraction>()?.activeVillainCam == null) continue;
                float dist = Vector3.Distance(transform.position, actor.transform.position);
                if (dist < rescueRange)
                {
                    GameStateManager.Instance?.ShowFKeyHint("구출하기");
                    return;
                }
            }
            GameStateManager.Instance?.HideFKeyHint();
        }
    }

    // ─── F키 입력 처리 ─────────────────────────
    private void HandleInput()
    {
        if (nearFilm != null && Input.GetKeyDown(KeyCode.F))
        {
            hasPickedUpFilm = true;
            actorController.RPC_PickUpFilm(nearFilm.GetComponent<NetworkObject>().Id);
            return;
        }

        if (nearScript != null && Input.GetKeyDown(KeyCode.F))
        {
            if (scriptCount >= maxScriptCount)
            {
                Debug.Log("[ActorInteraction] 각본 최대 보유 수 초과 (3개)");
                return;
            }
            scriptCount++;
            actorController.RPC_PickUpScript(nearScript.GetComponent<NetworkObject>().Id);
            Debug.Log($"[ActorInteraction] 각본 획득 | 보유: {scriptCount}/{maxScriptCount}");
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
                        Debug.Log($"[ActorInteraction] 각본 소모 | 남은: {scriptCount}/{maxScriptCount}");
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

            // 구출 가능한 팀원 감지 및 구출 시작
            if (Input.GetKeyDown(KeyCode.F))
            {
                var actors = FindObjectsByType<ActorController>(FindObjectsSortMode.None);
                foreach (var actor in actors)
                {
                    if (actor == actorController) continue;
                    var targetInteraction = actor.GetComponent<ActorInteraction>();
                    if (targetInteraction?.activeVillainCam == null && !actor.IsCarried) continue;
                    float dist = Vector3.Distance(transform.position, actor.transform.position);
                    if (dist < rescueRange)
                    {
                        StartRescue(actor);
                        return;
                    }
                }
            }
        }
    }

    // ─── 구출 시스템 ───────────────────────────
    private void StartRescue(ActorController target)
    {
        isRescuing = true;
        rescueTimer = 0f;
        rescueTarget = target;
        rescueStartPos = transform.position;
        actorController.RPC_PlayEmotion("Rescue"); // 구출 애니메이션
        GameStateManager.Instance?.HideFKeyHint();
        Debug.Log($"[ActorInteraction] 구출 시작: {target.name}");
    }

    private void HandleRescue()
    {
        if (!isRescuing || rescueTarget == null) return;

        // 움직이면 구출 실패
        float moved = Vector3.Distance(transform.position, rescueStartPos);
        if (moved > 0.3f)
        {
            CancelRescue();
            return;
        }

        rescueTimer += Time.deltaTime;

        if (rescueTimer >= rescueDuration)
        {
            CompleteRescue();
        }
    }

    private void CancelRescue()
    {
        isRescuing = false;
        rescueTimer = 0f;
        rescueTarget = null;
        actorController.RPC_ReturnToIdle();
        Debug.Log("[ActorInteraction] 구출 실패 - 움직임 감지");
    }

    private void CompleteRescue()
    {
        if (rescueTarget == null) return;

        // 잡힌 연기자의 VillainCamera 미니게임 일시 중단
        var targetInteraction = rescueTarget.GetComponent<ActorInteraction>();
        if (targetInteraction != null)
        {
            targetInteraction.PauseVillainMiniGame();
        }

        isRescuing = false;
        rescueTimer = 0f;
        rescueTarget = null;
        actorController.RPC_ReturnToIdle();
        Debug.Log("[ActorInteraction] 구출 성공!");
    }

    // 악역 미니게임 일시 중단 (구출당했을 때 외부에서 호출)
    public void PauseVillainMiniGame()
    {
        if (activeVillainCam == null) return;

        // 게이지 현재 값 저장 후 미니게임 패널 숨김
        MiniGameManager.Instance?.HideAll();
        isVillainMiniGameActive = false;
        villainPaused = false;

        // IsCarried 해제 (구출됨)
        if (actorController.HasStateAuthority)
        {
            actorController.IsCarried = false;
            actorController.NetIsCarried = false;
            actorController.IsLockedByVillain = false;
        }

        // VillainCamera는 유지 (게이지값 보존) - isMiniGameActive만 false로
        activeVillainCam.RPC_StopMiniGame();

        // activeVillainCam은 null로 하지 않음 - 다시 잡혔을 때 이어서 진행
        // 단 현재 actPoint는 VillainCamera에 저장됨
        float savedActPoint = activeVillainCam.actPoint;
        activeVillainCam = null;

        Debug.Log($"[ActorInteraction] 구출됨 - 게이지 {savedActPoint} 저장");
    }
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
