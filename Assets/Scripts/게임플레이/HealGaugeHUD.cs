using Fusion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HealGaugeHUD - 자가치료 게이지 UI
/// H키를 누르는 동안 게이지가 차고, 떼면 멈춤
/// </summary>
public class HealGaugeHUD : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject healGaugePanel; // 게이지 패널 (H 누를 때만 표시)
    [SerializeField] private Slider healSlider;          // 치료 게이지 슬라이더
    [SerializeField] private float maxHealTime = 5f;     // ActorController와 동일하게 설정

    private ActorController localActor;

    private void Update()
    {
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
            return;
        }

        bool isRecovering = localActor.IsDead && !localActor.SelfHeal && Input.GetKey(KeyCode.H);

        // 패널 표시/숨김 - 부상 상태에서 게이지가 차있을 때만 표시
        if (healGaugePanel != null)
            healGaugePanel.SetActive(localActor.IsInjury && !localActor.IsDead && !localActor.SelfHeal && localActor.SelfHealTime > 0f);

        // 게이지 업데이트
        if (healSlider != null)
            healSlider.value = localActor.SelfHealTime / maxHealTime;
    }
}
