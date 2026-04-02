
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FusionLobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static FusionLobbyManager Instance;

    [Header("Runner Prefab")]
    [SerializeField] private NetworkRunner runnerPrefab;

    [Header("Network Prefabs")]
    [SerializeField] private NetworkObject networkPlayerPrefab;

    [Header("Match Setting")]
    [SerializeField] private int maxVillainCount = 1;
    [SerializeField] private int maxActorCount = 1;

    [Header("Scene Setting")]
    [SerializeField] private int characterSelectSceneBuildIndex = 2;
    [SerializeField] private int gameplaySceneBuildIndex = 3;

    private int currentVillainCount = 0;
    private int currentActorCount = 0;

    private bool isStartingCharacterSelect = false;
    private bool isStartingGameplay = false;

    private NetworkRunner runner;
    private List<SessionInfo> cachedSessionList = new List<SessionInfo>();
    private MatchRole selectedRole = MatchRole.None;

    private bool isSearching = false;
    private bool hasTriedJoinOrCreate = false;

    private readonly Dictionary<PlayerRef, NetworkPlayer> spawnedPlayers = new Dictionary<PlayerRef, NetworkPlayer>();
    private readonly Dictionary<PlayerRef, MatchRole> pendingRoles = new Dictionary<PlayerRef, MatchRole>();
    private readonly Dictionary<PlayerRef, string> pendingNicknames = new Dictionary<PlayerRef, string>();
    private readonly Dictionary<PlayerRef, NetworkPlayer> registeredPlayers = new Dictionary<PlayerRef, NetworkPlayer>();

    public event Action<string> OnStatusChanged;
    public event Action<int, int> OnPlayerCountChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private int GetMaxPlayerCount() => maxVillainCount + maxActorCount;

    private void ResetRoleCounts()
    {
        currentVillainCount = 0;
        currentActorCount = 0;
    }

    private void RecalculateRoleCounts()
    {
        ResetRoleCounts();

        foreach (var player in GetAllNetworkPlayers())
        {
            if (player == null || !player.CanReadNetworkState())
                continue;

            MatchRole role = player.GetRole();
            if (role == MatchRole.Villain) currentVillainCount++;
            else if (role == MatchRole.Actor) currentActorCount++;
        }

        Debug.Log($"[FusionLobbyManager] 역할 재계산 완료 | Villain={currentVillainCount}/{maxVillainCount}, Actor={currentActorCount}/{maxActorCount}");
    }

    public void SetSelectedRole(MatchRole role) => selectedRole = role;
    public MatchRole GetSelectedRole() => selectedRole;
    public NetworkRunner GetRunner() => runner;

    public NetworkPlayer GetLocalNetworkPlayer()
    {
        if (runner == null) return null;
        TryResolveNetworkPlayer(runner.LocalPlayer, out var player);
        return player;
    }

    public List<NetworkPlayer> GetAllNetworkPlayers()
    {
        List<NetworkPlayer> result = new List<NetworkPlayer>();
        if (runner == null) return result;

        foreach (var playerRef in runner.ActivePlayers)
        {
            if (TryResolveNetworkPlayer(playerRef, out var player))
                result.Add(player);
        }

        return result;
    }

    private bool TryResolveNetworkPlayer(PlayerRef playerRef, out NetworkPlayer networkPlayer)
    {
        networkPlayer = null;

        // 1) 이미 등록된 캐시
        if (registeredPlayers.TryGetValue(playerRef, out var cachedPlayer))
        {
            if (cachedPlayer != null && cachedPlayer.Object != null && cachedPlayer.CanReadNetworkState())
            {
                networkPlayer = cachedPlayer;
                return true;
            }
            registeredPlayers.Remove(playerRef);
        }

        // 2) Spawned()에서 등록된 캐시
        if (spawnedPlayers.TryGetValue(playerRef, out var spawnedPlayer))
        {
            if (spawnedPlayer != null && spawnedPlayer.Object != null && spawnedPlayer.CanReadNetworkState())
            {
                registeredPlayers[playerRef] = spawnedPlayer;
                networkPlayer = spawnedPlayer;
                return true;
            }
            spawnedPlayers.Remove(playerRef);
        }

        // 3) Runner의 PlayerObject
        if (runner != null && runner.TryGetPlayerObject(playerRef, out var playerObject) && playerObject != null)
        {
            var found = playerObject.GetComponent<NetworkPlayer>();
            if (found != null && found.Object != null && found.CanReadNetworkState())
            {
                spawnedPlayers[playerRef] = found;
                registeredPlayers[playerRef] = found;
                networkPlayer = found;
                return true;
            }
        }

        // 4) 씬 전체 검색 fallback
        // 씬 전환 직후엔 TryGetPlayerObject가 잠깐 실패할 수 있어서 마지막으로 전체 검색한다.
        var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            if (p == null || p.Object == null || !p.CanReadNetworkState())
                continue;

            if (p.Object.InputAuthority == playerRef)
            {
                spawnedPlayers[playerRef] = p;
                registeredPlayers[playerRef] = p;
                networkPlayer = p;
                return true;
            }
        }

        return false;
    }

    public void RegisterNetworkPlayer(NetworkPlayer player)
    {
        if (player == null || player.Object == null)
            return;

        PlayerRef owner = player.Object.InputAuthority;
        spawnedPlayers[owner] = player;
        registeredPlayers[owner] = player;
        Debug.Log($"[FusionLobbyManager] RegisterNetworkPlayer | Owner={owner} | Nickname={player.Nickname}");
    }

    public void UnregisterNetworkPlayer(NetworkPlayer player)
    {
        if (player == null || player.Object == null)
            return;

        PlayerRef owner = player.Object.InputAuthority;
        spawnedPlayers.Remove(owner);
        registeredPlayers.Remove(owner);
        Debug.Log($"[FusionLobbyManager] UnregisterNetworkPlayer | Owner={owner}");
    }

    public async void StartMatchmaking()
    {
        if (selectedRole == MatchRole.None)
        {
            OnStatusChanged?.Invoke("역할을 먼저 선택해주세요.");
            return;
        }

        isSearching = true;
        hasTriedJoinOrCreate = false;
        isStartingCharacterSelect = false;
        isStartingGameplay = false;

        string myNickname = "Guest";
        if (PlayerSession.Instance != null && PlayerSession.Instance.IsLoggedIn)
            myNickname = PlayerSession.Instance.Nickname;

        OnStatusChanged?.Invoke("매칭 서버에 접속 중...");

        await EnsureRunnerExists();

        var joinLobbyResult = await runner.JoinSessionLobby(SessionLobby.ClientServer);
        if (!joinLobbyResult.Ok)
        {
            OnStatusChanged?.Invoke($"로비 접속 실패: {joinLobbyResult.ShutdownReason}");
            isSearching = false;
            return;
        }

        OnStatusChanged?.Invoke("방 목록을 불러오는 중...");
        pendingNicknames[runner.LocalPlayer] = myNickname;
        pendingRoles[runner.LocalPlayer] = selectedRole;
    }

    public async void CancelMatchmaking()
    {
        isSearching = false;
        hasTriedJoinOrCreate = false;
        isStartingCharacterSelect = false;
        isStartingGameplay = false;

        if (runner != null)
        {
            await runner.Shutdown();
            runner = null;
        }

        spawnedPlayers.Clear();
        registeredPlayers.Clear();
        pendingRoles.Clear();
        pendingNicknames.Clear();
        ResetRoleCounts();

        OnStatusChanged?.Invoke("매칭이 취소되었습니다.");
        OnPlayerCountChanged?.Invoke(0, GetMaxPlayerCount());
    }

    private async System.Threading.Tasks.Task EnsureRunnerExists()
    {
        if (runner != null) return;

        runner = Instantiate(runnerPrefab);
        runner.name = "FusionRunner";
        DontDestroyOnLoad(runner.gameObject);

        runner.ProvideInput = true; // 중요: 로컬 입력을 Fusion이 수집
        runner.AddCallbacks(this);

        if (runner.GetComponent<NetworkSceneManagerDefault>() == null)
            runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        await System.Threading.Tasks.Task.CompletedTask;
    }

    private async void TryJoinOrCreateBestSession()
    {
        if (!isSearching) return;

        string targetRoomName = FindBestRoomNameForRole();
        StartGameArgs args;

        if (!string.IsNullOrEmpty(targetRoomName))
        {
            OnStatusChanged?.Invoke("기존 방에 입장하는 중...");
            args = new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = targetRoomName,
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>()
            };
        }
        else
        {
            OnStatusChanged?.Invoke("새 방을 생성하는 중...");
            string newRoomName = $"room_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var properties = new Dictionary<string, SessionProperty>
            {
                { "state", (SessionProperty)"waiting" },
                { "villainCount", (SessionProperty)(selectedRole == MatchRole.Villain ? 1 : 0) },
                { "actorCount", (SessionProperty)(selectedRole == MatchRole.Actor ? 1 : 0) },
                { "createdAt", (SessionProperty)DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
            };

            args = new StartGameArgs
            {
                GameMode = GameMode.Host,
                SessionName = newRoomName,
                PlayerCount = GetMaxPlayerCount(),
                SessionProperties = properties,
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>()
            };
        }

        var result = await runner.StartGame(args);
        if (!result.Ok)
        {
            OnStatusChanged?.Invoke($"매칭 실패: {result.ShutdownReason}");
            isSearching = false;
            return;
        }

        OnStatusChanged?.Invoke("방 입장 완료");
        UpdatePlayerCountUI();
    }

    private string FindBestRoomNameForRole()
    {
        var waitingRooms = cachedSessionList
            .Where(s => s.IsOpen)
            .Where(s => s.Properties != null)
            .Where(s => s.Properties.ContainsKey("state"));

        if (selectedRole == MatchRole.Villain)
        {
            waitingRooms = waitingRooms
                .Where(s => s.Properties.ContainsKey("villainCount"))
                .Where(s => (int)s.Properties["villainCount"] < maxVillainCount);
        }
        else if (selectedRole == MatchRole.Actor)
        {
            waitingRooms = waitingRooms
                .Where(s => s.Properties.ContainsKey("actorCount"))
                .Where(s => (int)s.Properties["actorCount"] < maxActorCount);
        }

        var bestRoom = waitingRooms
            .OrderBy(s => s.Properties.ContainsKey("createdAt") ? (long)s.Properties["createdAt"] : long.MaxValue)
            .FirstOrDefault();

        return bestRoom?.Name;
    }

    private void UpdatePlayerCountUI()
    {
        if (runner == null) return;
        int currentCount = runner.ActivePlayers.Count();
        OnPlayerCountChanged?.Invoke(currentCount, GetMaxPlayerCount());
        Debug.Log($"[FusionLobbyManager] ActivePlayers Count = {currentCount}");
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        cachedSessionList = sessionList;
        if (!isSearching || hasTriedJoinOrCreate) return;
        hasTriedJoinOrCreate = true;
        TryJoinOrCreateBestSession();
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        UpdatePlayerCountUI();
        if (!runner.IsServer) return;

        if (spawnedPlayers.ContainsKey(player) && spawnedPlayers[player] != null && spawnedPlayers[player].CanReadNetworkState())
        {
            RecalculateRoleCounts();
            TryStartCharacterSelectScene();
            return;
        }

        MatchRole role = ResolveRoleForPlayer(player, true);
        string nickname = ResolveNicknameForPlayer(player);

        if (role == MatchRole.None)
        {
            Debug.LogWarning($"[FusionLobbyManager] 역할 결정 실패 | Player={player}");
            return;
        }

        if (role == MatchRole.Villain && currentVillainCount >= maxVillainCount)
        {
            Debug.Log("[FusionLobbyManager] 악역 슬롯 가득 참 -> 입장 거절");
            runner.Disconnect(player);
            return;
        }

        if (role == MatchRole.Actor && currentActorCount >= maxActorCount)
        {
            Debug.Log("[FusionLobbyManager] 연기자 슬롯 가득 참 -> 입장 거절");
            runner.Disconnect(player);
            return;
        }

        NetworkObject playerObject = runner.Spawn(networkPlayerPrefab, Vector3.zero, Quaternion.identity, player);
        runner.SetPlayerObject(player, playerObject);

        NetworkPlayer networkPlayer = playerObject.GetComponent<NetworkPlayer>();
        networkPlayer.Initialize(nickname, role, true);

        spawnedPlayers[player] = networkPlayer;
        registeredPlayers[player] = networkPlayer;

        RecalculateRoleCounts();
        Debug.Log($"[FusionLobbyManager] NetworkPlayer 스폰 완료 | Player={player} | Nickname={nickname} | Role={role}");

        TryStartCharacterSelectScene();
        UpdatePlayerCountUI();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[FusionLobbyManager] Player Left: {player}");

        if (runner.IsServer)
        {
            if (spawnedPlayers.TryGetValue(player, out NetworkPlayer networkPlayer))
            {
                if (networkPlayer != null && networkPlayer.Object != null)
                    runner.Despawn(networkPlayer.Object);

                spawnedPlayers.Remove(player);
            }

            pendingRoles.Remove(player);
            pendingNicknames.Remove(player);
            RecalculateRoleCounts();
            Debug.Log($"[FusionLobbyManager] 퇴장 후 카운트 | Villain={currentVillainCount}/{maxVillainCount}, Actor={currentActorCount}/{maxActorCount}");
        }

        registeredPlayers.Remove(player);
        UpdatePlayerCountUI();
    }

    public async void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        Debug.Log("[FusionLobbyManager] Host Migration 시작");

        await runner.Shutdown(shutdownReason: ShutdownReason.HostMigration);

        NetworkRunner newRunner = Instantiate(runnerPrefab);
        newRunner.name = "FusionRunner_HostMigration";
        DontDestroyOnLoad(newRunner.gameObject);

        newRunner.ProvideInput = true;
        newRunner.AddCallbacks(this);
        if (newRunner.GetComponent<NetworkSceneManagerDefault>() == null)
            newRunner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        this.runner = newRunner;

        var startGameArgs = new StartGameArgs
        {
            GameMode = GameMode.Host,
            HostMigrationToken = hostMigrationToken,
            HostMigrationResume = HostMigrationResume,
            SceneManager = newRunner.GetComponent<NetworkSceneManagerDefault>()
        };

        var result = await newRunner.StartGame(startGameArgs);
        if (!result.Ok)
        {
            Debug.LogError($"[FusionLobbyManager] Host Migration 실패: {result.ShutdownReason}");
            return;
        }

        Debug.Log("[FusionLobbyManager] Host Migration 성공");
    }

    private void HostMigrationResume(NetworkRunner runner)
    {
        Debug.Log("[FusionLobbyManager] HostMigrationResume 시작");

        spawnedPlayers.Clear();
        registeredPlayers.Clear();

        foreach (var obj in runner.GetResumeSnapshotNetworkObjects())
        {
            NetworkPlayer player = obj.GetComponent<NetworkPlayer>();
            if (player == null) continue;

            PlayerRef owner = obj.InputAuthority;
            spawnedPlayers[owner] = player;
            registeredPlayers[owner] = player;
            Debug.Log($"[FusionLobbyManager] 복원됨 → Player={owner} | Nickname={player.Nickname} | Role={player.RoleValue}");
        }

        RecalculateRoleCounts();
        UpdatePlayerCountUI();
        TryStartCharacterSelectScene();
    }

    private bool IsMatchReadyToStart() => currentVillainCount >= maxVillainCount && currentActorCount >= maxActorCount;

    public void TryLoadGameplayScene()
    {
        if (runner == null)
        {
            Debug.LogWarning("[FusionLobbyManager] GameplayScene 이동 실패: runner 없음");
            return;
        }
        if (!runner.IsServer)
        {
            Debug.LogWarning("[FusionLobbyManager] GameplayScene 이동 실패: Host 아님");
            return;
        }
        if (isStartingGameplay) return;

        isStartingGameplay = true;
        Debug.Log("[FusionLobbyManager] GameplayScene 이동 시작");
        runner.LoadScene(SceneRef.FromIndex(gameplaySceneBuildIndex), LoadSceneMode.Single);
    }

    private void TryStartCharacterSelectScene()
    {
        RecalculateRoleCounts();

        if (runner == null)
        {
            Debug.LogWarning("[FusionLobbyManager] runner가 null이라 씬 이동 불가");
            return;
        }
        if (!runner.IsServer)
        {
            Debug.LogWarning("[FusionLobbyManager] Host가 아니라 씬 이동 불가");
            return;
        }
        if (isStartingCharacterSelect) return;

        if (runner.ActivePlayers.Count() < GetMaxPlayerCount())
        {
            Debug.LogWarning($"[FusionLobbyManager] 아직 전체 플레이어 수 부족 | Active={runner.ActivePlayers.Count()} / Need={GetMaxPlayerCount()}");
            return;
        }
        if (!IsMatchReadyToStart())
        {
            Debug.LogWarning("[FusionLobbyManager] 아직 정원 미충족");
            return;
        }
        if (characterSelectSceneBuildIndex < 0)
        {
            Debug.LogError("[FusionLobbyManager] characterSelectSceneBuildIndex가 잘못됨");
            return;
        }

        isStartingCharacterSelect = true;
        Debug.Log($"[FusionLobbyManager] CharacterSelectScene 이동 시작 | BuildIndex={characterSelectSceneBuildIndex}");
        runner.LoadScene(SceneRef.FromIndex(characterSelectSceneBuildIndex), LoadSceneMode.Single);
    }

    private IEnumerator SceneLoadDoneRoutine()
    {
        yield return null;
        yield return null;

        if (runner != null && runner.IsServer)
            RespawnMissingPlayerObjects();

        float timeout = 5f;
        float timer = 0f;

        while (timer < timeout)
        {
            RebuildRegisteredPlayers();

            int activeCount = runner != null ? runner.ActivePlayers.Count() : 0;
            int registeredCount = GetAllNetworkPlayers().Count;

            if (activeCount > 0 && registeredCount >= activeCount)
            {
                Debug.Log($"[FusionLobbyManager] 씬 로드 후 플레이어 재연결 완료 | registered={registeredCount} / active={activeCount}");
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("[FusionLobbyManager] 씬 로드 후 플레이어 재연결 타임아웃");
    }

    private void RebuildRegisteredPlayers()
    {
        if (runner == null)
        {
            Debug.Log("[FusionLobbyManager] RebuildRegisteredPlayers 실패: runner null");
            return;
        }

        foreach (var playerRef in runner.ActivePlayers)
        {
            if (TryResolveNetworkPlayer(playerRef, out NetworkPlayer networkPlayer))
            {
                spawnedPlayers[playerRef] = networkPlayer;
                registeredPlayers[playerRef] = networkPlayer;
                Debug.Log($"[FusionLobbyManager] Rebuild 등록 성공 | PlayerRef={playerRef} | Nickname={networkPlayer.Nickname}");
            }
            else
            {
                Debug.LogWarning($"[FusionLobbyManager] NetworkPlayer 복구 실패 | PlayerRef={playerRef}");
            }
        }
    }

    private void RespawnMissingPlayerObjects()
    {
        if (runner == null || !runner.IsServer)
            return;

        Debug.Log("[FusionLobbyManager] RespawnMissingPlayerObjects 시작");

        foreach (var playerRef in runner.ActivePlayers)
        {
            if (TryResolveNetworkPlayer(playerRef, out NetworkPlayer resolvedPlayer) && resolvedPlayer != null)
            {
                if (resolvedPlayer.Object != null)
                    runner.SetPlayerObject(playerRef, resolvedPlayer.Object);
                continue;
            }

            MatchRole role = ResolveRoleForPlayer(playerRef, false);
            string nickname = ResolveNicknameForPlayer(playerRef);

            if (role == MatchRole.None)
            {
                Debug.LogWarning($"[FusionLobbyManager] PlayerObject 재생성 건너뜀 - 역할 없음 | Player={playerRef}");
                continue;
            }

            NetworkObject playerObject = runner.Spawn(networkPlayerPrefab, Vector3.zero, Quaternion.identity, playerRef);
            runner.SetPlayerObject(playerRef, playerObject);

            NetworkPlayer networkPlayer = playerObject.GetComponent<NetworkPlayer>();
            networkPlayer.Initialize(nickname, role, true);

            spawnedPlayers[playerRef] = networkPlayer;
            registeredPlayers[playerRef] = networkPlayer;
            Debug.Log($"[FusionLobbyManager] PlayerObject 재생성 완료 | {playerRef} | {nickname} | {role}");
        }

        RecalculateRoleCounts();
    }

    private MatchRole ResolveRoleForPlayer(PlayerRef player, bool saveResolvedRole)
    {
        if (pendingRoles.TryGetValue(player, out MatchRole savedRole) && savedRole != MatchRole.None)
            return savedRole;

        MatchRole resolvedRole = MatchRole.None;
        if (player == runner.LocalPlayer) resolvedRole = selectedRole;
        else if (selectedRole == MatchRole.Villain) resolvedRole = MatchRole.Actor;
        else if (selectedRole == MatchRole.Actor) resolvedRole = MatchRole.Villain;

        if (saveResolvedRole && resolvedRole != MatchRole.None)
            pendingRoles[player] = resolvedRole;

        return resolvedRole;
    }

    private string ResolveNicknameForPlayer(PlayerRef player)
    {
        if (pendingNicknames.TryGetValue(player, out string savedNickname) && !string.IsNullOrWhiteSpace(savedNickname))
            return savedNickname;

        string nickname = player == runner.LocalPlayer && PlayerSession.Instance != null && PlayerSession.Instance.IsLoggedIn
            ? PlayerSession.Instance.Nickname
            : $"Player_{player.PlayerId}";

        pendingNicknames[player] = nickname;
        return nickname;
    }

    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[FusionLobbyManager] Shutdown 발생: {shutdownReason}");

        if (shutdownReason == ShutdownReason.HostMigration)
        {
            Debug.Log("[FusionLobbyManager] Host Migration용 종료");
            return;
        }

        OnStatusChanged?.Invoke("네트워크 연결이 종료되었습니다.");
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerNetworkInput data = new PlayerNetworkInput();

        data.move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        data.look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        NetworkButtons buttons = default;
        if (Input.GetKey(KeyCode.LeftShift)) buttons.Set(PlayerNetworkInput.WALK, true);
        if (Input.GetKey(KeyCode.LeftControl)) buttons.Set(PlayerNetworkInput.SIT, true);
        if (Input.GetKey(KeyCode.H)) buttons.Set(PlayerNetworkInput.HEAL, true);
        if (Input.GetKeyDown(KeyCode.V)) buttons.Set(PlayerNetworkInput.VAULT, true);
        if (Input.GetMouseButtonDown(0)) buttons.Set(PlayerNetworkInput.ATTACK, true);

        data.buttons = buttons;
        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("[FusionLobbyManager] OnSceneLoadDone 호출");
        isStartingCharacterSelect = false;
        isStartingGameplay = false;
        StartCoroutine(SceneLoadDoneRoutine());
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
