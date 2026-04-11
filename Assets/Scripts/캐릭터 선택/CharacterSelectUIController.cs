using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ═══════════════════════════════════════════════════════════════
// CharacterSelectUIController
// 캐릭터 선택 씬 전체를 하나의 스크립트로 관리
//
// [화면 전환 구조]
//   대기 화면 (WaitPanel)
//     └── "캐릭터" 버튼 클릭
//           ↓
//   선택 화면 (SelectPanel)
//     ├── 탭 버튼으로 그리드 내용 전환 (연기자 / 악역 / 무기)
//     ├── 카드 클릭 → 오른쪽 원에 이미지 표시
//     └── "선택하기" 클릭 → 대기 화면으로 복귀 + 결과 반영
//
// [씬 패널 구성]
//   Canvas
//   ├── WaitPanel          ← 대기 화면 (첫 번째 사진)
//   └── SelectPanel        ← 선택 화면 (두 번째/세 번째 사진)
//
// ═══════════════════════════════════════════════════════════════
public class CharacterSelectUIController : MonoBehaviour
{
    public static CharacterSelectUIController Instance;

    // ─────────────────────────────────────────
    // [대기 화면 - WaitPanel]
    // ─────────────────────────────────────────
    [Header("── 대기 화면 (WaitPanel) ──────────────")]
    [SerializeField] private GameObject waitPanel;

    // 왼쪽: 연기자 캐릭터 슬롯들
    // 각 슬롯은 가로로 긴 카드 형태 (사진 1번 기준)
    // 슬롯 안의 왼쪽 정사각형 Image에 선택한 초상화가 표시됨
    [SerializeField] private Image[] actorPortraitSlots;    // 연기자 초상화 표시 슬롯 배열

    // 왼쪽 상단: 캐릭터 선택 버튼 (클릭 시 SelectPanel로 전환)
    [SerializeField] private Button openSelectPanelButton;  // "캐릭터" 버튼

    // 오른쪽: 악역이 선택한 캐릭터 초상화 (큰 원 안)
    [SerializeField] private Image villainPortraitInWait;   // 악역 선택 결과 이미지

    // 하단 오른쪽: 타이머 + 준비 버튼
    [SerializeField] private TMP_Text timerText;            // "60초" 카운트다운
    [SerializeField] private float selectTimeLimit = 60f;
    [SerializeField] private Button readyButton;            // 준비하기 / 준비해제
    [SerializeField] private TMP_Text readyButtonText;


    [Header("── 경고 UI ────────────────────────────")]
    [SerializeField] private GameObject warningPanel;    // 경고 패널 (무기 미선택 시 표시)
    [SerializeField] private TMP_Text warningText;       // 경고 메시지 텍스트

    // ─────────────────────────────────────────
    // [선택 화면 - SelectPanel]
    // ─────────────────────────────────────────
    [Header("── 선택 화면 (SelectPanel) ────────────")]
    [SerializeField] private GameObject selectPanel;

    // 상단 탭 버튼 3개
    [SerializeField] private Button tabActorButton;         // "연기자" 탭
    [SerializeField] private Button tabVillainButton;       // "악역" 탭
    [SerializeField] private Button tabWeaponButton;        // "무기" 탭

    // 가운데 그리드 (탭에 따라 카드가 생성됨)
    [SerializeField] private Transform gridContent;         // 연기자/악역 탭용 Content (GridLayoutGroup)
    [SerializeField] private GameObject cardPrefab;         // 캐릭터 카드 프리팹

    // 오른쪽 미리보기 원
    [SerializeField] private Image previewImage;            // 카드 클릭 시 여기에 이미지 표시
    [SerializeField] private Sprite defaultPreviewSprite;   // 아무것도 선택 안 했을 때 기본 이미지

    [Header("── 무기 패널 (WeaponPanel) ────────────────")]
    [SerializeField] private GameObject weaponPanel;         // 무기 탭 클릭 시 오버레이로 뜨는 패널

    // 카테고리별 다이아몬드 버튼 컨테이너
    // 에디터에서 카테고리 UI를 직접 만들고 각 카테고리 아래 버튼이 들어갈 빈 오브젝트를 연결
    // WeaponCategory enum 순서와 배열 순서가 일치해야 함 (단검/장검/짧은둔기/긴둔기/특수소품)
    [SerializeField] private Transform[] categoryContainers; // 카테고리별 버튼 컨테이너 배열

