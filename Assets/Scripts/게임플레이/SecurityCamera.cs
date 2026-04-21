using Fusion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SecurityCamera - 보안 카메라
/// Generator.cs 구조 기반 + Fusion 네트워크 동기화
///
/// [라이트 동기화 방식]
///   서버가 IsLightOn/IsCameraOff 변경
///   → Render()에서 모든 클라이언트 라이트 상태 반영
///   (RPC 대신 Render() 사용 - 더 안정적)
/// </summary>
public class SecurityCamera : NetworkBehaviour
{
    [Header("스포트라이트")]
    [SerializeField] private Light spotLight;

    [Header("스폰 포인트")]
    [SerializeField] public Transform spawnPoint; // 카메라 끄기 완료 후 연기자 이동 위치

    [Header("미니게임")]
    [SerializeField] public GameObject miniGamePanel;
    [SerializeField] public float minTriggerTime = 2f;
    [SerializeField] public float maxTriggerTime = 5f;
    [SerializeField] public float actSpeed = 1f;
    [SerializeField] public float maxActPoint = 10f; // ProgressBar의 maxGage와 동일하게 설정

    [Header("진행도 UI")]
    [SerializeField] public Slider progressBar;

    // 네트워크 동기화 - 서버가 바꾸면 Render()에서 모든 클라이언트 반영
    [Networked] public NetworkBool IsLightOn { get; set; }
    [Networked] public NetworkBool IsCameraOff { get; set; }

    // 미니게임 상태 (ActorInteraction에서 접근)
    public float actPoint = 0f;
    public float miniGameTime = 0f;
    public bool isMiniGameActive = false;
    public PoseGame poseGameScript;

    public override void Spawned()
    {
        if (Object == null || !Object.IsValid) return;

        // public 직렬화 필드는 씬/프리팹에 저장된 값이 남을 수 있으므로
        // Spawned()에서 명시적으로 초기화해 이전 테스트 데이터가 시작 시 남는 문제를 방지
        actPoint = 0f;
        miniGameTime = 0f;
        isMiniGameActive = false;

        if (spotLight == null)
            spotLight = GetComponentInChildren<Light>(true);

        if (spotLight != null)
            spotLight.enabled = false;
        else
            Debug.LogError($"[SecurityCamera] {gameObject.name} - Light 컴포넌트 없음!");

        // RegisterCamera는 연기자가 실제로 카메라 작업을 시작할 때(ActorInteraction.HandleInput)
        // 호출하므로 여기서 자동 등록하지 않는다.
        // 자동 등록 시 마지막으로 Spawned된 카메라가 항상 ProgressBar에 물려
        // 작업 중이지 않아도 ProgressBar가 해당 카메라를 계속 참조하는 문제가 생김
    }

    // FilmItem → 누구든 호출 가능, RPC로 모든 클라이언트에 동기화
    public void TurnOnLight()
    {
        IsLightOn = true;
        Rpc_TurnOnLight();
        Debug.Log($"[SecurityCamera] {gameObject.name} 라이트 ON");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_TurnOnLight()
    {
        if (spotLight == null)
            spotLight = GetComponentInChildren<Light>(true);
        if (spotLight != null)
        {
            spotLight.gameObject.SetActive(true);
            spotLight.enabled = true;
        }
        Debug.Log($"[SecurityCamera] {gameObject.name} 라이트 ON RPC 수신");
    }

    // ActorInteraction에서 직접 호출
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TurnOff()
    {
        if (IsCameraOff) return;
        IsCameraOff = true;
        IsLightOn = false;
        Debug.Log($"[SecurityCamera] {gameObject.name} 꺼짐");
        Rpc_TurnOffLight();
        GameStateManager.Instance?.OnCameraOff();
    }

    // ActorInteraction → 서버에서 호출
    public void TurnOff()
    {
        if (Runner == null || !Runner.IsServer) return;
        if (IsCameraOff) return;
        IsCameraOff = true;
        IsLightOn = false;
        Debug.Log($"[SecurityCamera] {gameObject.name} 꺼짐");
        Rpc_TurnOffLight();
        GameStateManager.Instance?.OnCameraOff();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_TurnOffLight()
    {
        if (spotLight == null)
            spotLight = GetComponentInChildren<Light>(true);
        if (spotLight != null)
        {
            spotLight.enabled = false;
            spotLight.gameObject.SetActive(false);
        }
        Debug.Log($"[SecurityCamera] {gameObject.name} 라이트 OFF RPC 수신");
    }

    // PoseGame에서 호출
    public void EndMiniGame()
    {
        isMiniGameActive = false;
        miniGameTime = 0f;
        if (miniGamePanel != null) miniGamePanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}