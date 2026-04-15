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

    private void Awake()
    {
        // KeySetting이 비어있으면 여기서 기본값으로 채움
        if (KeySetting.keys.Count == 0)
        {
            foreach (var pair in fallbackKeys)
                KeySetting.keys[pair.Key] = pair.Value;
            Debug.Log("[InputProvider] KeySetting 비어있어 기본값으로 초기화");
        }
    }

    bool GetKeySafe(KeyAction action)
    {
        // KeySetting 우선, 없으면 fallback 사용
        if (KeySetting.keys.TryGetValue(action, out var key))
            return Input.GetKey(key);
        if (fallbackKeys.TryGetValue(action, out var fallback))
            return Input.GetKey(fallback);
        return false;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new PlayerNetworkInput();

        Vector2 move = Vector2.zero;
        if (GetKeySafe(KeyAction.LEFT))  move.x -= 1f;
        if (GetKeySafe(KeyAction.RIGHT)) move.x += 1f;
        if (GetKeySafe(KeyAction.UP))    move.y += 1f;
        if (GetKeySafe(KeyAction.DOWN))  move.y -= 1f;
        data.move = move.normalized;

        data.look = new Vector2(
            Input.GetAxisRaw("Mouse X"),
            Input.GetAxisRaw("Mouse Y")
        );

        data.buttons.Set(PlayerNetworkInput.WALK,   GetKeySafe(KeyAction.WALK));
        data.buttons.Set(PlayerNetworkInput.SIT,    GetKeySafe(KeyAction.SIT));
        data.buttons.Set(PlayerNetworkInput.HEAL,   GetKeySafe(KeyAction.HEAL));
        data.buttons.Set(PlayerNetworkInput.VAULT,  GetKeySafe(KeyAction.VAULT));
        data.buttons.Set(PlayerNetworkInput.ATTACK, GetKeySafe(KeyAction.INTERACTION));

        input.Set(data);
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