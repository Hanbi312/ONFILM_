using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PoseGame : MonoBehaviour
{
    // 키워드 + 스프라이트 쌍
    [System.Serializable]
    public class KeywordData
    {
        public string keyword;
        public Sprite sprite;           // UI에 표시할 스프라이트
        public string animationTrigger; // 플레이어 Animator에 넘길 트리거 이름 (예: "Despair", "Joy")
    }

    [Header("키워드 데이터 (Inspector에서 연결)")]
    public KeywordData[] keywordDatas;

    [Header("UI")]
    public Image centerImage;           // 중앙 정답 이미지
    public Image[] outerImages;         // 선택지 이미지 3개
    public Button[] outerButtons;       // 선택지 버튼 3개
    public TMP_Text timerText;

    List<KeywordData> selectedDatas = new List<KeywordData>();
    KeywordData answerData;

    Generator generator;
    SecurityCamera securityCamera;
    System.Action<string> onSuccess; // 성공 시 animationTrigger 전달
    System.Action onFail;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) OnClickKeyword(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) OnClickKeyword(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) OnClickKeyword(2);

        if (timerText != null)
        {
            if (generator != null)
                timerText.text = (2f - generator.miniGameTime).ToString("F1") + "s";
            else if (securityCamera != null)
                timerText.text = (2f - securityCamera.miniGameTime).ToString("F1") + "s";
        }
    }

    public void SetupMiniGame(Generator gen)
    {
        generator = gen;
        securityCamera = null;
        SetupKeywords();
    }

    public void SetupMiniGame(SecurityCamera cam, System.Action<string> successCallback = null, System.Action failCallback = null)
    {
        securityCamera = cam;
        generator = null;
        onSuccess = successCallback;
        onFail = failCallback;
        SetupKeywords();
    }

    private void SetupKeywords()
    {
        selectedDatas.Clear();
        List<KeywordData> tempList = new List<KeywordData>(keywordDatas);

        for (int i = 0; i < 3; i++)
        {
            int rand = Random.Range(0, tempList.Count);
            selectedDatas.Add(tempList[rand]);
            tempList.RemoveAt(rand);
        }

        // 선택지 이미지 세팅
        for (int i = 0; i < 3; i++)
        {
            if (outerImages[i] != null)
                outerImages[i].sprite = selectedDatas[i].sprite;
        }

        // 정답 랜덤 선택 → 중앙 이미지 표시
        int answerIndex = Random.Range(0, 3);
        answerData = selectedDatas[answerIndex];
        if (centerImage != null)
            centerImage.sprite = answerData.sprite;

        SetupButtons();
    }

    void SetupButtons()
    {
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            outerButtons[i].onClick.RemoveAllListeners();
            outerButtons[i].onClick.AddListener(() => OnClickKeyword(index));
        }
    }

    void OnClickKeyword(int index)
    {
        if (index >= selectedDatas.Count) return;

        KeywordData clicked = selectedDatas[index];
        bool success = clicked == answerData;

        Debug.Log($"[PoseGame] 클릭: {clicked.keyword} | 정답: {answerData.keyword} | 성공: {success}");

        if (success)
        {
            Debug.Log("성공!");
            onSuccess?.Invoke(answerData.animationTrigger);
        }
        else
        {
            Debug.Log("실패!");
            onFail?.Invoke();

            if (generator != null) generator.EndMiniGame();
            else if (securityCamera != null) securityCamera.EndMiniGame();
        }

        onSuccess = null;
        onFail = null;
    }

    public void OnTimeout()
    {
        onFail?.Invoke();
        onSuccess = null;
        onFail = null;

        if (generator != null) generator.EndMiniGame();
        else if (securityCamera != null) securityCamera.EndMiniGame();
    }
}

