using UnityEngine;

// ─────────────────────────────────────────────
// CharacterData - 캐릭터 1개의 데이터를 담는 ScriptableObject
//
// [만드는 법]
//   Project 창 우클릭 → Create → GameData → CharacterData
//
// [추가 필드가 필요할 때]
//   원하는 타입의 public 변수를 아래에 추가하면 Inspector에서 바로 편집 가능
// ─────────────────────────────────────────────
[CreateAssetMenu(fileName = "NewCharacter", menuName = "GameData/CharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("기본 정보")]
    public string characterName;        // 캐릭터 이름
    public Sprite portrait;             // 캐릭터 선택 UI에 표시할 초상화/아이콘
    public Sprite previewImage;         // 캐릭터 클릭 시 오른쪽에 표시할 이미지
    public GameObject modelPrefab;      // 선택 화면에 표시할 3D 모델 프리팹

    [Header("진영")]
    public MatchRole role;              // Actor(연기자) 또는 Villain(악역)

    [Header("설명")]
    [TextArea] public string description; // 캐릭터 설명 (선택 UI에 표시용, 선택 사항)
}
