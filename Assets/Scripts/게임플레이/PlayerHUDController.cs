using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerHUDController - 연기자 인게임 HUD
/// 왼쪽 하단에 본인 + 팀원 캐릭터 정보 표시
/// </summary>
public class PlayerHUDController : MonoBehaviour
{
    [Header("본인 캐릭터 HUD (큰 것)")]
    [SerializeField] private Image myPortrait;          // 본인 프로필 이미지
    [SerializeField] private Slider myHealthBar;        // 본인 체력바
    [SerializeField] private Image myBorderImage;       // 본인 테두리 (비극 연기 중 빨간색)

    [Header("팀원 HUD 프리팹")]
    [SerializeField] private GameObject teammatePrefab; // 팀원 HUD 프리팹
    [SerializeField] private Transform teammateContainer; // 팀원 HUD 컨테이너

    // 팀원 HUD 프리팹 구성 요소
    // 프리팹 안에 있어야 할 것:
    // - Image (Portrait)
    // - Slider (HealthBar)
    // - Image (Border) - 빨간 테두리용

    private ActorController localActor;
    private List<ActorController> teammates = new List<ActorController>();
    private List<TeammateHUDEntry> teammateEntries = new List<TeammateHUDEntry>();

    private CharacterSelectSession session;

    private class TeammateHUDEntry
    {
        public ActorController actor;
        public Image portrait;
        public Slider healthBar;
        public Image border;
        public GameObject root;
    }

    private void Start()
    {
        session = CharacterSelectSession.Instance;
    }

    private void Update()
    {
        // 로컬 연기자 찾기
        if (localActor == null)
        {
            FindLocalActor();
            return;
        }

        // 본인 HUD 업데이트
        UpdateMyHUD();

        // 팀원 목록 갱신
        RefreshTeammates();

        // 팀원 HUD 업데이트
        UpdateTeammateHUDs();
    }

    private void FindLocalActor()
    {
        var actors = FindObjectsByType<ActorController>(FindObjectsSortMode.None);
        foreach (var actor in actors)
        {
            if (actor.HasInputAuthority)
            {
                localActor = actor;

                // 본인 프로필 이미지 설정
                if (session?.SelectedCharacter?.portrait != null && myPortrait != null)
                    myPortrait.sprite = session.SelectedCharacter.portrait;

                break;
            }
        }
    }

    private void UpdateMyHUD()
    {
        if (localActor == null) return;

        // 체력바 업데이트
        if (myHealthBar != null)
            myHealthBar.value = localActor.Health / localActor.defaultHealth;

        // 비극 연기 중(IsCarried) 빨간 테두리
        if (myBorderImage != null)
            myBorderImage.color = localActor.IsCarried ? Color.red : Color.clear;
    }

    private void RefreshTeammates()
    {
        var allActors = FindObjectsByType<ActorController>(FindObjectsSortMode.None);
        Debug.Log($"[PlayerHUD] 전체 ActorController 수: {allActors.Length}");

        foreach (var actor in allActors)
        {
            // 본인 제외
            if (actor.HasInputAuthority)
            {
                Debug.Log($"[PlayerHUD] 본인 스킵: {actor.name}");
                continue;
            }

            Debug.Log($"[PlayerHUD] 팀원 발견: {actor.name}");

            // 이미 등록된 팀원이면 스킵
            bool alreadyAdded = false;
            foreach (var entry in teammateEntries)
            {
                if (entry.actor == actor) { alreadyAdded = true; break; }
            }
            if (alreadyAdded) continue;

            // 새 팀원 HUD 생성
            AddTeammateHUD(actor);
        }
    }

    private void AddTeammateHUD(ActorController actor)
    {
        if (teammatePrefab == null || teammateContainer == null) return;

        GameObject obj = Instantiate(teammatePrefab, teammateContainer);

        var entry = new TeammateHUDEntry();
        entry.actor = actor;
        entry.root = obj;
        entry.portrait = obj.transform.Find("Portrait")?.GetComponent<Image>();
        entry.healthBar = obj.transform.Find("HealthBar")?.GetComponent<Slider>();
        entry.border = obj.transform.Find("Border")?.GetComponent<Image>();

        // 프로필 이미지 설정 - NetworkPlayer에서 캐릭터 이름 읽기
        SetTeammatePortrait(entry, actor);

        teammateEntries.Add(entry);
    }

    private void SetTeammatePortrait(TeammateHUDEntry entry, ActorController actor)
    {
        if (session?.AllCharacters == null) return;
        if (entry.portrait == null) return;

        // NetworkPlayer에서 선택한 캐릭터 이름 가져오기
        var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            if (!player.CanReadNetworkState()) continue;
            if (player.GetRole() != MatchRole.Actor) continue;

            string characterName = player.SelectedCharacterName.ToString();
            if (string.IsNullOrEmpty(characterName)) continue;

            var charData = session.AllCharacters.Find(c => c.characterName == characterName);
            if (charData?.portrait != null)
            {
                entry.portrait.sprite = charData.portrait;
                break;
            }
        }
    }

    private void UpdateTeammateHUDs()
    {
        foreach (var entry in teammateEntries)
        {
            if (entry.actor == null || entry.root == null) continue;

            // 체력바
            if (entry.healthBar != null)
                entry.healthBar.value = entry.actor.Health / entry.actor.defaultHealth;

            // 빨간 테두리 (비극 연기 중)
            if (entry.border != null)
                entry.border.color = entry.actor.IsCarried ? Color.red : Color.clear;
        }
    }
}
