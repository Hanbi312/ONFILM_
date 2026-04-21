using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerHUDController - 미리 만들어 둔 UI 슬롯에 데이터만 채워 넣는 방식
///
/// ── Unity Inspector 세팅 ──────────────────────────────────
///
/// [본인 HUD]
///   My Portrait      → 본인 초상화 Image
///   My Health Bar    → 본인 체력 Slider
///   My Border Image  → 본인 테두리 Image (비극 연기 중 빨간색)
///
/// [팀원 HUD 슬롯] - 씬에 미리 만든 슬롯을 인덱스 순서대로 연결
///   Teammate Portraits[0]   → 팀원1 초상화 Image
///   Teammate Portraits[1]   → 팀원2 초상화 Image
///   Teammate Health Bars[0] → 팀원1 체력 Slider
///   Teammate Health Bars[1] → 팀원2 체력 Slider
///   Teammate Borders[0]     → 팀원1 테두리 Image
///   Teammate Borders[1]     → 팀원2 테두리 Image
///   Teammate Roots[0]       → 팀원1 슬롯 루트 GameObject (표시/숨김 전체 제어)
///   Teammate Roots[1]       → 팀원2 슬롯 루트 GameObject
///
///   ※ 같은 인덱스끼리 한 팀원 세트입니다.
///      [0] = 팀원1, [1] = 팀원2, ...
/// ─────────────────────────────────────────────────────────
/// </summary>
public class PlayerHUDController : MonoBehaviour
{
    // ─── 본인 HUD ─────────────────────────────────────────
    [Header("본인 HUD")]
    [SerializeField] private Image  myPortrait;
    [SerializeField] private Slider myHealthBar;
    [SerializeField] private Image  myBorderImage;

    // ─── 팀원 HUD 슬롯 (씬에 미리 만든 것) ───────────────
    [Header("팀원 HUD 슬롯 (인덱스 순서 맞게 연결)")]
    [SerializeField] private Image[]      teammatePortraits;
    [SerializeField] private Slider[]     teammateHealthBars;
    [SerializeField] private Image[]      teammateBorders;
    [SerializeField] private GameObject[] teammateRoots;      // 슬롯 전체 표시/숨김용

    // ─── 내부 상태 ────────────────────────────────────────
    private ActorController        localActor;
    private CharacterSelectSession session;

    // 팀원 액터를 슬롯 인덱스 순서로 저장
    private readonly List<ActorController> assignedTeammates = new();
    // 초상화 세팅 완료 여부 (NetworkPlayer 동기화 타이밍 때문에 재시도 필요)
    private readonly List<bool> portraitDone = new();

    // ─── 생명주기 ─────────────────────────────────────────
    private void Start()
    {
        session = CharacterSelectSession.Instance;
        HideAllTeammateSlots();
    }

    private void Update()
    {
        if (localActor == null)
        {
            TryFindLocalActor();
            return;
        }

        UpdateMyHUD();
        RefreshTeammateList();
        UpdateTeammateHUDs();
    }

    // ─── 시작 시 팀원 슬롯 전부 숨기기 ───────────────────
    private void HideAllTeammateSlots()
    {
        if (teammateRoots == null) return;
        foreach (var root in teammateRoots)
            if (root != null) root.SetActive(false);
    }

    // ─── 본인 액터 탐색 (최초 1회) ───────────────────────
    private void TryFindLocalActor()
    {
        foreach (var actor in FindObjectsByType<ActorController>(FindObjectsSortMode.None))
        {
            if (!actor.HasInputAuthority) continue;

            localActor = actor;

            if (myPortrait != null && session?.SelectedCharacter?.portrait != null)
                myPortrait.sprite = session.SelectedCharacter.portrait;

            break;
        }
    }

