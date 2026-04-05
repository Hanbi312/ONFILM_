using Fusion;
using UnityEngine;

/// <summary>
/// VillainCamera - 악역용 카메라
/// NetworkBehaviour로 isMiniGameActive 네트워크 동기화
/// </summary>
public class VillainCamera : NetworkBehaviour
{
    [Header("스폰 포인트 (연기자가 보내질 위치)")]
    public Transform spawnPoint;

    [Header("상호작용 범위")]
    public float interactRange = 2f;

    [Header("미니게임 설정")]
    public float actSpeed = 1f;
    public float maxActPoint = 10f;

    // [Networked]로 모든 클라이언트에 동기화
    [Networked] public NetworkBool IsMiniGameActiveNet { get; set; }

    // 로컬 상태
    public float actPoint = 0f;
    public float miniGameTime = 0f;
    public PoseGame poseGameScript;

    // 기존 코드와 호환을 위한 프로퍼티
    public bool isMiniGameActive
    {
        get => IsMiniGameActiveNet;
        set
        {
            if (HasStateAuthority)
                IsMiniGameActiveNet = value;
        }
    }

    public void EndMiniGame()
    {
        if (HasStateAuthority)
            IsMiniGameActiveNet = false;
        miniGameTime = 0f;
        MiniGameManager.Instance?.HideAll();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StartMiniGame()
    {
        IsMiniGameActiveNet = true;
        actPoint = 0f;
        Debug.Log("[VillainCamera] 미니게임 시작 - 모든 클라이언트에 동기화");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StopMiniGame()
    {
        IsMiniGameActiveNet = false;
        actPoint = 0f;
        Debug.Log("[VillainCamera] 미니게임 종료 - 모든 클라이언트에 동기화");
    }
}
