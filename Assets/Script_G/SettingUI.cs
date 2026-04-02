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

            if (isOpen)
            {
                settingUI.SetActive(true);
                isUIOpen = true;
                isAnyUIOpen = true;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                settingUI.SetActive(false);
                isUIOpen = false;
                isAnyUIOpen = false;

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        for (int i=0; i < txt.Length; i++)
        {
            txt[i].text = KeySetting.keys[(KeyAction)i].ToString();
        }
    }
}