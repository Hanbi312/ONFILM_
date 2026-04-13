using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class NoDragScrollRect : ScrollRect
{
    public override void OnDrag(PointerEventData eventData)
    {
        // 드래그 무시
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        // 드래그 시작 무시
    }
}