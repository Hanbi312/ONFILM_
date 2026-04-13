using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Generator : MonoBehaviour
{
    //플레이어에게 부착할 것!

    public GameObject pushE;
    bool playerInRange = false;
    ActorController playerControllerScript;
    public float actPoint = 0f;
    public float actSpeed = 1f;

    public GameObject miniGamePanel;

    public float minTriggerTime = 2f;
    public float maxTriggerTime = 5f;

    private float nextTriggerTime;
    private float timer;

    private bool isMiniGameActive = false;
    bool isActing = false;
    public float actProgress = 0f;
    public float miniGameTime = 0f;

    PoseGame script;

    void Start()
    {
        pushE = GameObject.Find("PushE");
        playerControllerScript = GetComponent<ActorController>();

        // MiniGameManager 싱글톤에서 패널/스크립트 가져오기
        if (MiniGameManager.Instance != null)
        {
            miniGamePanel = MiniGameManager.Instance.miniGamePanel;
            script = MiniGameManager.Instance.poseGameScript;
        }
        else
        {
            Debug.LogError("[Generator] MiniGameManager가 씬에 없음! 빈 오브젝트에 MiniGameManager 컴포넌트를 추가하세요.");
        }

        SetNextTriggerTime();
    }

    void Update()
    {
        if (isMiniGameActive)
        {
            miniGameTime += Time.deltaTime;

            if (miniGameTime >= 2f)
            {
                Debug.Log("시간 초과!");
                EndMiniGame();
            }

            return;
        }

        ActProgress();

        if (isActing)
            CheckMiniGameTrigger();
        else
            timer = 0f;

        if (miniGameTime >= 2f)
            EndMiniGame();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("generator"))
        {
            playerInRange = true;
            OpenPanel();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("generator"))
        {
            playerInRange = false;
            ClosePanel();
            Debug.Log("범위를 벗어남");

            timer = 0f;
            isMiniGameActive = false;
            if (miniGamePanel != null) miniGamePanel.SetActive(false);
        }
    }

    public void OpenPanel()
    {
        if (pushE != null) pushE.SetActive(true);
    }

    public void ClosePanel()
    {
        if (pushE != null) pushE.SetActive(false);
    }

    void ActProgress()
    {
        isActing = false;

        if (playerControllerScript == null) return;

        if (playerInRange && !playerControllerScript.IsDead)
        {
            if (Input.GetKey(KeyCode.E))
            {
                isActing = true;
                Debug.Log("연기 중...");
                actPoint += actSpeed * Time.deltaTime;

                if (actPoint >= 100f)
                {
                    Debug.Log("연기 성공!");
                    actPoint = 0f;
                }
            }
        }

        if (playerInRange && playerControllerScript.IsDead)
        {
            if (Input.GetKeyDown(KeyCode.E))
                Debug.Log("부상! 연기 불가능!");
        }
    }

    void CheckMiniGameTrigger()
    {
        timer += Time.deltaTime;
        if (timer >= nextTriggerTime)
        {
            TriggerMiniGame();
            SetNextTriggerTime();
        }
    }

    void SetNextTriggerTime()
    {
        timer = 0f;
        nextTriggerTime = Random.Range(minTriggerTime, maxTriggerTime);
    }

    void TriggerMiniGame()
    {
        if (miniGamePanel == null) return;

        isMiniGameActive = true;
        miniGamePanel.SetActive(true);

        MouseLock.Instance.PushState(GameState.MiniGame);

        if (script != null)
            script.SetupMiniGame(this);

        Debug.Log("미니게임 발생!");
    }

    public void EndMiniGame()
    {
        isMiniGameActive = false;
        if (miniGamePanel != null) miniGamePanel.SetActive(false);

        MouseLock.Instance.PopState();

        miniGameTime = 0f;
    }
}
