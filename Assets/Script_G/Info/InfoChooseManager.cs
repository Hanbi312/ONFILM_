using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InfoChooseManager : MonoBehaviour
{
    [System.Serializable]
    public class InfoData
    {
        public string name;                 // 이름
        public Sprite portrite;             // UI에 표시할 스프라이트
        [TextArea]
        public string info;                 // 설명
        [TextArea]
        public string cooltime;             // 쿨타임
    }

    [Header("상세 설명 (Inspector에서 설정)")]
    public InfoData[] InfoDatas;

    [Header("UI")]
    public Image portriteSpace;             // 초상화
    public TMP_Text nameSpace;              // 이름
    public TMP_Text infoSpace;              // 설명
    public TMP_Text cooltimeSpace;              // 액티브/패시브, 쿨타임

    [Header("카테고리 패널 (Inspector에서 순서대로 연결)")]
    public GameObject[] categoryPanels; // 0 = 패널1, 1 = 패널2, 2 = 패널3

    // 버튼의 OnClick()에서 이 함수를 호출할 때 인덱스를 넘겨줌
    // ex) 첫 번째 버튼 → ClickButton(0), 두 번째 버튼 → ClickButton(1)
    public void ClickButton(int charaindex)
    {
        if (charaindex < 0 || charaindex >= InfoDatas.Length)
        {
            Debug.LogWarning($"[InfoChooseManager] 잘못된 인덱스: {charaindex}");
            return;
        }

        InfoData data = InfoDatas[charaindex];

        portriteSpace.sprite = data.portrite;
        nameSpace.text = data.name;
        infoSpace.text = data.info;
        cooltimeSpace.text = data.cooltime;
    }

    public void CategoryClick(int btnindex)
    {
        for (int i = 0; i < categoryPanels.Length; i++)
        {
            categoryPanels[i].SetActive(i == btnindex);
        }

        ClearInfo();
    }

    public void ClearInfo()
    {
        portriteSpace.sprite = null;
        //portriteSpace.color = Color.clear; // 이미지 투명하게
        nameSpace.text = "";
        infoSpace.text = "";
        cooltimeSpace.text = "";
    }

    void Start()
    {
        ClearInfo();
    }

    void Update()
    {
        
    }
}
