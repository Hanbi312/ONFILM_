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
    [SerializeField] private int characterSelectSceneBuildIndex = 3;
    [SerializeField] private int gameplaySceneBuildIndex = 4;

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
    private readonly Queue<(MatchRole role, string nickname)> pendingConnectionTokens = new Queue<(MatchRole, string)>();

    public event Action<string> OnStatusChanged;
    public event Action<int, int> OnPlayerCountChanged;

    // ─── 포커스 복귀 시 입력 재설정 ─────────────────────────────────────
    // Alt-Tab 등으로 창 포커스가 빠질 때 누르고 있던 키의 KeyUp 이벤트를
    // Unity가 받지 못해, 포커스 복귀 후에도 "키가 계속 눌린" 상태가 유지됨.
    // → 마지막으로 밟고 있던 키(예: S)가 고정돼서 뭘 눌러도 뒤로만 가는 현상.
    // 포커스 복귀 직후 짧은 시간 동안 입력을 무시해서 Unity가 실제 키 상태를
    // 다시 폴링하도록 유도함.
    private float inputIgnoreUntil = 0f;
    private const float FocusRegainBlockDuration = 0.15f;

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

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) return;

        // 포커스 복귀 시 유니티 Input 축을 초기화하고 잠깐 입력을 무시
        // → Alt-Tab 중에 고정된 키 상태(특히 S) 해제
        Input.ResetInputAxes();
        inputIgnoreUntil = Time.realtimeSinceStartup + FocusRegainBlockDuration;

        // 게임플레이 중이면 커서도 다시 잠금 (Alt-Tab 시 Unity가 자동 해제함)
        if (MouseLock.Instance != null && MouseLock.Instance.currentState == GameState.Gameplay)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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
            // Object 참조가 죽은 경우만 캐시에서 제거 (IsValid=false는 일시적이므로 유지)
            if (cachedPlayer == null || cachedPlayer.Object == null)
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
            if (spawnedPlayer == null || spawnedPlayer.Object == null)
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
                GameMode = GameMode.AutoHostOrClient,
                SessionName = targetRoomName,
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
                ConnectionToken = System.Text.Encoding.UTF8.GetBytes($"{(int)selectedRole}|{PlayerSession.Instance?.Nickname ?? "Guest"}"),
                // 클라이언트도 HostMigrationResume 반드시 등록해야 Migration 동작
                HostMigrationResume = HostMigrationResume,
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
                GameMode = GameMode.AutoHostOrClient,
                SessionName = newRoomName,
                PlayerCount = GetMaxPlayerCount(),
                SessionProperties = properties,
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
                // Host Migration 활성화: 호스트가 나가도 다른 클라이언트가 새 호스트로 승격
                HostMigrationToken = null,
                HostMigrationResume = HostMigrationResume,
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

        // 토큰 큐에서 역할/닉네임 꺼내기 (클라이언트가 입장 시 전달한 정보)
        if (player != runner.LocalPlayer && pendingConnectionTokens.Count > 0)
        {
            var (role, nickname) = pendingConnectionTokens.Dequeue();
            pendingRoles[player] = role;
            pendingNicknames[player] = nickname;
            Debug.Log($"[FusionLobbyManager] 토큰에서 역할 설정 | Player={player} | Role={role} | Nickname={nickname}");
        }

        MatchRole resolvedRole = ResolveRoleForPlayer(player, true);
        if (resolvedRole == MatchRole.None)
        {
            Debug.Log($"[FusionLobbyManager] 역할 미수신 - 잠시 후 재시도 | Player={player}");
            StartCoroutine(WaitAndSpawnPlayer(player));
            return;
        }

        SpawnNetworkPlayer(player, resolvedRole);
    }

    private IEnumerator WaitAndSpawnPlayer(PlayerRef player)
    {
        float timer = 0f;
        while (timer < 5f)
        {
            yield return new WaitForSeconds(0.2f);
            timer += 0.2f;

            MatchRole role = ResolveRoleForPlayer(player, true);
            if (role != MatchRole.None)
            {
                SpawnNetworkPlayer(player, role);
                yield break;
            }
        }
        Debug.LogWarning($"[FusionLobbyManager] 역할 수신 타임아웃 | Player={player}");
    }

    private void SpawnNetworkPlayer(PlayerRef player, MatchRole role)
    {
        string nickname = ResolveNicknameForPlayer(player);

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

        runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty>
        {
            { "villainCount", (SessionProperty)currentVillainCount },
            { "actorCount", (SessionProperty)currentActorCount }
        });

        Debug.Log($"[FusionLobbyManager] NetworkPlayer 스폰 완료 | Player={player} | Nickname={nickname} | Role={role}");

        TryStartCharacterSelectScene();
        UpdatePlayerCountUI();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[FusionLobbyManager] Player Left: {player}");

        if (runner.IsServer)
        {
            // 게임플레이 씬 중 퇴장 시 → 캐릭터는 유지 (despawn 안 함)
            // 씬 인덱스로 게임플레이 씬인지 확인
            bool isInGameplayScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex == gameplaySceneBuildIndex;

            if (isInGameplayScene)
            {
                // 퇴장 플레이어의 캐릭터 오브젝트는 그대로 두되 InputAuthority를 서버로 이전
                // → 이렇게 하면 캐릭터가 씬에 남아 다른 플레이어들에게 보임
                if (spawnedPlayers.TryGetValue(player, out NetworkPlayer networkPlayer))
                {
                    if (networkPlayer != null && networkPlayer.Object != null)
                    {
                        // NetworkPlayer 오브젝트만 despawn (캐릭터 오브젝트는 GameplayManager가 관리)
                        runner.Despawn(networkPlayer.Object);
                    }
                    spawnedPlayers.Remove(player);
                }

                // 퇴장 플레이어의 게임 캐릭터(ActorController/KillerController)는 despawn하지 않음
                // → 씬에 남겨두어 나머지 플레이어가 계속 플레이 가능
                Debug.Log($"[FusionLobbyManager] 게임 씬 중 퇴장 - 캐릭터 유지 | Player={player}");
            }
            else
            {
                // 로비/캐릭터선택 씬에서 퇴장 시 → 기존처럼 NetworkPlayer despawn
                if (spawnedPlayers.TryGetValue(player, out NetworkPlayer networkPlayer))
                {
                    if (networkPlayer != null && networkPlayer.Object != null)
                        runner.Despawn(networkPlayer.Object);

                    spawnedPlayers.Remove(player);
                }
            }

            pendingRoles.Remove(player);
            pendingNicknames.Remove(player);
            RecalculateRoleCounts();
            Debug.Log($"[FusionLobbyManager] 퇴장 후 카운트 | Villain={currentVillainCount}/{maxVillainCount}, Actor={currentActorCount}/{maxActorCount}");
        }

        registeredPlayers.Remove(player);
        UpdatePlayerCountUI();

        // GameStateManager에 플레이어 퇴장 알림 (localActor 재탐색)
        GameStateManager.Instance?.OnPlayerLeft(player);

        // GameplayManager에 플레이어 퇴장 알림 (캐릭터 처리)
        var gameplayManager = FindFirstObjectByType<GameplayManager>();
        gameplayManager?.OnPlayerLeft(player);
    }

    public async void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        Debug.Log("[FusionLobbyManager] ★ Host Migration 시작 - 새 호스트로 승격");

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
            // 새 호스트는 반드시 GameMode.Host로 재접속
            GameMode = GameMode.Host,
            HostMigrationToken = hostMigrationToken,
            HostMigrationResume = HostMigrationResume,
            SceneManager = newRunner.GetComponent<NetworkSceneManagerDefault>()
        };

        // 최대 3회 재시도
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var result = await newRunner.StartGame(startGameArgs);
            if (result.Ok)
            {
                Debug.Log($"[FusionLobbyManager] Host Migration 성공 (시도 {attempt}회)");
                return;
            }
            Debug.LogWarning($"[FusionLobbyManager] Host Migration 실패 (시도 {attempt}/3): {result.ShutdownReason}");
            if (attempt < 3)
                await System.Threading.Tasks.Task.Delay(500);
        }

        Debug.LogError("[FusionLobbyManager] Host Migration 3회 모두 실패 - 연결 종료");
        OnStatusChanged?.Invoke("호스트 이전 실패. 재접속이 필요합니다.");
    }

    private void HostMigrationResume(NetworkRunner runner)
    {
        Debug.Log("[FusionLobbyManager] HostMigrationResume 시작");

        spawnedPlayers.Clear();
        registeredPlayers.Clear();

        foreach (var obj in runner.GetResumeSnapshotNetworkObjects())
        {
            // NetworkPlayer 복원
            NetworkPlayer player = obj.GetComponent<NetworkPlayer>();
            if (player != null)
            {
                PlayerRef owner = obj.InputAuthority;
                spawnedPlayers[owner] = player;
                registeredPlayers[owner] = player;
                Debug.Log($"[FusionLobbyManager] NetworkPlayer 복원 → Player={owner} | Nickname={player.Nickname} | Role={player.RoleValue}");
                continue;
            }

            // 캐릭터 오브젝트 복원
            var actorCtrl = obj.GetComponent<ActorController>();
            if (actorCtrl != null)
            {
                obj.gameObject.SetActive(true);
                actorCtrl.RPC_ResumeAfterMigration();
                Debug.Log($"[FusionLobbyManager] 연기자 복원 → {obj.name}");
                continue;
            }

            var killerCtrl = obj.GetComponent<KillerController>();
            if (killerCtrl != null)
            {
                obj.gameObject.SetActive(true);
                killerCtrl.RPC_ResumeAfterMigration();
                Debug.Log($"[FusionLobbyManager] 악역 복원 → {obj.name}");
                continue;
            }

            // StateAuthority 이전
            if (runner.IsServer && !obj.HasStateAuthority)
                runner.SetPlayerObject(obj.InputAuthority, obj);
        }

        RecalculateRoleCounts();
        UpdatePlayerCountUI();

        // Migration 후 씬 오브젝트(GameStateManager 등) 재연결
        StartCoroutine(RebuildAfterMigrationRoutine());

        Debug.Log("[FusionLobbyManager] HostMigrationResume 완료");
    }

    private IEnumerator RebuildAfterMigrationRoutine()
    {
        // GameStateManager의 Spawned()가 완료될 때까지 대기 (최대 5초)
        float timeout = 5f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;

            var gsm = GameStateManager.Instance;
            if (gsm != null && gsm.Object != null && gsm.Object.IsValid)
                break;
        }

        // GameStateManager - localActor 재탐색
        GameStateManager.Instance?.RebuildAfterMigration();

        // 플레이어 딕셔너리 재구성
        RebuildRegisteredPlayers();

        Debug.Log("[FusionLobbyManager] Migration 후 씬 오브젝트 재연결 완료");
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
                // 씬 전환 직후 Fusion tick 안정화 전에는 정상적으로 실패할 수 있음 → Log (Warning 아님)
                Debug.Log($"[FusionLobbyManager] NetworkPlayer 아직 준비 안 됨 (재시도 중) | PlayerRef={playerRef}");
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
        // pendingRoles에 저장된 역할 우선 사용
        if (pendingRoles.TryGetValue(player, out MatchRole savedRole) && savedRole != MatchRole.None)
            return savedRole;

        // 로컬 플레이어는 selectedRole 사용
        if (player == runner.LocalPlayer)
        {
            if (saveResolvedRole) pendingRoles[player] = selectedRole;
            return selectedRole;
        }

        // 다른 플레이어는 pendingRoles에 없으면 None 반환 (역할 수신 대기)
        return MatchRole.None;
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

    public void SetPendingRole(PlayerRef player, MatchRole role, string nickname)
    {
        pendingRoles[player] = role;
        if (!string.IsNullOrEmpty(nickname))
            pendingNicknames[player] = nickname;
        Debug.Log($"[FusionLobbyManager] SetPendingRole | Player={player} | Role={role} | Nickname={nickname}");
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[FusionLobbyManager] 서버 연결 완료 - 역할 전송");
        // 클라이언트가 연결되면 즉시 역할을 pendingRoles에 저장
        pendingRoles[runner.LocalPlayer] = selectedRole;
        pendingNicknames[runner.LocalPlayer] = PlayerSession.Instance?.Nickname ?? $"Player_{runner.LocalPlayer.PlayerId}";
    }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        if (token != null && token.Length > 0)
        {
            try
            {
                string tokenStr = System.Text.Encoding.UTF8.GetString(token);
                string[] parts = tokenStr.Split('|');
                if (parts.Length >= 2)
                {
                    int roleValue = int.Parse(parts[0]);
                    string nickname = parts[1];
                    MatchRole role = (MatchRole)roleValue;

                    // 연결 요청한 플레이어의 역할을 미리 저장
                    // request.Accept()는 항상 호출해야 연결이 수락됨
                    Debug.Log($"[FusionLobbyManager] 연결 요청 | Role={role} | Nickname={nickname}");

                    // 토큰 데이터를 임시 저장 (PlayerRef는 아직 모름)
                    pendingConnectionTokens.Enqueue((role, nickname));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FusionLobbyManager] 토큰 파싱 실패: {e.Message}");
            }
        }
        request.Accept();
    }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[FusionLobbyManager] Shutdown 발생: {shutdownReason}");

        if (shutdownReason == ShutdownReason.HostMigration)
        {
            Debug.Log("[FusionLobbyManager] Host Migration용 종료 - 게임 계속 유지");
            return;
        }

        // DisconnectedByPluginLogic: 플레이어가 의도적으로 종료한 경우
        // 나머지 플레이어가 있으면 Migration으로 이어지므로 여기는 마지막 플레이어 퇴장 시만 도달
        OnStatusChanged?.Invoke("네트워크 연결이 종료되었습니다.");
    }

    // 디버그 로그 쓰로틀링
    private float lastInputLogTime = 0f;

    // 커서 잠금 상태 추적 (전환 감지용)
    private bool wasCursorLocked = true;

    // 킬러 절대 Yaw 추적 (delta 누적 오차 방지)
    private float killerYaw = 0f;
    private bool killerYawReady = false;
    private KillerController lastKiller = null;

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerNetworkInput data = new PlayerNetworkInput();

        bool isCursorLocked = Cursor.lockState == CursorLockMode.Locked;

        // 커서 잠금 해제 → 잠금 전환 순간 감지 (UI 닫힘)
        // Unity가 커서를 화면 중앙으로 강제 이동시켜 Mouse X/Y에 스파이크 발생
        // → 0.2초간 look 입력 무시해서 NetYaw 점프 방지
        if (isCursorLocked && !wasCursorLocked)
            inputIgnoreUntil = Time.realtimeSinceStartup + 0.2f;
        wasCursorLocked = isCursorLocked;

        // 포커스 복귀 직후 or 커서 전환 직후: 빈 입력 전송
        if (Time.realtimeSinceStartup < inputIgnoreUntil)
        {
            input.Set(data);
            return;
        }

        // 커서 잠금 해제 상태(UI 열려있음)에서는 이동/look 입력 전송 안 함
        if (!isCursorLocked)
        {
            input.Set(data);
            return;
        }
        // 킬러 스폰/리스폰 감지 → Yaw 재초기화
        var curKiller = KillerController.LocalKiller;
        if (curKiller != lastKiller)
        {
            lastKiller = curKiller;
            killerYawReady = false;
        }

        // 절대 Yaw 초기화 (킬러 스폰 시 1회)
        if (!killerYawReady && curKiller != null)
        {
            killerYaw = curKiller.transform.eulerAngles.y;
            killerYawReady = true;
        }

        // 매 프레임 마우스로 절대 Yaw 누적
        if (killerYawReady)
        {
            float sens = curKiller?.mouseSensitivity ?? 2f;
            killerYaw += Input.GetAxisRaw("Mouse X") * sens;
            data.lookYaw = killerYaw;
        }

        // WASD 4방향 이동 (ActorController와 동일하게)
        float h = 0f, v = 0f;
        if (Input.GetKey(KeyCode.A)) h -= 1f;
        if (Input.GetKey(KeyCode.D)) h += 1f;
        if (Input.GetKey(KeyCode.W)) v += 1f;
        if (Input.GetKey(KeyCode.S)) v -= 1f;
        data.move = new Vector2(h, v).normalized;

        data.look = new Vector2(0f, Input.GetAxisRaw("Mouse Y"));

        NetworkButtons buttons = default;
        if (Input.GetKey(KeyCode.LeftShift)) buttons.Set(PlayerNetworkInput.WALK, true);
        if (Input.GetKey(KeyCode.LeftControl)) buttons.Set(PlayerNetworkInput.SIT, true);
        if (Input.GetKey(KeyCode.H)) buttons.Set(PlayerNetworkInput.HEAL, true);
        if (Input.GetKeyDown(KeyCode.V)) buttons.Set(PlayerNetworkInput.VAULT, true);
        // Fusion 네트워크 입력은 GetKey(지속)을 쓰는 게 안정적 - Tick과 Unity Frame이
        // 1:1이 아니어서 GetMouseButtonDown이 특정 Tick에서 누락되거나 중복될 수 있음.
        // 연속 공격 방지는 KillerController의 AttackCooldownTimer가 담당.
        if (Input.GetMouseButton(0)) buttons.Set(PlayerNetworkInput.ATTACK, true);

        data.buttons = buttons;

        // ─── 디버그 로그 (0.25초마다, 움직임 입력이 있을 때만) ─────
        if (data.move.sqrMagnitude > 0.001f && Time.time - lastInputLogTime > 0.25f)
        {
            Debug.Log(
                $"[InputSend] sentMove={data.move} " +
                $"rawHV=({Input.GetAxisRaw("Horizontal"):F2},{Input.GetAxisRaw("Vertical"):F2}) " +
                $"W={Input.GetKey(KeyCode.W)} A={Input.GetKey(KeyCode.A)} " +
                $"S={Input.GetKey(KeyCode.S)} D={Input.GetKey(KeyCode.D)}");
            lastInputLogTime = Time.time;
        }

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