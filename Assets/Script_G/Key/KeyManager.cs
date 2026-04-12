using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using TMPro;

public enum KeyAction
{
    UP, DOWN, LEFT, RIGHT,
    WALK,        // LeftShift
    SIT,        // LeftControl
    INTERACTION,// Mouse0
    SKILL,      // Mouse1
    TRAITA,     // E
    TRAITB,     // R
    HEAL,       // H
    VAULT,      // V
    KEYCOUNT
}

public static class KeySetting
{
    public static Dictionary<KeyAction, KeyCode> keys = new Dictionary<KeyAction, KeyCode>();
}

public class KeyManager : MonoBehaviour
{
    KeyCode[] defaultKeys = new KeyCode[]
    {
        KeyCode.W,            // UP
        KeyCode.S,            // DOWN
        KeyCode.A,            // LEFT
        KeyCode.D,            // RIGHT
        KeyCode.LeftShift,    // WALK
        KeyCode.LeftControl,  // SIT
        KeyCode.Mouse0,       // INTERACTION
        KeyCode.Mouse1,       // SKILL
        KeyCode.E,            // TRAITA
        KeyCode.R,            // TRAITB
        KeyCode.H,            // HEAL
        KeyCode.V,            // VAULT
    };

    [Header("키 버튼 텍스트 (순서가 KeyAction 열거형과 일치해야 함)")]
    public TMP_Text[] keyTexts; // Inspector에서 각 키 버튼의 텍스트 연결

    public Color defaultColor = Color.black;    // 기본 색상
    public Color activeColor = Color.red;    // 변경 중 색상

    public TMP_Text warningText;            //경고 문구 출력
    public float warningDuration = 1f;      //경고 문구 출력 시간


    private void Awake()
    {
        // 저장된 키 설정 무시 - 항상 기본값으로 초기화
        PlayerPrefs.DeleteAll();
        KeySetting.keys.Clear();

        for (int i = 0; i < (int)KeyAction.KEYCOUNT; i++)
        {
            KeyAction action = (KeyAction)i;
            KeySetting.keys.Add(action, defaultKeys[i]);
        }
    }

    void OnGUI()
    {
        if (key == -1) return;

        Event keyEvent = Event.current;

        if (keyEvent.type == EventType.KeyDown)
        {
            SetKey(key, keyEvent.keyCode);
        }

        if (keyEvent.type == EventType.MouseDown)
        {
            if (keyEvent.button == 0)
                SetKey(key, KeyCode.Mouse0);
            else if (keyEvent.button == 1)
                SetKey(key, KeyCode.Mouse1);
        }
    }

    int key = -1;

    public void ChangeKey(int num)
    {
        // 이전에 대기 중이던 텍스트 색상 복구
        if (key != -1 && keyTexts != null && key < keyTexts.Length && keyTexts[key] != null)
            keyTexts[key].color = defaultColor;

        key = num;

        // 새로 선택된 텍스트 색상 변경
        if (keyTexts != null && num < keyTexts.Length && keyTexts[num] != null)
            keyTexts[num].color = activeColor;
    }

    void SetKey(int keyIndex, KeyCode keyCode)
    {
        KeyAction action = (KeyAction)keyIndex;

        // 현재 값 저장 (유지용)
        KeyCode previousKey = KeySetting.keys[action];

        // 중복 검사
        foreach (var pair in KeySetting.keys)
        {
            if (pair.Value == keyCode && pair.Key != action)
            {
                ShowWarning($"해당 키({keyCode})는 이미 사용 중입니다.");

                // UI 색상 복구
                if (keyTexts != null && keyIndex < keyTexts.Length && keyTexts[keyIndex] != null)
                    keyTexts[keyIndex].color = defaultColor;

                key = -1;
                return; // 기존 값 유지
            }
        }

        // 딕셔너리 업데이트
        KeySetting.keys[action] = keyCode;

        // 컴퓨터에 저장
        PlayerPrefs.SetInt("Key_" + action.ToString(), (int)keyCode);
        PlayerPrefs.Save();

        // 색상 원래대로 복구
        if (keyTexts != null && keyIndex < keyTexts.Length && keyTexts[keyIndex] != null)
            keyTexts[keyIndex].color = defaultColor;

        key = -1;
    }

    // 모든 키를 기본값으로 초기화
    public void ResetToDefault()
    {
        for (int i = 0; i < (int)KeyAction.KEYCOUNT; i++)
        {
            KeyAction action = (KeyAction)i;
            KeySetting.keys[action] = defaultKeys[i];
            PlayerPrefs.SetInt("Key_" + action.ToString(), (int)defaultKeys[i]);
        }
        PlayerPrefs.Save();
    }

    Coroutine warningCoroutine;

    void ShowWarning(string message)
    {
        if (warningCoroutine != null)
            StopCoroutine(warningCoroutine);

        warningCoroutine = StartCoroutine(WarningRoutine(message));
    }

    IEnumerator WarningRoutine(string message)
    {
        warningText.text = message;
        warningText.gameObject.SetActive(true);

        yield return new WaitForSeconds(warningDuration);

        warningText.gameObject.SetActive(false);
    }
}