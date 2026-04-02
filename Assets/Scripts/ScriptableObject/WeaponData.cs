using UnityEngine;

// ─────────────────────────────────────────────
// WeaponData - 무기 1개의 데이터를 담는 ScriptableObject
//
// [만드는 법]
//   Project 창 우클릭 → Create → GameData → WeaponData
//
// [무기 카테고리]
//   캐릭터 선택 UI 탭 구성: 단검 / 장검 / 짧은둔기 / 긴둔기 / 특수소품
//   WeaponCategory enum을 기준으로 카테고리 분류됨
// ─────────────────────────────────────────────
[CreateAssetMenu(fileName = "NewWeapon", menuName = "GameData/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("기본 정보")]
    public string weaponName;           // 무기 이름
    public Sprite icon;                 // 무기 아이콘 (캐릭터 선택 UI 표시용)
    public WeaponCategory category;     // 무기 카테고리 (탭 분류용)

    [Header("스탯")]
    public float range;                 // 사거리 (m)
    public float castDelay;             // 선 딜레이 (s) - 공격 모션 시작 후 판정까지 시간
    public float successDelay;          // 성공 딜레이 (s) - 맞은 후 다음 공격까지 시간
    public float failDelay;             // 실패 딜레이 (s) - 빗나간 후 다음 공격까지 시간

    [Header("3D 모델")]
    public GameObject modelPrefab;      // 게임플레이 씬에서 손에 들 무기 프리팹

    [Header("애니메이션")]
    public string attackAnimTrigger = "Attack"; // 공격 애니메이션 트리거 이름 (Animator 파라미터와 일치)
}

// 캐릭터 선택 UI 탭의 무기 카테고리
public enum WeaponCategory
{
    단검,
    장검,
    짧은둔기,
    긴둔기,
    특수소품
}
