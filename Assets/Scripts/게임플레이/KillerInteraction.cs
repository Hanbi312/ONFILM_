using Fusion;
using UnityEngine;

/// <summary>
/// KillerInteraction - 악역 상호작용
/// 악역은 연기자 들기/내려놓기 + VillainCamera 상호작용만 처리
/// 미니게임 UI는 연기자(ActorInteraction)가 처리
/// </summary>
public class KillerInteraction : MonoBehaviour
{
    [Header("상호작용 범위")]
    [SerializeField] private float interactRange = 2f;

    private KillerController killerController;
    private ActorController carriedActor = null;
    private bool isCarrying = false;

    private void Start()
    {
        killerController = GetComponent<KillerController>();
    }

    private void Update()
    {
        if (killerController == null) return;
        if (!killerController.HasInputAuthority) return;

        if (isCarrying)
        {
            // 들고 있는 상태에서 VillainCamera 감지
            VillainCamera nearCam = DetectVillainCamera();
            if (nearCam != null && !nearCam.isMiniGameActive)
            {
                GameStateManager.Instance?.ShowFKeyHint("카메라 상호작용");
                if (Input.GetKeyDown(KeyCode.F))
                {
                    StartVillainCameraAct(nearCam);
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

        // 사망한 연기자 감지
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

    private ActorController DetectDeadActor()
    {
        var actors = FindObjectsByType<ActorController>(FindObjectsSortMode.None);
        ActorController closest = null;
        float closestDist = interactRange;

        // 현재 악역 카메라 미니게임 진행 중인 연기자 ID 수집 (네트워크 동기화된 값)
        var busyActorIds = new System.Collections.Generic.HashSet<NetworkId>();
        var villainCams = FindObjectsByType<VillainCamera>(FindObjectsSortMode.None);
        foreach (var cam in villainCams)
        {
            if (cam.IsMiniGameActiveNet)
                busyActorIds.Add(cam.TargetActorId);
        }

        foreach (var actor in actors)
        {
            if (!actor.IsDead) continue;
            if (actor.IsCarried) continue;

            // 악역 카메라 미니게임 진행 중인 연기자는 상호작용 불가 (네트워크 동기화 기반)
            if (actor.Object != null && busyActorIds.Contains(actor.Object.Id)) continue;

            // 수평(XZ) 거리만 사용 → 높은 오브젝트 위에 있어도 바로 아래에서 감지 가능
            Vector3 delta = actor.transform.position - transform.position;
            float dist = new Vector2(delta.x, delta.z).magnitude;
            if (dist < closestDist) { closestDist = dist; closest = actor; }
        }
        return closest;
    }

    private VillainCamera DetectVillainCamera()
    {
        var cams = FindObjectsByType<VillainCamera>(FindObjectsSortMode.None);
        VillainCamera closest = null;
        float closestDist = interactRange;

        foreach (var cam in cams)
        {
            float dist = Vector3.Distance(transform.position, cam.transform.position);
            if (dist < closestDist) { closestDist = dist; closest = cam; }
        }
        return closest;
    }

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

    private void StartVillainCameraAct(VillainCamera cam)
    {
        if (carriedActor == null) return;

        var actorNetId = carriedActor.GetComponent<NetworkObject>().Id;
        var actor = carriedActor; // null 처리 전에 참조 저장

        // ★ 순서 중요: StopCarry를 먼저 → FixedUpdateNetwork의 carry 위치 덮어쓰기 차단
        // 이전 코드(Teleport → StopCarry)는 두 RPC 사이에 FixedUpdateNetwork 한 틱이 실행되면
        // carriedActorRef.position = killer 위치로 덮어써 텔레포트가 무시됐음
        killerController.RPC_StopCarry(actorNetId);
        carriedActor = null;
        isCarrying = false;

        // carry 해제 후 spawnPoint로 텔레포트 (이제 carry 시스템이 위치를 덮어쓰지 않음)
        if (cam.spawnPoint != null)
            actor.RPC_Teleport(cam.spawnPoint.position, cam.spawnPoint.rotation);

        // 서버에 VillainCamera 미니게임 활성화 요청
        cam.RPC_StartMiniGame(actorNetId);

        GameStateManager.Instance?.HideFKeyHint();
        Debug.Log("[KillerInteraction] VillainCamera 상호작용 시작 → 연기자 화면에 미니게임 표시");
    }
}