using System.Collections;
using Fusion;
using UnityEngine;

/// <summary>
/// ScriptItem - 각본 아이템
/// 상태만 관리, 상호작용은 ActorInteraction에서 처리
/// </summary>
public class ScriptItem : NetworkBehaviour
{
    [Networked] public NetworkBool IsPickedUp { get; set; }

    [Header("줍기 애니메이션")]
    [SerializeField] private Animator itemAnimator;
    [SerializeField] private string pickUpTrigger = "PickUp"; // Animator 트리거 이름

    private bool isPlayingAnim = false;

    private void Update()
    {
        if (Object == null || !Object.IsValid) return;

        // IsPickedUp이 true가 되면 모든 클라이언트에서 애니메이션 재생 후 비활성화
        if (IsPickedUp && !isPlayingAnim && gameObject.activeSelf)
        {
            isPlayingAnim = true;
            StartCoroutine(PlayPickUpAndDisable());
        }
    }

    private IEnumerator PlayPickUpAndDisable()
    {
        if (itemAnimator != null)
        {
            itemAnimator.SetTrigger(pickUpTrigger);

            // 전이(transition)가 완료될 때까지 최대 10프레임 대기
            int waitFrames = 0;
            string triggerStateName = pickUpTrigger; // 트리거명과 스테이트명이 동일하다고 가정
            while (waitFrames < 10)
            {
                yield return null;
                waitFrames++;
                var info = itemAnimator.GetCurrentAnimatorStateInfo(0);
                // 전이가 끝나고 목표 스테이트에 진입했으면 length 유효
                if (!itemAnimator.IsInTransition(0) && info.length > 0f)
                {
                    yield return new WaitForSeconds(info.length);
                    break;
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ScriptItem] Animator 없음 - 바로 비활성화 | {gameObject.name}");
        }

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