using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GameStateManager - 게임 전체 상태 관리
/// 각본 획득, 카메라 꺼짐 카운트, 문 활성화, 게임 클리어
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

    [Header("문 진행도 UI")]
    [SerializeField] private Slider doorProgressBar;

    [Header("게임 설정")]
    [SerializeField] private int requiredCameraOffCount = 4;

    [Networked] public NetworkBool HasScript { get; set; }
    [Networked] public int CameraOffCount { get; set; }
    [Networked] public NetworkBool IsDoorActivated { get; set; }
    [Networked] public NetworkBool IsGameClear { get; set; }

    private float doorProgress = 0f;
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
        if (doorProgressBar != null) doorProgressBar.gameObject.SetActive(false);
    }

    public override void Render()
    {
        // 문 활성화 동기화
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

            if (doorProgressBar != null)
            {
                doorProgressBar.gameObject.SetActive(true);
                doorProgressBar.value = doorProgress / doorInteractTime;
            }

            if (doorProgress >= doorInteractTime)
                RPC_GameClear();
        }
        else if (!Input.GetKey(KeyCode.F))
        {
            ResetDoorProgress();
        }
    }

    private void ResetDoorProgress()
    {
        doorProgress = 0f;
        if (doorProgressBar != null)
        {
            doorProgressBar.value = 0f;
            doorProgressBar.gameObject.SetActive(false);
        }
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

    public void OnScriptPickedUp()
    {
        if (!HasStateAuthority) return;
        HasScript = true;
        Debug.Log("[GameStateManager] 각본 획득");
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
        StartCoroutine(GameClearRoutine());
    }

    private IEnumerator GameClearRoutine()
    {
        yield return new WaitForSeconds(1f);
        Debug.Log("[GameStateManager] 게임 종료");
    }
}
