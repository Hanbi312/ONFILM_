using System.Collections.Generic;
using UnityEngine;

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

    private void Awake()
    {
        for (int i = 0; i < (int)KeyAction.KEYCOUNT; i++)
        {
            KeySetting.keys.Add((KeyAction)i, defaultKeys[i]);
        }
    }

    void OnGUI()
    {
        if (key == -1) return;

        Event keyEvent = Event.current;

        if (keyEvent.type == EventType.KeyDown)
        {
            KeySetting.keys[(KeyAction)key] = keyEvent.keyCode;
            key = -1;
        }

        if (keyEvent.type == EventType.MouseDown)
        {
            if (keyEvent.button == 0)
                KeySetting.keys[(KeyAction)key] = KeyCode.Mouse0;
            else if (keyEvent.button == 1)
                KeySetting.keys[(KeyAction)key] = KeyCode.Mouse1;

            key = -1;
        }
    }

    int key = -1;
    public void ChangeKey(int num)
    {
        key = num;
    }
}