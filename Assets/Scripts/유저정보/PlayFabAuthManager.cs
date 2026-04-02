using System;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using TMPro;

public class PlayFabAuthManager : MonoBehaviour
{
    public static PlayFabAuthManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Register(string email, string password, string nickname,
        Action onSuccess,
        Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            onError?.Invoke("이메일을 입력해주세요.");
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            onError?.Invoke("비밀번호를 입력해주세요.");
            return;
        }

        if (string.IsNullOrWhiteSpace(nickname))
        {
            onError?.Invoke("닉네임을 입력해주세요.");
            return;
        }

        var request = new RegisterPlayFabUserRequest
        {
            Email = email,
            Password = password,
            Username = nickname,
            DisplayName = nickname,
            RequireBothUsernameAndEmail = false
        };

        PlayFabClientAPI.RegisterPlayFabUser(request,
            result =>
            {
                Login(email, password, onSuccess, onError);
            },
            error =>
            {
                onError?.Invoke(GetReadableError(error));
            });
    }

    public void Login(string email, string password,
        Action onSuccess,
        Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            onError?.Invoke("이메일을 입력해주세요.");
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            onError?.Invoke("비밀번호를 입력해주세요.");
            return;
        }

        var request = new LoginWithEmailAddressRequest
        {
            Email = email,
            Password = password,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true
            }
        };

        PlayFabClientAPI.LoginWithEmailAddress(request,
            result =>
            {
                string playFabId = result.PlayFabId;
                string nickname = "NoName";

                if (result.InfoResultPayload != null &&
                    result.InfoResultPayload.PlayerProfile != null &&
                    !string.IsNullOrEmpty(result.InfoResultPayload.PlayerProfile.DisplayName))
                {
                    nickname = result.InfoResultPayload.PlayerProfile.DisplayName;
                }

                if (PlayerSession.Instance != null)
                {
                    PlayerSession.Instance.SetSession(playFabId, email, nickname);
                }

                onSuccess?.Invoke();
            },
            error =>
            {
                onError?.Invoke(GetReadableError(error));
            });
    }

    private string GetReadableError(PlayFabError error)
    {
        switch (error.Error)
        {
            case PlayFabErrorCode.InvalidEmailAddress:
                return "올바른 이메일 형식이 아닙니다.";
            case PlayFabErrorCode.EmailAddressNotAvailable:
                return "이미 가입된 이메일입니다.";
            case PlayFabErrorCode.UsernameNotAvailable:
                return "이미 사용 중인 닉네임입니다.";
            case PlayFabErrorCode.InvalidPassword:
                return "비밀번호 형식이 올바르지 않습니다.";
            case PlayFabErrorCode.AccountNotFound:
                return "계정을 찾을 수 없습니다.";
            case PlayFabErrorCode.InvalidEmailOrPassword:
                return "이메일 또는 비밀번호가 올바르지 않습니다.";
            default:
                return $"오류: {error.ErrorMessage}";
        }
    }
}