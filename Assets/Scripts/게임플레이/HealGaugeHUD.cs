using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class HealGaugeHUD : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject healGaugePanel;
    [SerializeField] private Slider healSlider;
    [SerializeField] private float maxHealTime = 30f;

    private ActorController localActor;

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
                    localActor = actor;
                    break;
                }
            }

            // 로컬 플레이어가 연기자가 아니면 패널 숨김
            if (localActor == null)
            {
                if (healGaugePanel != null) healGaugePanel.SetActive(false);
                return;
            }
        }

        // H 누르는 동안(자가치료) 또는 타인에게 치료받는 중에 패널 표시
        bool selfHealing = Input.GetKey(KeyCode.H) && localActor.IsInjury && !localActor.IsDead && !localActor.IsBeingHealed;
        bool beingHealed = localActor.IsBeingHealed && localActor.IsInjury && !localActor.IsDead;
        bool show = selfHealing || beingHealed;

        if (healGaugePanel != null) healGaugePanel.SetActive(show);
        // maxHealTime을 ActorController에서 직접 읽어서 게이지 비율 정확하게 표시
        float maxTime = localActor.selfHealTime;
        if (healSlider != null) healSlider.value = maxTime > 0f ? localActor.SelfHealTime / maxTime : 0f;
    }
}