using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyUIController : MonoBehaviour
{
    [Header("Top UI")]
    [SerializeField] private TMP_Text nicknameText;

    [Header("Role Buttons")]
    [SerializeField] private Button actorButton;
    [SerializeField] private Button villainButton;
    [SerializeField] private Button findMatchButton;
    [SerializeField] private Button backButton;

    [Header("Match Popup")]
    [SerializeField] private GameObject matchPopupPanel;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Button cancelMatchButton;

    [Header("Loading")]
    [SerializeField] private RectTransform loadingImage;
    [SerializeField] private float rotateSpeed = 200f;

    private MatchRole currentRole = MatchRole.None;

    private void Start()
    {
        SetupInitialUI();
        RefreshNickname();

        if (FusionLobbyManager.Instance != null)
        {
            FusionLobbyManager.Instance.OnStatusChanged += HandleStatusChanged;
            FusionLobbyManager.Instance.OnPlayerCountChanged += HandlePlayerCountChanged;
        }
    }

    private void Update()
    {
        if (loadingImage != null && matchPopupPanel != null && matchPopupPanel.activeSelf)
            loadingImage.Rotate(0f, 0f, +rotateSpeed * Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (FusionLobbyManager.Instance != null)
        {
            FusionLobbyManager.Instance.OnStatusChanged -= HandleStatusChanged;
            FusionLobbyManager.Instance.OnPlayerCountChanged -= HandlePlayerCountChanged;
        }
    }

    private void SetupInitialUI()
    {
        if (matchPopupPanel != null)
            matchPopupPanel.SetActive(false);

        if (findMatchButton != null)
            findMatchButton.interactable = false;

        if (statusText != null)
            statusText.text = "";

        if (playerCountText != null)
            playerCountText.text = "(0/4)";
    }

    private void RefreshNickname()
    {
        if (nicknameText == null)
            return;

        if (PlayerSession.Instance != null && PlayerSession.Instance.IsLoggedIn)
            nicknameText.text = PlayerSession.Instance.Nickname;
        else
            nicknameText.text = "Guest";
    }

    public void OnClickActor()
    {
        currentRole = MatchRole.Actor;
        if (findMatchButton != null)
            findMatchButton.interactable = true;
        if (FusionLobbyManager.Instance != null)
            FusionLobbyManager.Instance.SetSelectedRole(currentRole);
    }

    public void OnClickVillain()
    {
        currentRole = MatchRole.Villain;
        if (findMatchButton != null)
            findMatchButton.interactable = true;
        if (FusionLobbyManager.Instance != null)
            FusionLobbyManager.Instance.SetSelectedRole(currentRole);
    }

    public void OnClickFindMatch()
    {
        if (currentRole == MatchRole.None)
            return;

        if (matchPopupPanel != null)
            matchPopupPanel.SetActive(true);
        if (statusText != null)
            statusText.text = "플레이어를 찾는 중...";
        if (playerCountText != null)
            playerCountText.text = "(0/4)";
        if (FusionLobbyManager.Instance != null)
            FusionLobbyManager.Instance.StartMatchmaking();
    }

    public void OnClickCancelMatch()
    {
        if (FusionLobbyManager.Instance != null)
            FusionLobbyManager.Instance.CancelMatchmaking();
        if (matchPopupPanel != null)
            matchPopupPanel.SetActive(false);
        if (statusText != null)
            statusText.text = "";
        if (playerCountText != null)
            playerCountText.text = "(0/4)";
    }

    public void OnClickBack()
    {
        SceneManager.LoadScene("MainScene");
    }

    private void HandleStatusChanged(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void HandlePlayerCountChanged(int current, int max)
    {
        if (playerCountText != null)
            playerCountText.text = $"({current}/{max})";
    }
}
