using Fusion;
using UnityEngine;

/// <summary>
/// ScriptItem - 각본 아이템
/// 상태만 관리, 상호작용은 ActorInteraction에서 처리
/// </summary>
public class ScriptItem : NetworkBehaviour
{
    [Networked] public NetworkBool IsPickedUp { get; set; }

    private void Update()
    {
        if (Object == null || !Object.IsValid) return;
        if (IsPickedUp && gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    // 서버에서 호출
    public void PickUp()
    {
        if (!HasStateAuthority) return;
        if (IsPickedUp) return;
        IsPickedUp = true;
        GameStateManager.Instance?.OnScriptPickedUp();
        Debug.Log("[ScriptItem] 각본 획득 완료");
    }
}
