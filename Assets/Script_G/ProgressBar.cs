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
    SecurityCamera securityCamera;
    VillainCamera villainCamera;

    void Start()
    {
        currentGage = 0f;
        generator = FindObjectOfType<Generator>();
        UpdateGauge();
    }

    void Update()
    {
        if (villainCamera != null)
        {
            currentGage = Mathf.Clamp(villainCamera.actPoint, 0f, maxGage);
            UpdateGauge();
            return;
        }

        if (securityCamera != null)
        {
            currentGage = Mathf.Clamp(securityCamera.actPoint, 0f, maxGage);
            UpdateGauge();
            return;
        }

        if (generator == null) return;
        currentGage = Mathf.Clamp(generator.actPoint, 0f, maxGage);
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

    public void SetSecurityCamera(SecurityCamera cam)
    {
        securityCamera = cam;
        villainCamera = null;
        if (cam != null) maxGage = cam.maxActPoint;
    }

    public void SetVillainCamera(VillainCamera cam)
    {
        villainCamera = cam;
        securityCamera = null;
        if (cam != null) maxGage = cam.maxActPoint;
    }
}
