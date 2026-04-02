using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PoseGameHover : MonoBehaviour
{
    //호버 ------------------------------------------
    public Sprite hover_img;
    public Sprite no_hover_img;
    Image thisImg;
    //호버 ------------------------------------------

    void Start()
    {
        //호버
        thisImg = GetComponent<Image>();
    }

    //호버 함수------------------------------------------
    public void ChangeButtonUI_In()
    {
        thisImg.sprite = hover_img;
    }

    public void ChangeButtonUI_out()
    {
        thisImg.sprite = no_hover_img;
    }
    //호버 함수------------------------------------------
}
