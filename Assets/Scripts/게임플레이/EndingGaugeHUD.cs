using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class EndingGaugeHUD : MonoBehaviour
{
    [Header("게이지 칸 (왼쪽→오른쪽 순서로 연결)")]
    [SerializeField] private Image[] gaugeSlots; // 총 8칸

    [Header("스프라이트 (슬롯 순서와 동일하게 연결)")]
    [SerializeField] private Sprite[] happySprites; // 왼쪽 4칸 스프라이트 (인덱스 0~3)
    [SerializeField] private Sprite[] badSprites;   // 오른쪽 4칸 스프라이트 (인덱스 0~3)

    [Header("설정")]
    [SerializeField] private int maxHappyCount = 4;
    [SerializeField] private int maxBadCount = 4;

    private int lastCameraOffCount = -1;
    private int lastTragedyPoint = -1;

    private void Start()
    {
        ResetAllSlots();
    }

    private void Update()
    {
        if (GameStateManager.Instance == null) return;
        if (GameStateManager.Instance.Object == null || !GameStateManager.Instance.Object.IsValid) return;

        int cameraOff = GameStateManager.Instance.CameraOffCount;
        int tragedyPoint = GameStateManager.Instance.TragedyPoint;

        if (cameraOff == lastCameraOffCount && tragedyPoint == lastTragedyPoint) return;
        lastCameraOffCount = cameraOff;
        lastTragedyPoint = tragedyPoint;

        UpdateGauge(cameraOff, tragedyPoint);
    }

    private void UpdateGauge(int happyCount, int badCount)
    {
        if (gaugeSlots == null) return;

        int totalSlots = gaugeSlots.Length;

        for (int i = 0; i < totalSlots; i++)
        {
            if (gaugeSlots[i] == null) continue;

            if (i < maxHappyCount)
            {
                // 왼쪽 4칸: 해피엔딩 (왼쪽부터 채워짐)
                // happySprites[i] = 슬롯 i번에 해당하는 스프라이트
                bool filled = i < happyCount;
                Sprite sprite = (happySprites != null && i < happySprites.Length) ? happySprites[i] : null;

                gaugeSlots[i].sprite = filled ? sprite : null;
                gaugeSlots[i].color  = filled ? Color.white : Color.clear;
            }
            else
            {
                // 오른쪽 4칸: 베드엔딩 (오른쪽부터 채워짐)
                int badSlotIndex = totalSlots - 1 - i; // 오른쪽 끝부터 채우기 위한 인덱스
                bool filled = badSlotIndex < badCount;
                // badSprites 배열은 오른쪽 슬롯 기준 인덱스로 접근 (슬롯4→badSprites[0], 슬롯5→badSprites[1]...)
                int badSpriteIndex = i - maxHappyCount;
                Sprite sprite = (badSprites != null && badSpriteIndex < badSprites.Length) ? badSprites[badSpriteIndex] : null;

                gaugeSlots[i].sprite = filled ? sprite : null;
                gaugeSlots[i].color  = filled ? Color.white : Color.clear;
            }
        }
    }

    private void ResetAllSlots()
    {
        if (gaugeSlots == null) return;
        foreach (var slot in gaugeSlots)
        {
            if (slot != null)
            {
                slot.sprite = null;
                slot.color = Color.clear;
            }
        }
    }
}