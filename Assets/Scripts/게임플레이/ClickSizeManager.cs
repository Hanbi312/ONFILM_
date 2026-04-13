using UnityEngine;
using UnityEngine.UI;

public class ClickSizeManager : MonoBehaviour
{
    Image img;

    void Start()
    {
        img = GetComponent<Image>();
        img.alphaHitTestMinimumThreshold = 0.5f;
    }
}