    // 다이아몬드 버튼 프리팹 (무기 1개당 1개 생성)
    [SerializeField] private GameObject weaponDiamondPrefab; // 다이아몬드 모양 버튼 프리팹

    // 무기 정보 패널 (오른쪽, 무기 클릭 시 표시)
    [SerializeField] private GameObject weaponInfoPanel;        // 무기 정보 패널 (기본 비활성)
    [SerializeField] private Image weaponInfoIcon;              // 무기 프로필 이미지 (다이아몬드 형태)
    [SerializeField] private TMP_Text weaponNameText;           // 무기 이름
    [SerializeField] private TMP_Text weaponRangeText;          // 사거리
    [SerializeField] private TMP_Text weaponCastDelayText;      // 선 딜레이
    [SerializeField] private TMP_Text weaponSuccessDelayText;   // 성공 딜레이
    [SerializeField] private TMP_Text weaponFailDelayText;      // 실패 딜레이

    // 선택하기 버튼
    [SerializeField] private Button backButton;             // 뒤로가기 버튼 (선택 화면 → 대기 화면)
    [SerializeField] private Button selectButton;           // 카드 선택 후 활성화
    [SerializeField] private TMP_Text selectButtonText;    // 선택하기 버튼 텍스트


    // ─────────────────────────────────────────
    // [데이터]
    // ─────────────────────────────────────────
    [Header("── 데이터 (ScriptableObject 연결) ──────")]
    [SerializeField] private List<CharacterData> allCharacters; // 모든 캐릭터 데이터
    [SerializeField] private List<WeaponData> allWeapons;       // 모든 무기 데이터


    // ─────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────
    private enum Tab { Actor, Villain, Weapon }
    private Tab currentTab = Tab.Actor;

    // 선택 화면에서 클릭 중인 항목 (선택하기 전까지는 pending)
    private CharacterData pendingCharacter = null;
    private WeaponData pendingWeapon = null;

    private FusionLobbyManager lobbyManager;
    private NetworkRunner runner;
    private NetworkPlayer myNetworkPlayer;
    private bool isFindingMyPlayer = false;

    private float remainTime;
    private bool timerRunning = false;
    private bool isStartingGameplay = false; // 씬 전환 중복 방지
    private MatchRole myRole = MatchRole.None;


    // ═══════════════════════════════════════════════════════════
    // 초기화
    // ═══════════════════════════════════════════════════════════
    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        lobbyManager = FusionLobbyManager.Instance;
        runner = lobbyManager?.GetRunner();
        myRole = lobbyManager != null ? lobbyManager.GetSelectedRole() : MatchRole.None;

        // 버튼 이벤트 등록
        openSelectPanelButton.onClick.AddListener(OpenSelectPanel);
        if (backButton != null) backButton.onClick.AddListener(OnClickBack);
        tabActorButton.onClick.AddListener(() => SwitchTab(Tab.Actor));
        tabVillainButton.onClick.AddListener(() => SwitchTab(Tab.Villain));
        tabWeaponButton.onClick.AddListener(() => SwitchTab(Tab.Weapon));
        selectButton.onClick.AddListener(OnClickSelect);
        readyButton.onClick.AddListener(OnClickReady);

        // 초기 상태
        readyButton.interactable = false;
        selectButton.interactable = false;

        // 대기 화면으로 시작
        ShowWaitPanel();

        // 타이머 시작
        remainTime = selectTimeLimit;
        timerRunning = true;

