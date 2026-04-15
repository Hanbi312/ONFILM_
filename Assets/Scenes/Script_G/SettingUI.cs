using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingUI : MonoBehaviour
{
    public GameObject settingUI;
    public static bool isUIOpen = false;
    bool isOpen = false;
    public static bool isAnyUIOpen = false;

    public TMP_Text[] txt;

    void Start()
    {
        settingUI.SetActive(false);

        for(int i=0; i<txt.Length; i++)
        {
            txt[i].text = KeySetting.keys[(KeyAction)i].ToString();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isOpen = !isOpen;

            settingUI.SetActive(isOpen);

            if (!isOpen)
            {
                Open();
            }
            else
            {
                Close();
            }
        }

        if (MouseLock.Instance == null)
        {
            Debug.LogError("MouseLock ����!");
        }

        for (int i=0; i < txt.Length; i++)
        {
            txt[i].text = KeySetting.keys[(KeyAction)i].ToString();
        }
    }

    public void Open()
    {
        settingUI.SetActive(true);
        isUIOpen = true;
        isAnyUIOpen = true;

        MouseLock.Instance.PushState(GameState.UI);
        settingUI.SetActive(true);
    }

    public void Close()
    {
        settingUI.SetActive(false);
        isUIOpen = false;
        isAnyUIOpen = false;

        settingUI.SetActive(false);
        MouseLock.Instance.PopState();
    }
}