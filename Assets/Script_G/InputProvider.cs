using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using UnityEngine;

// NetworkRunner의 입력 수집 콜백
// KeySetting 시스템은 오직 여기서만 Input.GetKey()로 읽음
// 씬에 빈 오브젝트를 만들고 이 컴포넌트를 붙인 뒤,
// NetworkRunner에 AddCallbacks(this)로 등록해야 함
public class InputProvider : MonoBehaviour, INetworkRunnerCallbacks
{
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new PlayerNetworkInput();

        // 이동
        Vector2 move = Vector2.zero;
        if (Input.GetKey(KeySetting.keys[KeyAction.LEFT])) move.x -= 1f;
        if (Input.GetKey(KeySetting.keys[KeyAction.RIGHT])) move.x += 1f;
        if (Input.GetKey(KeySetting.keys[KeyAction.UP])) move.y += 1f;
        if (Input.GetKey(KeySetting.keys[KeyAction.DOWN])) move.y -= 1f;
        data.move = move.normalized;

        // 마우스 시선
        data.look = new Vector2(
            Input.GetAxisRaw("Mouse X"),
            Input.GetAxisRaw("Mouse Y")
        );

        // 버튼
        data.buttons.Set(PlayerNetworkInput.WALK, Input.GetKey(KeySetting.keys[KeyAction.WALK]));
        data.buttons.Set(PlayerNetworkInput.SIT, Input.GetKey(KeySetting.keys[KeyAction.SIT]));
        data.buttons.Set(PlayerNetworkInput.HEAL, Input.GetKey(KeySetting.keys[KeyAction.HEAL]));
        data.buttons.Set(PlayerNetworkInput.VAULT, Input.GetKey(KeySetting.keys[KeyAction.VAULT]));

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