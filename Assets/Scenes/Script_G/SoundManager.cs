using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [System.Serializable]
    public class SoundData
    {
        public string soundName;    // 소리 이름 (식별용)
        public AudioClip clip;      // 오디오 클립
    }

    [Header("사운드 목록")]
    public SoundData[] sounds;
    public AudioSource audioSource;

    // 이름으로 재생
    public void PlaySound(string soundName)
    {
        SoundData data = System.Array.Find(sounds, s => s.soundName == soundName);

        if (data == null)
        {
            Debug.LogWarning($"[SoundManager] '{soundName}' 사운드를 찾을 수 없습니다.");
            return;
        }

        audioSource.PlayOneShot(data.clip);
    }

    // 인덱스로 재생
    public void PlaySound(int index)
    {
        if (index < 0 || index >= sounds.Length)
        {
            Debug.LogWarning($"[SoundManager] 잘못된 인덱스: {index}");
            return;
        }

        audioSource.PlayOneShot(sounds[index].clip);
    }
}