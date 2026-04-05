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
        if (!killerController.HasInputAuthority && !killerController.HasStateAuthority) return;

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

        foreach (var actor in actors)
        {
            if (!actor.IsDead) continue;
            if (actor.IsCarried) continue;
            float dist = Vector3.Distance(transform.position, actor.transform.position);
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

        // 연기자를 spawnPoint로 텔레포트
        if (cam.spawnPoint != null)
            carriedActor.RPC_Teleport(cam.spawnPoint.position, cam.spawnPoint.rotation);

        // 내려놓기
        killerController.RPC_StopCarry(carriedActor.GetComponent<NetworkObject>().Id);
        carriedActor = null;
        isCarrying = false;

        // 서버에 VillainCamera 활성화 요청
        cam.RPC_StartMiniGame();

        GameStateManager.Instance?.HideFKeyHint();
        Debug.Log("[KillerInteraction] VillainCamera 상호작용 시작 → 연기자 화면에 미니게임 표시");
    }
}
