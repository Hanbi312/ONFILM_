using UnityEngine;

public class PlayerSession : MonoBehaviour
{
    public static PlayerSession Instance;

    public string PlayFabId;
    public string Email;
    public string Nickname;
    public bool IsLoggedIn;

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

    public void SetSession(string playFabId, string email, string nickname)
    {
        PlayFabId = playFabId;
        Email = email;
        Nickname = nickname;
        IsLoggedIn = true;
    }

    public void ClearSession()
    {
        PlayFabId = "";
        Email = "";
        Nickname = "";
        IsLoggedIn = false;
    }
}