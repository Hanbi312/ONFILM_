using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseLock : MonoBehaviour
{

    public GameObject player;
    public float xmove;
    public float ymove;

    public Vector3 offset = new Vector3(0, 1.3f, -2.5f);

    void Update()
    {
        if (SettingUI.isAnyUIOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;

        }

        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        xmove += Input.GetAxis("Mouse X") * 200 * Time.deltaTime;
        ymove -= Input.GetAxis("Mouse Y") * 200 * Time.deltaTime;

        ymove = Mathf.Clamp(ymove, -40f, 70f);

        transform.rotation = Quaternion.Euler(ymove, xmove, 0);

        transform.position = player.transform.position + transform.rotation * offset;
    }
}