    // ─── 본인 HUD 업데이트 ────────────────────────────────
    private void UpdateMyHUD()
    {
        if (myHealthBar != null && localActor.defaultHealth > 0f)
            myHealthBar.value = localActor.Health / localActor.defaultHealth;

        if (myBorderImage != null)
            myBorderImage.color = localActor.IsCarried ? Color.red : Color.clear;
    }

    // ─── 팀원 목록 갱신 ───────────────────────────────────
    private void RefreshTeammateList()
    {
        foreach (var actor in FindObjectsByType<ActorController>(FindObjectsSortMode.None))
        {
            if (actor.HasInputAuthority) continue;           // 본인 제외
            if (assignedTeammates.Contains(actor)) continue; // 이미 등록된 팀원 제외

            int idx = assignedTeammates.Count;
            if (idx >= SlotCount()) continue;                // 슬롯 수 초과 무시

            assignedTeammates.Add(actor);
            portraitDone.Add(false);

            // 해당 슬롯 활성화
            if (teammateRoots != null && idx < teammateRoots.Length && teammateRoots[idx] != null)
                teammateRoots[idx].SetActive(true);
        }
    }

    // ─── 팀원 HUD 업데이트 ────────────────────────────────
    private void UpdateTeammateHUDs()
    {
        for (int i = 0; i < assignedTeammates.Count; i++)
        {
            var actor = assignedTeammates[i];
            if (actor == null) continue;

            // 초상화 — 세팅 안 됐으면 매 프레임 재시도
            if (!portraitDone[i])
                TrySetTeammatePortrait(i, actor);

            // 체력바
            if (teammateHealthBars != null && i < teammateHealthBars.Length)
                if (teammateHealthBars[i] != null && actor.defaultHealth > 0f)
                    teammateHealthBars[i].value = actor.Health / actor.defaultHealth;

            // 비극 연기 중 → 빨간 테두리
            if (teammateBorders != null && i < teammateBorders.Length)
                if (teammateBorders[i] != null)
                    teammateBorders[i].color = actor.IsCarried ? Color.red : Color.clear;
        }
    }

    // ─── 팀원 초상화 세팅 ─────────────────────────────────
    // ActorController.Object.InputAuthority 와 NetworkPlayer.Object.InputAuthority 를
    // 1:1 대조해서 정확한 팀원의 캐릭터 이름을 가져옴.
    private void TrySetTeammatePortrait(int idx, ActorController actor)
    {
        if (session?.AllCharacters == null) return;
        if (teammatePortraits == null || idx >= teammatePortraits.Length) return;
        if (teammatePortraits[idx] == null) return;
        if (actor.Object == null || !actor.Object.IsValid) return;

        PlayerRef actorOwner = actor.Object.InputAuthority;

        foreach (var np in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
        {
            if (!np.CanReadNetworkState()) continue;
            if (np.Object == null || !np.Object.IsValid) continue;
            if (np.Object.InputAuthority != actorOwner) continue;

            string characterName = np.SelectedCharacterName.ToString();
            if (string.IsNullOrEmpty(characterName)) return; // 아직 동기화 안 됨 → 다음 프레임 재시도

            var charData = session.AllCharacters.Find(c => c.characterName == characterName);
            if (charData?.portrait != null)
            {
                teammatePortraits[idx].sprite = charData.portrait;
                portraitDone[idx] = true;
            }
            return;
        }
    }

    // ─── 유틸 ─────────────────────────────────────────────
    // 연결된 배열 중 가장 짧은 길이를 슬롯 수로 사용
    private int SlotCount()
    {
        int count = int.MaxValue;
        if (teammatePortraits  != null) count = Mathf.Min(count, teammatePortraits.Length);
        if (teammateHealthBars != null) count = Mathf.Min(count, teammateHealthBars.Length);
        if (teammateBorders    != null) count = Mathf.Min(count, teammateBorders.Length);
        if (teammateRoots      != null) count = Mathf.Min(count, teammateRoots.Length);
        return count == int.MaxValue ? 0 : count;
    }
}