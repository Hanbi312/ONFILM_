using UnityEngine;

public class EndingGaugeHUD : MonoBehaviour
{
    [Header("게이지 슬롯 오브젝트 (왼쪽→오른쪽 순서로 연결, 총 8개)")]
    [SerializeField] private GameObject[] gaugeSlots;

    [Header("설정")]
    [SerializeField] private int maxHappyCount = 4;
    [SerializeField] private int maxBadCount   = 4;

    private int  lastCameraOffCount  = -1;
    private int  lastTragedyPoint    = -1;
    private bool loggedInitialState  = false;

    private void Awake()  { SetAllSlots(false); }
    private void OnEnable() { SetAllSlots(false); }  // Canvas 재활성화 시에도 초기화
    private void Start()  { SetAllSlots(false); }

    private void Update()
    {
        // Inspector 연결 여부 경고
        if (gaugeSlots == null || gaugeSlots.Length == 0)
        {
            if (!loggedInitialState)
            {
                Debug.LogWarning("[EndingGaugeHUD] gaugeSlots 배열이 비어있음! Inspector에서 연결 필요");
                loggedInitialState = true;
            }
            return;
        }

        if (GameStateManager.Instance == null ||
            GameStateManager.Instance.Object == null ||
            !GameStateManager.Instance.Object.IsValid)
        {
            SetAllSlots(false);
            lastCameraOffCount = 0;
            lastTragedyPoint   = 0;
            return;
        }

        int cameraOff    = GameStateManager.Instance.CameraOffCount;
        int tragedyPoint = GameStateManager.Instance.TragedyPoint;

        // 최초 유효 읽기 시 현재 값을 로그로 확인
        if (!loggedInitialState)
        {
            loggedInitialState = true;
            Debug.Log($"[EndingGaugeHUD] 초기 상태 | CameraOffCount={cameraOff} | TragedyPoint={tragedyPoint}");
            // 혹시 0이 아닌 값이라면 로그 확인 필요
            if (cameraOff != 0 || tragedyPoint != 0)
                Debug.LogWarning($"[EndingGaugeHUD] 시작 시 카운트가 0이 아님! CameraOff={cameraOff}, Tragedy={tragedyPoint}");
        }

        if (cameraOff == lastCameraOffCount && tragedyPoint == lastTragedyPoint) return;
        lastCameraOffCount = cameraOff;
        lastTragedyPoint   = tragedyPoint;

        UpdateGauge(cameraOff, tragedyPoint);
    }

    private void UpdateGauge(int happyCount, int badCount)
    {
        if (gaugeSlots == null) return;
        int totalSlots = gaugeSlots.Length;

        for (int i = 0; i < totalSlots; i++)
        {
            if (gaugeSlots[i] == null) continue;

            bool active;
            if (i < maxHappyCount)
            {
                active = i < happyCount;
            }
            else if (i < maxHappyCount + maxBadCount)
            {
                int badSlotIndex = i - maxHappyCount;
                active = badSlotIndex < badCount;
            }
            else
            {
                active = false;
            }

            gaugeSlots[i].SetActive(active);
        }
    }

    private void SetAllSlots(bool active)
    {
        if (gaugeSlots == null) return;
        foreach (var slot in gaugeSlots)
            if (slot != null) slot.SetActive(active);
    }
}