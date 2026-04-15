using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class HealGaugeHUD : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject healGaugePanel;
    [SerializeField] private Slider healSlider;
    [SerializeField] private float maxHealTime = 30f;

    private ActorController   localActor;
    private ActorInteraction  localInteraction; // 타인 치료 진행도 참조용

    private void Update()
    {
        // 로컬 연기자 찾기
        if (localActor == null)
        {
            var actors = FindObjectsByType<ActorController>(FindObjectsSortMode.None);
            foreach (var actor in actors)
            {
                if (actor.HasInputAuthority)
                {
                    localActor       = actor;
                    localInteraction = actor.GetComponent<ActorInteraction>();
                    break;
                }
            }

            if (localActor == null)
            {
                if (healGaugePanel != null) healGaugePanel.SetActive(false);
                return;
            }
        }

        // ── 표시 조건 ────────────────────────────────────────────────
        // 1) 자가치료: H키를 누르는 동안, 부상 상태, 살아있음, 타인 치료받는 중 아님
        bool selfHealing  = Input.GetKey(KeyCode.H)
                            && localActor.IsInjury
                            && !localActor.IsDead
                            && !localActor.IsBeingHealed;

        // 2) 타인에게 치료받는 중
        bool beingHealed  = localActor.IsBeingHealed
                            && localActor.IsInjury
                            && !localActor.IsDead;

        // 3) 타인을 치료해주는 중 (치료해주는 쪽도 게이지 표시)
        bool healingOther = localInteraction != null
                            && localInteraction.IsHealingOther
                            && localInteraction.HealTarget != null;

        bool show = selfHealing || beingHealed || healingOther;

        if (healGaugePanel != null) healGaugePanel.SetActive(show);

        if (!show)
        {
            if (healSlider != null) healSlider.value = 0f;
            return;
        }

        // ── 슬라이더 값 결정 ─────────────────────────────────────────
        float progress = 0f;

        if (healingOther)
        {
            // 치료해주는 쪽: 대상(HealTarget)의 누적 게이지를 기준으로 표시
            // 대상의 selfHealTime이 최댓값 기준 (RPC_HealByOther가 selfHealTime까지 누적)
            ActorController target = localInteraction.HealTarget;
            float maxTime = target.selfHealTime;
            progress = maxTime > 0f ? target.SelfHealTime / maxTime : 0f;
        }
        else
        {
            // 자가치료 / 타인에게 치료받는 중: 로컬 액터의 누적 게이지 사용
            float maxTime = localActor.selfHealTime;
            progress = maxTime > 0f ? localActor.SelfHealTime / maxTime : 0f;
        }

        if (healSlider != null) healSlider.value = progress;
    }
}