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
    [SerializeField] private int requiredCameraOffCount = 3;
    [SerializeField] private int requiredTragedyPoint = 3; // 악역 승리 조건

    [Networked] public NetworkBool HasScript { get; set; }
    [Networked] public int CameraOffCount { get; set; }
    [Networked] public NetworkBool IsDoorActivated { get; set; }
    [Networked] public NetworkBool IsGameClear { get; set; }
    [Networked] public int TragedyPoint { get; set; }
    [Networked] public NetworkBool ActorWin { get; set; } // true=연기자 승, false=악역 승

    private float doorProgress = 0f;
    private bool isOpeningDoor = false;
    private ActorController localActor;
    private KillerController localKiller;

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
            exitDoor.SetActive(IsDoorActivated || TragedyPoint >= requiredTragedyPoint);
    }

    private void Update()
    {
        if (Object == null || !Object.IsValid) return;
        if (IsGameClear) return;

        // 연기자 조건: 카메라 4개 꺼짐
        // 악역 조건: 비극 포인트 4점
        bool actorCanOpen = IsDoorActivated;
        bool killerCanOpen = TragedyPoint >= requiredTragedyPoint;

        if (!actorCanOpen && !killerCanOpen) return;

        // 로컬 플레이어 찾기
        if (localActor == null) FindLocalActor();
        if (localKiller == null) FindLocalKiller();

        // 연기자 처리
        if (actorCanOpen && localActor != null)
            HandleDoorInteraction(localActor.transform, true);

        // 악역 처리
        if (killerCanOpen && localKiller != null)
            HandleDoorInteraction(localKiller.transform, false);
    }

    private void HandleDoorInteraction(Transform playerTransform, bool isActor)
    {
        float dist = Vector3.Distance(exitDoor.transform.position, playerTransform.position);
        bool inRange = dist <= doorInteractRange;

        if (inRange)
            ShowFKeyHint("문 열기");
        else
        {
            HideFKeyHint();
            ResetDoorProgress();
            return;
        }

        if (inRange && Input.GetKeyDown(KeyCode.F))
        {
            if (!isOpeningDoor)
            {
                isOpeningDoor = true;
                if (localActor != null && playerTransform == localActor.transform)
                    localActor.RPC_PlayEmotion("OpenDoor");
                else if (localKiller != null && playerTransform == localKiller.transform)
                    localKiller.RPC_PlayEmotion("OpenDoor");
            }
            else
            {
                isOpeningDoor = false;
                if (localActor != null && playerTransform == localActor.transform)
                    localActor.RPC_ReturnToIdle();
                else if (localKiller != null && playerTransform == localKiller.transform)
                    localKiller.RPC_ReturnToIdle();
                ResetDoorProgress();
            }
        }

        if (isOpeningDoor && inRange)
        {
            doorProgress += Time.deltaTime;
            Debug.Log($"[GameStateManager] 문 진행도: {doorProgress:F1}/{doorInteractTime}");

            if (doorProgress >= doorInteractTime)
            {
                Debug.Log("[GameStateManager] 게이지 완료 - RPC_GameClear 호출!");
                RPC_GameClear(isActor);
            }
        }

        if (!inRange && isOpeningDoor)
        {
            isOpeningDoor = false;
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

    private void FindLocalKiller()
    {
        var killers = FindObjectsByType<KillerController>(FindObjectsSortMode.None);
        foreach (var killer in killers)
        {
            if (killer.HasInputAuthority)
            {
                localKiller = killer;
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

    public void OnScriptPickedUp()
    {
        if (!HasStateAuthority) return;
        HasScript = true;
        Debug.Log("[GameStateManager] 각본 획득");
    }

    public void AddTragedyPoint()
    {
        if (HasStateAuthority)
        {
            TragedyPoint++;
            Debug.Log($"[GameStateManager] 비극 포인트 +1 | 총 {TragedyPoint}포인트");
        }
        else
        {
            // 클라이언트에서 호출 시 서버로 RPC 전송
            RPC_AddTragedyPoint();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_AddTragedyPoint()
    {
        TragedyPoint++;
        Debug.Log($"[GameStateManager] 비극 포인트 +1 (RPC) | 총 {TragedyPoint}포인트");
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
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_GameClear(bool actorWin)
    {
        if (IsGameClear) return;
        IsGameClear = true;
        ActorWin = actorWin;
        RPC_OnGameClear(actorWin);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnGameClear(bool actorWin)
    {
        Debug.Log($"[GameStateManager] 게임 클리어! | {(actorWin ? "연기자 승리 - 해피엔딩" : "악역 승리 - 베드엔딩")}");

        if (doorAnimators != null && doorAnimators.Length > 0)
        {
            foreach (var anim in doorAnimators)
            {
                if (anim != null)
                {
                    anim.SetTrigger("Open");
                    Debug.Log($"[GameStateManager] Open 트리거 발동: {anim.gameObject.name}");
                }
                else
                    Debug.LogError("[GameStateManager] doorAnimators 배열에 null 있음!");
            }
        }
        else
            Debug.LogError("[GameStateManager] doorAnimators 배열이 비어있음!");

        // 결과 저장 후 결과 씬으로 이동
        ResultData.IsActorWin = actorWin;
        StartCoroutine(GameClearRoutine());
    }

    private IEnumerator GameClearRoutine()
    {
        yield return new WaitForSeconds(2f);
        Debug.Log("[GameStateManager] 결과 씬으로 이동");
        if (Runner != null && Runner.IsServer)
            Runner.LoadScene(SceneRef.FromIndex(5), UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}
