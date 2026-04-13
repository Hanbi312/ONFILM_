using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

/// <summary>
/// 게임플레이 씬에서 캐릭터를 스폰한다.
///
/// [흐름]
///   1. 게임플레이 씬 진입
///   2. 클라이언트: NetworkPlayer 준비 대기 → RPC_SendSelection으로 선택 데이터 서버 전송
///   3. 서버: 모든 플레이어 선택 데이터 수신 확인
///   4. 서버: CharacterData.modelPrefab으로 스폰 (없으면 기본 프리팹으로 fallback)
/// </summary>
public class GameplayManager : MonoBehaviour
{
    [Header("기본 프리팹 (캐릭터 선택 없을 때 fallback)")]
    [SerializeField] private NetworkObject defaultActorPrefab;
    [SerializeField] private NetworkObject defaultVillainPrefab;

    [Header("스폰 포인트")]
    public Transform actorSpawnPoint; // 리스폰용 기본 스폰 포인트
    [SerializeField] private Transform villainSpawnPoint;
    [SerializeField] private Transform[] actorSpawnPoints;   // 연기자 랜덤 스폰 배열
    [SerializeField] private Transform[] villainSpawnPoints; // 악역 랜덤 스폰 배열

    [Header("스폰 튜닝")]
    [SerializeField] private float spawnLift = 0.5f;
    [SerializeField] private float spawnTimeout = 15f;

    private FusionLobbyManager lobbyManager;
    private NetworkRunner runner;

    private bool hasSpawnedAll = false;
    private readonly Dictionary<PlayerRef, NetworkObject> spawnedCharacters
        = new Dictionary<PlayerRef, NetworkObject>();

    private void Start()
    {
        Debug.Log("[GameplayManager] Start");

        lobbyManager = FusionLobbyManager.Instance;
        if (lobbyManager == null)
        {
            Debug.LogError("[GameplayManager] FusionLobbyManager 없음");
            return;
        }

        runner = lobbyManager.GetRunner();
        if (runner == null)
        {
            Debug.LogError("[GameplayManager] runner 없음");
            return;
        }

        // 선택 데이터 전송과 스폰을 코루틴으로 순서대로 처리
        StartCoroutine(SendSelectionAndSpawnRoutine());
    }

    /// <summary>
    /// 플레이어 퇴장 시 FusionLobbyManager → GameStateManager 경유 또는 직접 호출
    /// 퇴장한 플레이어의 캐릭터를 spawnedCharacters에서 제거 (despawn은 하지 않음)
    /// 캐릭터는 씬에 남겨두어 나머지 플레이어가 계속 게임 가능
    /// </summary>
    public void OnPlayerLeft(PlayerRef player)
    {
        if (spawnedCharacters.TryGetValue(player, out var characterObj))
        {
            if (characterObj != null)
            {
                // 연기자 캐릭터라면 죽은 상태로 처리 (방치된 캐릭터가 게임에 영향 없도록)
                var actorCtrl = characterObj.GetComponent<ActorController>();
                if (actorCtrl != null && !actorCtrl.IsDead && actorCtrl.HasStateAuthority)
                {
                    actorCtrl.IsDead = true;
                    Debug.Log($"[GameplayManager] 퇴장 플레이어 캐릭터를 사망 처리 | Player={player}");
                }
                // 악역이 나갔으면 그냥 두기 (씬에 남아있는 게 나음)
            }
            spawnedCharacters.Remove(player);
        }
        Debug.Log($"[GameplayManager] 플레이어 퇴장 처리 완료 | Player={player} | 남은 캐릭터={spawnedCharacters.Count}");
    }

    // ─── 선택 데이터 전송 + 스폰 전체 흐름 ────
    private IEnumerator SendSelectionAndSpawnRoutine()
    {
        // 1단계: NetworkPlayer 준비 대기 후 선택 데이터 전송
        yield return StartCoroutine(WaitAndSendSelectionRoutine());

        // 2단계: 서버만 스폰 처리
        if (runner.IsServer)
            yield return StartCoroutine(SpawnCharactersRoutine());
    }

