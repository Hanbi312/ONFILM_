using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ProgressBar : MonoBehaviour
{
    public Slider progressBar;
    public float maxGage = 100f;

    private float currentGage;
    Generator generator;
    // ★ SecurityCamera도 지원
    SecurityCamera securityCamera;

    void Start()
    {
        currentGage = 0f;
        generator = FindObjectOfType<Generator>();
        UpdateGauge();
    }

    void Update()
    {
        // ★ SecurityCamera가 연결됐으면 그 actPoint 사용
        if (securityCamera != null)
        {
            currentGage = securityCamera.actPoint;
            currentGage = Mathf.Clamp(currentGage, 0f, maxGage);
            UpdateGauge();
            return;
        }

        if (generator == null)
            return;

        currentGage = generator.actPoint;
        currentGage = Mathf.Clamp(currentGage, 0f, maxGage);
        UpdateGauge();
    }

    void UpdateGauge()
    {
        if (progressBar != null)
            progressBar.value = currentGage / maxGage;
    }

    public void SetTimerRatio(float ratio)
    {
        if (progressBar != null)
            progressBar.value = ratio;
    }

    // ★ SecurityCamera에서 호출해서 연결
    public void SetSecurityCamera(SecurityCamera cam)
    {
        securityCamera = cam;
        if (cam != null)
            maxGage = cam.maxActPoint; // maxGage를 카메라 설정과 동기화
    }
}
