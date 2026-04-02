using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────
// CharacterSelectSession
// 캐릭터/무기 선택 결과를 게임플레이 씬까지 유지하는 싱글톤
//
// [사용법]
//   선택 완료 후: CharacterSelectSession.Instance.SetCharacter(data);
//   게임플레이 씬에서 읽기: CharacterSelectSession.Instance.SelectedCharacter
//
// [AllCharacters / AllWeapons]
//   GameplayManager에서 이름으로 데이터를 검색할 때 사용
//   CharacterSelectUIController의 allCharacters/allWeapons와 같은 에셋을 연결하면 됨
// ─────────────────────────────────────────────
public class CharacterSelectSession : MonoBehaviour
{
    public static CharacterSelectSession Instance;

    // 선택한 캐릭터/무기 (게임플레이 씬에서 읽어서 스폰에 적용)
    public CharacterData SelectedCharacter { get; private set; }
    public WeaponData SelectedWeapon { get; private set; }   // 악역만 사용, 연기자는 null

    // 전체 데이터 목록 - GameplayManager에서 이름으로 검색할 때 사용
    // Inspector에서 CharacterSelectUIController와 동일한 에셋 연결
    [Header("전체 데이터 (GameplayManager 검색용)")]
    public List<CharacterData> AllCharacters;
    public List<WeaponData> AllWeapons;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetCharacter(CharacterData character)
    {
        SelectedCharacter = character;
        Debug.Log($"[CharacterSelectSession] 캐릭터 선택: {character?.characterName}");
    }

    public void SetWeapon(WeaponData weapon)
    {
        SelectedWeapon = weapon;
        Debug.Log($"[CharacterSelectSession] 무기 선택: {weapon?.weaponName}");
    }

    // 로비로 돌아갈 때 초기화
    public void Clear()
    {
        SelectedCharacter = null;
        SelectedWeapon = null;
    }
}
