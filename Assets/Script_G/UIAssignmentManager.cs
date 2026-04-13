using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class UIAssignmentManager : MonoBehaviour
{
    public static UIAssignmentManager Instance;

    public GameObject PushE;
    public GameObject miniGamePanel;

    void Awake()
    {
        PushE = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(obj => obj.name == "PushE");

        miniGamePanel = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(obj => obj.name == "PoseGame");
        Instance = this;
    }
}