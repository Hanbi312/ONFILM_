using Fusion;
using UnityEngine;

public class NetworkPlayer : NetworkBehaviour
{
    // 기존 네트워크 변수
    [Networked] public string Nickname { get; set; }
    [Networked] public int RoleValue { get; set; }
    [Networked] public NetworkBool IsAccepted { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }

    // 캐릭터/무기 선택 결과 동기화
    // 클라이언트가 RPC_SendSelection()으로 서버에 전송하면
    // 서버가 이 변수에 저장 → 모든 클라이언트에 자동 동기화
    [Networked] public NetworkString<_32> SelectedCharacterName { get; set; }
    [Networked] public NetworkString<_32> SelectedWeaponName { get; set; }

    // 서버가 선택 데이터를 수신했는지 여부 (스폰 대기 조건 확인용)
    [Networked] public NetworkBool HasSentSelection { get; set; }

    public override void Spawned()
    {
        Debug.Log($"[NetworkPlayer] Spawned | {Nickname}");

        if (FusionLobbyManager.Instance != null)
            FusionLobbyManager.Instance.RegisterNetworkPlayer(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Debug.Log($"[NetworkPlayer] Despawned | {Nickname}");

        if (FusionLobbyManager.Instance != null)
            FusionLobbyManager.Instance.UnregisterNetworkPlayer(this);
    }

    public void Initialize(string nickname, MatchRole role, bool isAccepted)
    {
        Nickname = nickname;
        RoleValue = (int)role;
        IsAccepted = isAccepted;
        IsReady = false;
        HasSentSelection = false;
        SelectedCharacterName = "";
        SelectedWeaponName = "";
    }

    public bool CanReadNetworkState()
    {
        return Object != null && Object.IsValid;
    }

    public MatchRole GetRole()
    {
        if (!CanReadNetworkState()) return MatchRole.None;
        return (MatchRole)RoleValue;
    }

    public bool GetSafeReady()
    {
        if (!CanReadNetworkState()) return false;
        return IsReady;
    }

    // 준비 상태 변경 RPC
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetReady(bool ready)
    {
        IsReady = ready;
        Debug.Log($"[NetworkPlayer] RPC_SetReady | {Nickname} | {IsReady}");
    }

    // 캐릭터/무기 선택 결과를 서버에 전송
    // 게임플레이 씬 진입 후 GameplayManager.SendMySelectionToServer()에서 호출
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SendSelection(string characterName, string weaponName)
    {
        SelectedCharacterName = characterName;
        SelectedWeaponName = weaponName;
        HasSentSelection = true;
        Debug.Log($"[NetworkPlayer] RPC_SendSelection | {Nickname} | 캐릭터={characterName} | 무기={weaponName}");
    }
}
