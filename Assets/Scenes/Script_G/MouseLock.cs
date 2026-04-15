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
        Debug.Log($"¾À ·ÎµåµÊ: {scene.name}");

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
        Debug.Log("CursorManager ½ÇÇàµÊ");
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

        Debug.Log("State: " + currentState);
        Debug.Log("LockState: " + Cursor.lockState);
        Debug.Log("Visible: " + Cursor.visible);
    }

    void Update()
    {
        Debug.Log($"[MouseLock] ÇöÀç »óÅÂ: {currentState}");
    }
}

public enum GameState
{
    Gameplay,
    UI,
    MiniGame
}
