using UnityEngine;
using UnityEngine.SceneManagement;

public class MouseLock : MonoBehaviour
{
    public static MouseLock Instance;

    public GameState currentState;
    private GameState previousState;
    public GameState startState = GameState.Gameplay;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"씬 로드됨: {scene.name}");

        switch (scene.name)
        {
            case "AuthScene":
                SetState(GameState.UI);
                break;

            case "GameplayScene":
                SetState(GameState.Gameplay);
                break;
           default:
                SetState(GameState.UI);
                break;
        }
    }

    void Start()
    {
        Debug.Log("CursorManager 실행됨");
        SetState(startState);
    }

    void Awake()
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

    public void SetState(GameState newState)
    {
        currentState = newState;
        ApplyCursorState();
    }

    public void PushState(GameState newState)
    {
        previousState = currentState;
        SetState(newState);
    }

    public void PopState()
    {
        SetState(previousState);
    }

    void ApplyCursorState()
    {
        switch (currentState)
        {
            case GameState.Gameplay:
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                break;

            case GameState.UI:
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                break;

            case GameState.MiniGame:
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = true;
                break;
        }

        // ★ 수정: ApplyCursorState 호출 시에만 1회 로그 (Update에서 매 프레임 찍지 않음)
        Debug.Log($"[MouseLock] 상태 변경 → State={currentState} | LockState={Cursor.lockState} | Visible={Cursor.visible}");
    }

    // ★ 수정: Update에서 Debug.Log 완전 제거
    // 이전 코드가 매 프레임 Debug.Log를 찍어서 초당 60개 로그 → 심각한 성능 저하 유발
    void Update()
    {
        // 필요한 로직이 생기면 여기에 추가
    }
}

public enum GameState
{
    Gameplay,
    UI,
    MiniGame
}