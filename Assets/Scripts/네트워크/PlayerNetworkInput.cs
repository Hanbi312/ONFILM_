using Fusion;
using UnityEngine;

// Fusion이 네트워크로 주고받을 입력 데이터
public struct PlayerNetworkInput : INetworkInput
{
    public Vector2 move;     // WASD
    public Vector2 look;     // 마우스 이동
    public float   lookYaw;  // 킬러 절대 Yaw (몸통 회전용)
    public NetworkButtons buttons;

    // 버튼 인덱스
    public const int WALK = 0;
    public const int SIT = 1;
    public const int HEAL = 2;
    public const int VAULT = 3;
    public const int ATTACK = 4; // 킬러 좌클릭 공격
}