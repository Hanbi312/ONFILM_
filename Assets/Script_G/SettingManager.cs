using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class SettingsManager : MonoBehaviour
{
    [Header("슬라이더")]
    public Slider mainVolumeSlider;
    public Slider menuVolumeSlider;
    public Slider sensitivitySlider;

    [Header("오디오 믹서")]
    public AudioMixer audioMixerMain;
    public AudioMixer audioMixerMenu;

    [Header("범위 설정")]
    public float sensitivityMin = 0.1f;
    public float sensitivityMax = 5f;

    public static SettingsManager Instance;

    public static event System.Action<float> OnSensitivityChanged;

    public float CurrentSensitivity { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // 슬라이더 범위 설정 (0~1)
        mainVolumeSlider.minValue = 0f;
        mainVolumeSlider.maxValue = 1f;
        menuVolumeSlider.minValue = 0f;
        menuVolumeSlider.maxValue = 1f;
        sensitivitySlider.minValue = sensitivityMin;
        sensitivitySlider.maxValue = sensitivityMax;

        // 저장된 값 불러오기
        mainVolumeSlider.value = PlayerPrefs.GetFloat("MainVolume", 1f);
        menuVolumeSlider.value = PlayerPrefs.GetFloat("MenuVolume", 1f);
        sensitivitySlider.value = PlayerPrefs.GetFloat("Sensitivity", 2f);

        // 불러온 값 즉시 적용
        ApplyMainVolume(mainVolumeSlider.value);
        ApplyMenuVolume(menuVolumeSlider.value);
        ApplySensitivity(sensitivitySlider.value);

        // 슬라이더 값이 바뀔 때마다 자동 호출
        mainVolumeSlider.onValueChanged.AddListener(ApplyMainVolume);
        menuVolumeSlider.onValueChanged.AddListener(ApplyMenuVolume);
        sensitivitySlider.onValueChanged.AddListener(ApplySensitivity);
    }

    // 슬라이더 0~1 값을 AudioMixer가 사용하는 dB(-80~0)로 변환
    float ToDecibel(float value)
    {
        // value가 0이면 log가 -무한대가 되므로 최솟값 처리
        return value > 0.0001f ? Mathf.Log10(value) * 20f : -80f;
    }

    void ApplyMainVolume(float value)
    {
        audioMixerMain.SetFloat("MainVolume", ToDecibel(value));
        PlayerPrefs.SetFloat("MainVolume", value);
    }

    void ApplyMenuVolume(float value)
    {
        audioMixerMenu.SetFloat("MenuVolume", ToDecibel(value));
        PlayerPrefs.SetFloat("MenuVolume", value);
    }

    void ApplySensitivity(float value)
    {
        CurrentSensitivity = value;

        OnSensitivityChanged?.Invoke(value);

        PlayerPrefs.SetFloat("Sensitivity", value);
    }

    public void SaveSettings()
    {
        PlayerPrefs.Save();
    }

    void OnDestroy()
    {
        mainVolumeSlider.onValueChanged.RemoveListener(ApplyMainVolume);
        menuVolumeSlider.onValueChanged.RemoveListener(ApplyMenuVolume);
        sensitivitySlider.onValueChanged.RemoveListener(ApplySensitivity);
    }
}