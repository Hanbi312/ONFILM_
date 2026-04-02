using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;


public class MainUIController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject registerPanel;

    [Header("Login UI")]
    [SerializeField] private TMP_InputField loginEmailInput;
    [SerializeField] private TMP_InputField loginPasswordInput;
    [SerializeField] private TMP_Text loginMessageText;

    [Header("Register UI")]
    [SerializeField] private TMP_InputField registerEmailInput;
    [SerializeField] private TMP_InputField registerPasswordInput;
    [SerializeField] private TMP_InputField registerPasswordConfirmInput;
    [SerializeField] private TMP_InputField registerNicknameInput;
    [SerializeField] private TMP_Text registerMessageText;

    private bool isBusy;

    private void Start()
    {
        ShowLoginPanel();
    }

    public void ShowLoginPanel()
    {
        loginPanel.SetActive(true);
        registerPanel.SetActive(false);
        ClearMessages();
    }

    public void ShowRegisterPanel()
    {
        loginPanel.SetActive(false);
        registerPanel.SetActive(true);
        ClearMessages();
    }

    public void OnClickRegister()
    {
        if (isBusy) return;

        ClearMessages();

        string email = registerEmailInput.text.Trim();
        string password = registerPasswordInput.text;
        string passwordConfirm = registerPasswordConfirmInput.text;
        string nickname = registerNicknameInput.text.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            registerMessageText.text = "이메일을 입력해주세요.";
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            registerMessageText.text = "비밀번호를 입력해주세요.";
            return;
        }

        if (string.IsNullOrWhiteSpace(passwordConfirm))
        {
            registerMessageText.text = "비밀번호 확인을 입력해주세요.";
            return;
        }

        if (password != passwordConfirm)
        {
            registerMessageText.text = "비밀번호와 비밀번호 확인이 일치하지 않습니다.";
            return;
        }

        if (string.IsNullOrWhiteSpace(nickname))
        {
            registerMessageText.text = "닉네임을 입력해주세요.";
            return;
        }

        isBusy = true;

        PlayFabAuthManager.Instance.Register(email, password, nickname,
            () =>
            {
                isBusy = false;
                SceneManager.LoadScene("LobbyScene");
            },
            error =>
            {
                isBusy = false;
                registerMessageText.text = error;
            });
    }

    public void OnClickLogin()
    {
        if (isBusy) return;

        ClearMessages();

        string email = loginEmailInput.text.Trim();
        string password = loginPasswordInput.text;

        if (string.IsNullOrWhiteSpace(email))
        {
            loginMessageText.text = "이메일을 입력해주세요.";
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            loginMessageText.text = "비밀번호를 입력해주세요.";
            return;
        }

        isBusy = true;

        PlayFabAuthManager.Instance.Login(email, password,
            () =>
            {
                isBusy = false;
                SceneManager.LoadScene("LobbyScene");
            },
            error =>
            {
                isBusy = false;
                loginMessageText.text = error;
            });
    }

    private void ClearMessages()
    {
        if (loginMessageText != null)
            loginMessageText.text = "";

        if (registerMessageText != null)
            registerMessageText.text = "";
    }
}