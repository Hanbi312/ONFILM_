using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using UnityEngine;

public class InputProvider : MonoBehaviour, INetworkRunnerCallbacks
{
    // KeyManager가 늦게 뜨거나 없어도 항상 동작하도록 기본값 하드코딩
    private static readonly Dictionary<KeyAction, KeyCode> fallbackKeys = new Dictionary<KeyAction, KeyCode>
    {
        { KeyAction.UP,          KeyCode.W           },
        { KeyAction.DOWN,        KeyCode.S           },
        { KeyAction.LEFT,        KeyCode.A           },
        { KeyAction.RIGHT,       KeyCode.D           },
        { KeyAction.WALK,        KeyCode.LeftShift   },
        { KeyAction.SIT,         KeyCode.LeftControl },
        { KeyAction.INTERACTION, KeyCode.Mouse0      },
        { KeyAction.SKILL,       KeyCode.Mouse1      },
        { KeyAction.TRAITA,      KeyCode.E           },
        { KeyAction.TRAITB,      KeyCode.R           },
        { KeyAction.HEAL,        KeyCode.H           },
        { KeyAction.VAULT,       KeyCode.V           },
    };

    // [DEBUG] "OnInput 호출됨" 로그 1회만 출력 플래그
    private bool loggedOnInputOnce = false;
    // [DEBUG] 입력값 로그 쿨다운
    private float nextInputLogTime = 0f;

    private void Awake()
    {
        // 인게임 진입 시 항상 기본 키로 강제 초기화
        KeySetting.keys.Clear();
        foreach (var pair in fallbackKeys)
            KeySetting.keys[pair.Key] = pair.Value;
        Debug.Log("[InputProvider] 인게임 키 설정 기본값으로 초기화 완료");
    }

    bool GetKeySafe(KeyAction action)
    {
        // 인게임에서는 항상 기본 키만 사용 (키세팅 무시)
        if (fallbackKeys.TryGetValue(action, out var key))
            return Input.GetKey(key);
        return false;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // 입력 처리는 FusionLobbyManager.OnInput에서 일괄 처리
        // InputProvider가 runner에 등록되어 있어도 여기서는 아무것도 하지 않음
        // (두 OnInput이 동시에 input.Set()하면 마지막 값이 덮어써져 방향 고정 버그 발생)
    }

    // INetworkRunnerCallbacks 필수 구현
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}