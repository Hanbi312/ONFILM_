using UnityEngine;

/// <summary>
/// 게임 실행 시 최초 1회 키 설정을 기본값으로 초기화
/// 가장 먼저 로드되는 씬(로그인/메인)에 빈 오브젝트에 붙여두면 됨
/// </summary>
public class KeyResetOnBoot : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetKeysOnBoot()
    {
        // Key_ 관련 PlayerPrefs만 삭제 (다른 저장 데이터는 유지)
        foreach (KeyAction action in System.Enum.GetValues(typeof(KeyAction)))
        {
            if (action == KeyAction.KEYCOUNT) continue;
            PlayerPrefs.DeleteKey("Key_" + action.ToString());
        }
        PlayerPrefs.Save();

        // KeySetting 딕셔너리도 즉시 기본값으로 채우기
        KeySetting.keys.Clear();
        KeyCode[] defaultKeys = new KeyCode[]
        {
            KeyCode.W,            // UP
            KeyCode.S,            // DOWN
            KeyCode.A,            // LEFT
            KeyCode.D,            // RIGHT
            KeyCode.LeftShift,    // WALK
            KeyCode.LeftControl,  // SIT
            KeyCode.Mouse0,       // INTERACTION
            KeyCode.Mouse1,       // SKILL
            KeyCode.E,            // TRAITA
            KeyCode.R,            // TRAITB
            KeyCode.H,            // HEAL
            KeyCode.V,            // VAULT
        };

        for (int i = 0; i < (int)KeyAction.KEYCOUNT; i++)
            KeySetting.keys[(KeyAction)i] = defaultKeys[i];

        Debug.Log("[KeyResetOnBoot] 키 설정 기본값으로 초기화 완료");
    }
}
