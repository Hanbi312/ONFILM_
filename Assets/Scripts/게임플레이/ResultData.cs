using UnityEngine;

/// <summary>
/// ResultData - 게임 결과를 결과 씬까지 전달하는 싱글톤
/// </summary>
public class ResultData : MonoBehaviour
{
    public static ResultData Instance;
    public static bool IsActorWin; // true=연기자 승(해피엔딩), false=악역 승(베드엔딩)

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
}
