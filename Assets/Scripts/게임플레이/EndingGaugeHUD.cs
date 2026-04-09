using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class EndingGaugeHUD : MonoBehaviour
{
    [Header("게이지 칸 (왼쪽→오른쪽 순서로 연결)")]
    [SerializeField] private Image[] gaugeSlots; // 총 8칸

    [Header("스프라이트")]
    [SerializeField] private Sprite happySprite;  // 파란 마름모 스프라이트
    [SerializeField] private Sprite badSprite;    // 빨간 마름모 스프라이트

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
                if (i < happyCount)
                {
                    gaugeSlots[i].sprite = happySprite;
                    gaugeSlots[i].color = Color.white;
                }
                else
                {
                    // 빈 칸 - 투명 처리
                    gaugeSlots[i].sprite = null;
                    gaugeSlots[i].color = Color.clear;
                }
            }
            else
            {
                // 오른쪽 4칸: 베드엔딩 (오른쪽부터 채워짐)
                int badIndex = totalSlots - 1 - i;
                if (badIndex < badCount)
                {
                    gaugeSlots[i].sprite = badSprite;
                    gaugeSlots[i].color = Color.white;
                }
                else
                {
                    gaugeSlots[i].sprite = null;
                    gaugeSlots[i].color = Color.clear;
                }
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
