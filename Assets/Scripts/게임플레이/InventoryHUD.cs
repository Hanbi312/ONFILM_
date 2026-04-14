using UnityEngine;
using UnityEngine.UI;

public class InventoryHUD : MonoBehaviour
{
    [Header("인벤토리 슬롯")]
    [SerializeField] private Image[] slots;

    [Header("스프라이트")]
    // private 형 > public 형
    [SerializeField] private Sprite scriptSprite; // 각본 아이템 스프라이트

    private ActorInteraction actorInteraction;

    private void Start()
    {
        ResetSlots();
    }

    private void Update()
    {
        if (actorInteraction == null)
        {
            var actors = FindObjectsByType<ActorController>(FindObjectsSortMode.None);
            foreach (var actor in actors)
            {
                if (actor.HasInputAuthority)
                {
                    actorInteraction = actor.GetComponent<ActorInteraction>();
                    break;
                }
            }
            return;
        }

        UpdateInventory();
    }

    private void UpdateInventory()
    {
        if (slots == null || slots.Length == 0) return;

        int scriptCount = actorInteraction.ScriptCount;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            if (i < scriptCount)
            {
                // 각본 있음 - 스프라이트 표시
                slots[i].sprite = scriptSprite;
                slots[i].color = Color.white;
            }
            else
            {
                // 빈 슬롯 - 투명 처리 (배경 패널이 보임)
                slots[i].sprite = null;
                slots[i].color = Color.clear;
            }
        }
    }

    private void ResetSlots()
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.sprite = null;
                slot.color = Color.clear;
            }
        }
    }
}