        StartCoroutine(FindMyNetworkPlayerRoutine());
    }

    private void Update()
    {
        // 타이머 업데이트
        if (timerRunning)
        {
            remainTime -= Time.deltaTime;
            if (remainTime <= 0f)
            {
                remainTime = 0f;
                timerRunning = false;
                AutoReady();
            }
            if (timerText != null)
                timerText.text = $"{Mathf.CeilToInt(remainTime)}초";
        }

        // NetworkPlayer 상태 감시
        if (myNetworkPlayer != null && !myNetworkPlayer.CanReadNetworkState())
            myNetworkPlayer = null;
        if (myNetworkPlayer == null && !isFindingMyPlayer)
            StartCoroutine(FindMyNetworkPlayerRoutine());

        // myRole이 아직 None이면 재시도 (씬 전환 직후 타이밍 문제 방지)
        if (myRole == MatchRole.None && lobbyManager != null)
            myRole = lobbyManager.GetSelectedRole();

        // 준비 버튼 상태 갱신
        RefreshReadyButton();

        // 대기 화면일 때 초상화 실시간 갱신 (상대방 선택 반영)
        if (waitPanel != null && waitPanel.activeSelf)
            RefreshWaitPanel();

        // 서버에서 전원 준비 확인 → 게임플레이 씬 이동
        if (runner != null && runner.IsServer)
            TryStartGameplay();
    }


    // ═══════════════════════════════════════════════════════════
    // 패널 전환
    // ═══════════════════════════════════════════════════════════

    // 대기 화면 표시
    private void ShowWaitPanel()
    {
        waitPanel.SetActive(true);
        selectPanel.SetActive(false);
        RefreshWaitPanel();
    }

    // 선택 화면 열기 (캐릭터 버튼 클릭)
    public void OpenSelectPanel()
    {
        waitPanel.SetActive(false);
        selectPanel.SetActive(true);

        if (weaponInfoPanel != null)
            weaponInfoPanel.SetActive(false);

        ClearPreview();
        selectButton.interactable = false;
        pendingCharacter = null;
        pendingWeapon = null;

        // 내 진영에 맞는 탭으로 시작
        SwitchTab(myRole == MatchRole.Villain ? Tab.Villain : Tab.Actor);
    }


    // ═══════════════════════════════════════════════════════════
    // 선택 화면 - 탭 전환
    // ═══════════════════════════════════════════════════════════
    private void SwitchTab(Tab tab)
    {
        currentTab = tab;
        ClearGrid();
        pendingCharacter = null;
        selectButton.interactable = false;
        if (selectButtonText != null) selectButtonText.text = "선택하기";

        ClearPreview(); // 3D 모델 초기화

        if (weaponInfoPanel != null)
            weaponInfoPanel.SetActive(false);

        if (tab == Tab.Weapon)
        {
            // 무기 탭: SelectPanel은 그대로 두고 WeaponPanel을 위에 오버레이로 표시
            if (weaponPanel != null)
            {
                weaponPanel.SetActive(true);
                PopulateWeaponPanel();
            }
        }
        else
        {
            // 연기자/악역 탭: WeaponPanel 닫기
            if (weaponPanel != null) weaponPanel.SetActive(false);
            if (gridContent != null) gridContent.gameObject.SetActive(true);

            switch (tab)
            {
                case Tab.Actor: PopulateCharacters(MatchRole.Actor); break;
                case Tab.Villain: PopulateCharacters(MatchRole.Villain); break;
            }
        }
    }

    // 무기 패널 뒤로가기 버튼 - WeaponPanel만 닫기 (SelectPanel은 그대로 유지)
    public void OnClickWeaponPanelBack()
    {
        if (weaponPanel != null) weaponPanel.SetActive(false);
        pendingWeapon = null;
        currentTab = myRole == MatchRole.Villain ? Tab.Villain : Tab.Actor;
    }

    // 캐릭터 카드 생성
    private void PopulateCharacters(MatchRole role)
    {
        foreach (var data in allCharacters)
        {
            if (data.role != role) continue;

            // 내 진영과 같으면 선택 가능, 다른 진영은 보기만 가능
            bool canSelect = (role == myRole);
            CharacterData captured = data;

            CreateCard(
                portrait: data.portrait,
                itemName: data.characterName,
                canSelect: canSelect,
                onClickAction: () =>
                {
                    // 클릭 시 오른쪽에 캐릭터 이미지 표시
                    ShowPreview(captured.previewImage != null ? captured.previewImage : captured.portrait);

                    if (canSelect)
                    {
                        pendingCharacter = captured;
                        pendingWeapon = null;
                        selectButton.interactable = true;
                        if (selectButtonText != null) selectButtonText.text = "선택하기";

                        if (weaponInfoPanel != null)
                            weaponInfoPanel.SetActive(false);
                    }
                }
            );
        }
    }

    // 무기 카드 생성
    // 무기 패널에 카테고리별 다이아몬드 버튼 자동 생성
    // categoryContainers 배열의 순서 = WeaponCategory enum 순서
    // (단검=0, 장검=1, 짧은둔기=2, 긴둔기=3, 특수소품=4)
    private void PopulateWeaponPanel()
    {
        bool canSelect = (myRole == MatchRole.Villain);

        // 기존 버튼 초기화
        if (categoryContainers != null)
            foreach (var container in categoryContainers)
                if (container != null)
                    foreach (Transform child in container)
                        Destroy(child.gameObject);

        var categories = System.Enum.GetValues(typeof(WeaponCategory));

        for (int i = 0; i < categories.Length; i++)
        {
            WeaponCategory category = (WeaponCategory)categories.GetValue(i);

            // 이 카테고리 컨테이너가 없으면 스킵
            if (categoryContainers == null || i >= categoryContainers.Length
                || categoryContainers[i] == null) continue;

            Transform container = categoryContainers[i];
            var weaponsInCategory = allWeapons.Where(w => w.category == category).ToList();

            // 카테고리에 해당하는 무기마다 다이아몬드 버튼 생성
            foreach (var data in weaponsInCategory)
            {
                WeaponData captured = data;
                GameObject diamond = Instantiate(
                    weaponDiamondPrefab != null ? weaponDiamondPrefab : cardPrefab,
                    container);

                // 다이아몬드 안 아이콘 설정
                var img = diamond.GetComponentInChildren<Image>();
                if (img != null && data.icon != null)
                    img.sprite = data.icon;

                var btn = diamond.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() =>
                    {
                        ShowWeaponInfo(captured);

                        if (canSelect)
                        {
                            pendingWeapon = captured;
                            selectButton.interactable = true;
                            if (selectButtonText != null) selectButtonText.text = "선택하기";
                        }
                    });

                    // 연기자는 선택 불가 - 시각적으로 어둡게
                    if (!canSelect)
                    {
                        var colors = btn.colors;
                        colors.normalColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                        btn.colors = colors;
                    }
                }
            }
        }
    }

    // 카드 1개 생성
    private void CreateCard(Sprite portrait, string itemName,
        bool canSelect, System.Action onClickAction)
    {
        GameObject card = Instantiate(cardPrefab, gridContent);

        var img = card.GetComponentInChildren<Image>();
        if (img != null && portrait != null)
            img.sprite = portrait;

        var label = card.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = itemName;

        var btn = card.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => onClickAction?.Invoke());

            // 선택 불가 진영 카드는 시각적으로 어둡게 표시
            if (!canSelect)
            {
                var colors = btn.colors;
                colors.normalColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                colors.highlightedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
                btn.colors = colors;
            }
        }
    }

    // 오른쪽 미리보기에 3D 모델 표시
    // 오른쪽 미리보기 원에 이미지 표시
    private void ShowPreview(Sprite sprite)
    {
        if (previewImage != null)
            previewImage.sprite = sprite;
    }

    private void ClearPreview()
    {
        if (previewImage != null)
            previewImage.sprite = defaultPreviewSprite;
    }

    // 무기 다이아몬드 버튼 클릭 시 호출 - 에디터 버튼 OnClick에 연결
    // weaponName은 WeaponData의 weaponName과 정확히 일치해야 함
    public void OnClickWeaponButton(string weaponName)
    {
        var data = allWeapons.Find(w => w.weaponName == weaponName);
        if (data == null)
        {
            Debug.LogWarning($"[CharacterSelectUI] 무기를 찾을 수 없음: {weaponName}");
            return;
        }

        ShowWeaponInfo(data);

        // 악역만 선택 가능
        if (myRole == MatchRole.Villain)
        {
            pendingWeapon = data;
            selectButton.interactable = true;
            if (selectButtonText != null) selectButtonText.text = "선택하기";
        }
    }

    // 무기 스탯 패널 표시
    private void ShowWeaponInfo(WeaponData data)
    {
        if (weaponInfoPanel == null) return;
        weaponInfoPanel.SetActive(true);

        // 무기 이름
        if (weaponNameText != null) weaponNameText.text = data.weaponName;
        // 무기 아이콘 - 다이아몬드 형태로 표시 (weaponInfoIcon은 45도 회전된 Image)
        if (weaponInfoIcon != null && data.icon != null) weaponInfoIcon.sprite = data.icon;
        // 스탯
        if (weaponRangeText != null) weaponRangeText.text = $"{data.range}m";
        if (weaponCastDelayText != null) weaponCastDelayText.text = $"{data.castDelay}s";
        if (weaponSuccessDelayText != null) weaponSuccessDelayText.text = $"{data.successDelay}s";
        if (weaponFailDelayText != null) weaponFailDelayText.text = $"{data.failDelay}s";
    }

    // 그리드 초기화
    private void ClearGrid()
    {
        foreach (Transform child in gridContent)
            Destroy(child.gameObject);


    }


    // ═══════════════════════════════════════════════════════════
    // 선택하기 버튼
    // ═══════════════════════════════════════════════════════════
    private void OnClickSelect()
    {
        if (currentTab == Tab.Weapon && pendingWeapon != null)
        {
            CharacterSelectSession.Instance?.SetWeapon(pendingWeapon);
            Debug.Log($"[CharacterSelectUI] 무기 확정: {pendingWeapon.weaponName}");

            if (selectButtonText != null)
                selectButtonText.text = "선택완료";
        }
        else if (pendingCharacter != null)
        {
            CharacterSelectSession.Instance?.SetCharacter(pendingCharacter);
            Debug.Log($"[CharacterSelectUI] 캐릭터 확정: {pendingCharacter.characterName}");

            if (selectButtonText != null)
                selectButtonText.text = "선택완료";
        }

        // 선택 결과를 즉시 서버에 전송 → 상대방 화면에 초상화 표시
        SendSelectionRPC();
    }

    // 뒤로가기 버튼 - 선택 화면 → 대기 화면으로 복귀
    public void OnClickBack()
    {
        ShowWaitPanel();
    }

    // 현재 선택된 캐릭터/무기를 NetworkPlayer RPC로 서버에 전송
    // 서버가 Networked 변수에 저장 → 모든 클라이언트에 동기화
    private void SendSelectionRPC()
    {
        if (myNetworkPlayer == null || !myNetworkPlayer.CanReadNetworkState()) return;

        var session = CharacterSelectSession.Instance;
        if (session == null) return;

        string characterName = session.SelectedCharacter?.characterName ?? "";
        string weaponName = session.SelectedWeapon?.weaponName ?? "";

        myNetworkPlayer.RPC_SendSelection(characterName, weaponName);
        Debug.Log($"[CharacterSelectUI] RPC 전송 | 캐릭터={characterName} | 무기={weaponName}");
    }


    // ═══════════════════════════════════════════════════════════
    // 대기 화면 갱신
    // ═══════════════════════════════════════════════════════════
    private void RefreshWaitPanel()
    {
        var session = CharacterSelectSession.Instance;
        if (session == null || lobbyManager == null) return;

        var allPlayers = lobbyManager.GetAllNetworkPlayers();
        if (allPlayers == null) return;

        int actorSlotIndex = 0;

        foreach (var player in allPlayers)
        {
            if (player == null || !player.CanReadNetworkState()) continue;

            MatchRole role = player.GetRole();

            // NetworkPlayer에 저장된 이름으로 CharacterData 검색
            string selectedName = player.SelectedCharacterName.ToString();
            CharacterData data = null;

            if (!string.IsNullOrEmpty(selectedName) && session.AllCharacters != null)
                data = session.AllCharacters.Find(c => c.characterName == selectedName);

            // 로컬 플레이어는 Session에서 직접 읽기 (RPC 전송 전에도 즉시 반영)
            if (player == myNetworkPlayer)
                data = session.SelectedCharacter ?? data;

            if (role == MatchRole.Actor)
            {
                if (data != null && actorPortraitSlots != null && actorSlotIndex < actorPortraitSlots.Length)
                    actorPortraitSlots[actorSlotIndex].sprite = data.portrait;
                actorSlotIndex++;
            }

            if (role == MatchRole.Villain)
            {
                if (data != null && villainPortraitInWait != null)
                    villainPortraitInWait.sprite = data.portrait;
            }
        }
    }


    // ═══════════════════════════════════════════════════════════
    // 준비 버튼
    // ═══════════════════════════════════════════════════════════

    // 준비하기 버튼 활성화 조건
    // 연기자: 캐릭터 선택 완료
    // 악역:   캐릭터 + 무기 둘 다 선택 완료
    private bool IsSelectionComplete()
    {
        var session = CharacterSelectSession.Instance;
        if (session == null) return false;

        if (myRole == MatchRole.Actor)
            return session.SelectedCharacter != null;

        if (myRole == MatchRole.Villain)
            return session.SelectedCharacter != null && session.SelectedWeapon != null;

        return false;
    }

    private void RefreshReadyButton()
    {
        if (readyButton == null) return;

        bool networkReady = myNetworkPlayer != null && myNetworkPlayer.CanReadNetworkState();
        bool selectionDone = IsSelectionComplete();

        readyButton.interactable = networkReady && selectionDone;

        if (readyButtonText != null && networkReady)
            readyButtonText.text = myNetworkPlayer.GetSafeReady() ? "준비해제" : "준비하기";
    }

    public void OnClickReady()
    {
        if (myNetworkPlayer == null || !myNetworkPlayer.CanReadNetworkState()) return;

        // 악역인데 무기 미선택 시 경고
        if (myRole == MatchRole.Villain)
        {
            var session = CharacterSelectSession.Instance;
            if (session?.SelectedWeapon == null)
            {
                ShowWarning("무기를 선택해주세요!");
                return;
            }
            if (session?.SelectedCharacter == null)
            {
                ShowWarning("캐릭터를 선택해주세요!");
                return;
            }
        }
        else if (myRole == MatchRole.Actor)
        {
            var session = CharacterSelectSession.Instance;
            if (session?.SelectedCharacter == null)
            {
                ShowWarning("캐릭터를 선택해주세요!");
                return;
            }
        }

        HideWarning();

        bool current = myNetworkPlayer.GetSafeReady();
        bool next = !current;
        myNetworkPlayer.RPC_SetReady(next);
        Debug.Log($"[CharacterSelectUI] OnClickReady | {myNetworkPlayer.Nickname} | {current} -> {next}");
    }

    private void ShowWarning(string message)
    {
        if (warningPanel != null) warningPanel.SetActive(true);
        if (warningText != null) warningText.text = message;
        // 3초 후 자동으로 숨김
        CancelInvoke(nameof(HideWarning));
        Invoke(nameof(HideWarning), 3f);
    }

    private void HideWarning()
    {
        if (warningPanel != null) warningPanel.SetActive(false);
    }

    // 시간 초과 시 강제 준비
    private void AutoReady()
    {
        if (myNetworkPlayer == null || !myNetworkPlayer.CanReadNetworkState()) return;
        if (!myNetworkPlayer.GetSafeReady())
            myNetworkPlayer.RPC_SetReady(true);
    }


    // ═══════════════════════════════════════════════════════════
    // NetworkPlayer 탐색
    // ═══════════════════════════════════════════════════════════
    private IEnumerator FindMyNetworkPlayerRoutine()
    {
        isFindingMyPlayer = true;
        float timer = 0f;

        while (timer < 5f)
        {
            myNetworkPlayer = lobbyManager?.GetLocalNetworkPlayer();

            if (myNetworkPlayer != null && myNetworkPlayer.CanReadNetworkState())
            {
                isFindingMyPlayer = false;
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("[CharacterSelectUI] 5초 안에 NetworkPlayer를 못 찾음");
        isFindingMyPlayer = false;
    }


    // ═══════════════════════════════════════════════════════════
    // 게임플레이 씬 전환 (서버만 호출)
    // ═══════════════════════════════════════════════════════════
    private void TryStartGameplay()
    {
        if (lobbyManager == null || runner == null) return;
        if (isStartingGameplay) return; // 중복 호출 방지

        var allPlayers = lobbyManager.GetAllNetworkPlayers();
        if (allPlayers == null || allPlayers.Count == 0) return;

        var valid = allPlayers.Where(p => p != null && p.CanReadNetworkState()).ToList();

        // 전체 활성 플레이어 수와 일치할 때만 체크 (일부 플레이어가 아직 로드 중이면 스킵)
        int activeCount = runner.ActivePlayers.Count();
        if (valid.Count < activeCount) return;

        if (valid.All(p => p.GetSafeReady()))
        {
            isStartingGameplay = true;
            Debug.Log("[CharacterSelectUI] 전원 준비 완료 → GameplayScene 이동");
            lobbyManager.TryLoadGameplayScene();
        }
    }
}