    // NetworkPlayer가 준비될 때까지 기다렸다가 선택 데이터 RPC 전송
    private IEnumerator WaitAndSendSelectionRoutine()
    {
        float timer = 0f;

        while (timer < spawnTimeout)
        {
            var myPlayer = lobbyManager.GetLocalNetworkPlayer();

            if (myPlayer != null && myPlayer.CanReadNetworkState())
            {
                var session = CharacterSelectSession.Instance;
                string characterName = session?.SelectedCharacter?.characterName ?? "";
                string weaponName = session?.SelectedWeapon?.weaponName ?? "";

                myPlayer.RPC_SendSelection(characterName, weaponName);
                Debug.Log($"[GameplayManager] 선택 전송 | 캐릭터={characterName} | 무기={weaponName}");
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("[GameplayManager] NetworkPlayer 준비 타임아웃 - 선택 데이터 전송 실패");
    }

    private IEnumerator SpawnCharactersRoutine()
    {
        float timer = 0f;

        // 1단계: NetworkPlayer 목록 준비 대기
        while (timer < spawnTimeout)
        {
            List<NetworkPlayer> players = lobbyManager.GetAllNetworkPlayers();
            int activeCount = runner.ActivePlayers.Count();

            if (players != null && activeCount > 0 && players.Count >= activeCount)
            {
                Debug.Log($"[GameplayManager] NetworkPlayer 준비 완료 | {players.Count}/{activeCount}");
                break;
            }

            Debug.Log($"[GameplayManager] NetworkPlayer 대기 중... {(players == null ? 0 : players.Count)}/{activeCount}");
            timer += Time.deltaTime;
            yield return null;
        }

        // 2단계: 모든 플레이어 선택 데이터 수신 대기
        timer = 0f;
        while (timer < spawnTimeout)
        {
            List<NetworkPlayer> players = lobbyManager.GetAllNetworkPlayers();

            if (players != null && players.Count > 0 &&
                players.All(p => p != null && p.CanReadNetworkState() && p.HasSentSelection))
            {
                Debug.Log("[GameplayManager] 모든 선택 데이터 수신 완료");
                break;
            }

            // 몇 명이 수신됐는지 로그
            int received = players?.Count(p => p != null && p.HasSentSelection) ?? 0;
            int total = players?.Count ?? 0;
            Debug.Log($"[GameplayManager] 선택 데이터 대기 중... {received}/{total}");

            timer += Time.deltaTime;
            yield return null;
        }

        yield return null;
        yield return null;

        timer = 0f;

        // 3단계: 스폰 시도
        while (timer < spawnTimeout)
        {
            if (hasSpawnedAll) yield break;

            bool success = TrySpawnAllCharacters();
            if (success)
            {
                hasSpawnedAll = true;
                Debug.Log("[GameplayManager] 모든 캐릭터 스폰 완료");
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("[GameplayManager] 제한 시간 안에 스폰 실패");
    }

    private bool TrySpawnAllCharacters()
    {
        if (villainSpawnPoint == null)
        {
            Debug.LogError("[GameplayManager] 악역 스폰 포인트 연결 안 됨");
            return false;
        }

        // 연기자 스폰 포인트 준비
        List<Transform> availableActorSpawnPoints = new List<Transform>();
        if (actorSpawnPoints != null && actorSpawnPoints.Length > 0)
        {
            availableActorSpawnPoints.AddRange(actorSpawnPoints.Where(p => p != null));
            for (int i = 0; i < availableActorSpawnPoints.Count; i++)
            {
                int rand = Random.Range(i, availableActorSpawnPoints.Count);
                var temp = availableActorSpawnPoints[i];
                availableActorSpawnPoints[i] = availableActorSpawnPoints[rand];
                availableActorSpawnPoints[rand] = temp;
            }
        }
        else if (actorSpawnPoint != null)
            availableActorSpawnPoints.Add(actorSpawnPoint);

        // 악역 스폰 포인트 준비
        List<Transform> availableVillainSpawnPoints = new List<Transform>();
        if (villainSpawnPoints != null && villainSpawnPoints.Length > 0)
        {
            availableVillainSpawnPoints.AddRange(villainSpawnPoints.Where(p => p != null));
            for (int i = 0; i < availableVillainSpawnPoints.Count; i++)
            {
                int rand = Random.Range(i, availableVillainSpawnPoints.Count);
                var temp = availableVillainSpawnPoints[i];
                availableVillainSpawnPoints[i] = availableVillainSpawnPoints[rand];
                availableVillainSpawnPoints[rand] = temp;
            }
        }
        else if (villainSpawnPoint != null)
            availableVillainSpawnPoints.Add(villainSpawnPoint);

        int activePlayerCount = runner.ActivePlayers.Count();
        List<NetworkPlayer> players = lobbyManager.GetAllNetworkPlayers();

        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("[GameplayManager] NetworkPlayer 목록 없음");
            return false;
        }

        if (players.Count < activePlayerCount)
        {
            Debug.LogWarning($"[GameplayManager] 플레이어 준비 안 됨 | {players.Count}/{activePlayerCount}");
            return false;
        }

        int actorSpawnIndex = 0;
        int villainSpawnIndex = 0;

        foreach (NetworkPlayer player in players)
        {
            if (player == null || !player.CanReadNetworkState() || player.Object == null)
                return false;

            PlayerRef owner = player.Object.InputAuthority;
            if (owner == PlayerRef.None) return false;
            if (spawnedCharacters.ContainsKey(owner)) continue;

            MatchRole role = player.GetRole();
            Transform spawnPoint;

            if (role == MatchRole.Actor)
            {
                if (actorSpawnIndex < availableActorSpawnPoints.Count)
                    spawnPoint = availableActorSpawnPoints[actorSpawnIndex++];
                else
                    spawnPoint = availableActorSpawnPoints[0];
            }
            else
            {
                if (villainSpawnIndex < availableVillainSpawnPoints.Count)
                    spawnPoint = availableVillainSpawnPoints[villainSpawnIndex++];
                else
                    spawnPoint = availableVillainSpawnPoints[0];
            }

            // 선택한 캐릭터 프리팹 결정
            NetworkObject prefabToSpawn = GetPrefabForPlayer(player, role);

            if (prefabToSpawn == null)
            {
                Debug.LogError($"[GameplayManager] 스폰 프리팹 없음 | {player.Nickname} | Role={role}");
                return false;
            }

            Vector3 spawnPos = spawnPoint.position + Vector3.up * spawnLift;

            NetworkObject spawned = runner.Spawn(
                prefabToSpawn,
                spawnPos,
                spawnPoint.rotation,
                owner
            );

            if (spawned == null)
            {
                Debug.LogError($"[GameplayManager] 스폰 실패 | {player.Nickname}");
                return false;
            }

            runner.SetPlayerObject(owner, spawned);
            spawnedCharacters[owner] = spawned;

            // 악역이면 무기 장착
            if (role == MatchRole.Villain)
                EquipWeapon(spawned, player);

            Debug.Log($"[GameplayManager] 스폰 성공 | {player.Nickname} | Role={role} | Pos={spawned.transform.position}");
        }

        return spawnedCharacters.Count >= activePlayerCount;
    }

    // 선택한 캐릭터 이름으로 프리팹 결정
    // 선택 데이터 없으면 기본 프리팹으로 fallback
    private NetworkObject GetPrefabForPlayer(NetworkPlayer player, MatchRole role)
    {
        string selectedName = player.SelectedCharacterName.ToString();

        if (!string.IsNullOrEmpty(selectedName))
        {
            var session = CharacterSelectSession.Instance;
            if (session?.AllCharacters != null)
            {
                var data = session.AllCharacters.FirstOrDefault(
                    c => c.characterName == selectedName && c.role == role);

                if (data?.modelPrefab != null)
                {
                    var netObj = data.modelPrefab.GetComponent<NetworkObject>();
                    if (netObj != null)
                    {
                        Debug.Log($"[GameplayManager] 선택 프리팹 사용 | {player.Nickname} | {selectedName}");
                        return netObj;
                    }
                }
            }
        }

        // Fallback
        Debug.LogWarning($"[GameplayManager] 기본 프리팹 사용 (fallback) | {player.Nickname} | 선택={selectedName}");
        return role == MatchRole.Actor ? defaultActorPrefab : defaultVillainPrefab;
    }

    // 악역 캐릭터에 무기 장착
    private void EquipWeapon(NetworkObject character, NetworkPlayer player)
    {
        string weaponName = player.SelectedWeaponName.ToString();
        if (string.IsNullOrEmpty(weaponName)) return;

        var session = CharacterSelectSession.Instance;
        if (session?.AllWeapons == null) return;

        var weaponData = session.AllWeapons.FirstOrDefault(w => w.weaponName == weaponName);
        if (weaponData?.modelPrefab == null) return;

        var netObj = weaponData.modelPrefab.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[GameplayManager] 무기 프리팹에 NetworkObject 없음! | {weaponData.weaponName}");
            return;
        }

        // hand.R 본 탐색
        Transform handBone = FindBoneRecursive(character.transform, "hand.R");
        Vector3 spawnPos = handBone != null ? handBone.position : character.transform.position;
        Quaternion spawnRot = handBone != null ? handBone.rotation : character.transform.rotation;

        NetworkObject spawnedWeapon = runner.Spawn(netObj, spawnPos, spawnRot, character.InputAuthority);

        if (spawnedWeapon != null)
        {
            var follower = spawnedWeapon.GetComponent<WeaponFollower>();
            if (follower == null)
                follower = spawnedWeapon.gameObject.AddComponent<WeaponFollower>();

            follower.target = handBone;
            follower.ownerKiller = character.GetComponent<KillerController>(); // 소유자 킬러 연결

            var killerCtrl = character.GetComponent<KillerController>();
            if (killerCtrl != null)
            {
                killerCtrl.RPC_EquipWeapon(spawnedWeapon.Id);
                // 무기별 공격 트리거 설정
                if (!string.IsNullOrEmpty(weaponData.attackAnimTrigger))
                    killerCtrl.SetAttackTrigger(weaponData.attackAnimTrigger);
            }

            Debug.Log($"[GameplayManager] 무기 장착 | {player.Nickname} | {weaponData.weaponName} | 트리거={weaponData.attackAnimTrigger}");
        }
    }

    private Transform FindBoneRecursive(Transform parent, string boneName)
    {
        if (parent.name == boneName) return parent;
        foreach (Transform child in parent)
        {
            var found = FindBoneRecursive(child, boneName);
            if (found != null) return found;
        }
        return null;
    }
}