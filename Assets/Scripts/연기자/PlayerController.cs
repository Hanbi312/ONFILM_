using UnityEngine;


[RequireComponent(typeof(CharacterController))] // CharacterController가 없으면 자동으로 추가

public class PlayerController : MonoBehaviour // Mirror 멀티플레이용 클래스
{
    // ---------------- Movement ----------------
    [Header("Movement")]

    public float walkSpeed = 2.5f;           // 걷기 속도 (Shift)
    public float runSpeed = 5.5f;          // 달리기 속도 (기본)
    public float sitSpeed = 1.35f;          // 앉기 이동 속도 (Ctrl)

    public float rotationSmoothTime = 0.08f; // 캐릭터 회전 부드러움

    public float targetSpeed = 5.5f;        // 현재 적용되는 이동 속도

    public float injurySpeed = 1f;          // 부상 상태 이동 속도 감소값
    public float deadSpeed = 0.5f;          // 사망 상태 이동 속도

    // ---------------- Gravity ----------------
    [Header("Gravity")]

    public float gravity = -20f;            // 중력 값
    public float groundedStick = -2f;       // 캐릭터를 바닥에 붙게 하는 값

    // ---------------- Camera ----------------
    [Header("Camera")]

    public Transform cameraRoot;            // 카메라 상하 회전 축 (CameraRoot)
    public Camera playerCamera;             // 플레이어 카메라
    public AudioListener audioListener;     // 오디오 리스너

    public float mouseSensitivity = 2.0f;   // 마우스 감도

    public float pitchMin = -35f;           // 카메라 상하 최소 각도
    public float pitchMax = 70f;            // 카메라 상하 최대 각도

    // ---------------- Internal ----------------

    private CharacterController cc;         // CharacterController 참조
    private Animator anim;                  // Animator 참조

    private float pitch;                    // 카메라 상하 회전 값
    private float yVelocity;                // 중력 계산용 수직 속도
    private float currentYawVelocity;       // 캐릭터 회전 스무딩 변수

    // Animator 파라미터 (Hash 사용 → 성능 + 오타 방지)
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsSittingHash = Animator.StringToHash("IsSitting");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int IsRecoveringHash = Animator.StringToHash("IsRecovering");

    // ---------------- Status ----------------
    [Header("Status")]

    public float Health = 2f;               // 플레이어 체력
    public bool IsDead = false;             // 사망 여부

    // ---------------- Dead ----------------
    [Header("Dead")]

    public bool selfHeal = false;           // 자가회복 사용 여부
    public float selfHeaTime = 0f;          // 자가회복 누적 시간
    public bool IsInjury = false;           // 부상 상태

    // ---------------- Awake ----------------

    void Awake()
    {
        cc = GetComponent<CharacterController>(); // CharacterController 가져오기
        anim = GetComponent<Animator>();           // Animator 가져오기
    }

    // ---------------- Local Player Start ----------------

    /*public override void OnStartLocalPlayer()
    {
        // 내 플레이어만 카메라 사용
        if (playerCamera != null)
            playerCamera.enabled = true;

        if (audioListener != null)
            audioListener.enabled = true;

        // 로비에서는 마우스 잠금 해제
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ---------------- Client Start ----------------

    public override void OnStartClient()
    {
        // 다른 플레이어는 카메라/오디오 끄기
        if (!isLocalPlayer)
        {
            if (playerCamera != null)
                playerCamera.enabled = false;

            if (audioListener != null)
                audioListener.enabled = false;
        }
    }

    // ---------------- Update ----------------

    void Update()
    {
        if (!isLocalPlayer) return;                // 내 플레이어만 입력 처리

        LookByMouse();                             // 카메라 회전
        MoveAndAnimate();                          // 이동 + 애니메이션

        if (Health <= 0f && !IsDead)               // 체력 0 이하 → 사망
        {
            Debug.Log("플레이어 사망");
            targetSpeed = 0f;
            IsDead = true;
        }

        if (IsDead && !selfHeal && Input.GetKey(KeyCode.H)) // 자가회복
        {
            selfHeaTime += Time.deltaTime;

            if (selfHeaTime >= 5f)                 // 5초 후 회복
            {
                selfHeal = true;
                selfHeaTime = 0f;
                Health += 1f;
                IsDead = false;
                targetSpeed = runSpeed;

                Debug.Log("자가 회복: 체력 +1");
            }
        }

        if (Health <= 1f)
            IsInjury = true;                       // 체력 1 이하 → 부상
    }*/

    // ---------------- Camera Control ----------------

    void LookByMouse()
    {
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity; // 마우스 Y 입력

        pitch -= mouseY;                                            // 카메라 회전값 변경
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);             // 각도 제한

        if (cameraRoot != null)
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f); // 상하 회전 적용
    }

    // ---------------- Movement ----------------

    void MoveAndAnimate()
    {
        float h = Input.GetAxisRaw("Horizontal");   // A D
        float v = Input.GetAxisRaw("Vertical");     // W S

        Vector3 inputDir = new Vector3(h, 0f, v).normalized; // 입력 방향 벡터

        bool isMoving = inputDir.sqrMagnitude > 0.001f;      // 이동 중 여부
        bool isWalking = isMoving && Input.GetKey(KeyCode.LeftShift);  // 달리기
        bool isSitting = isMoving && Input.GetKey(KeyCode.LeftControl); // 앉기

        bool isDead = IsDead;
        bool isRecovering = selfHeaTime > 0f && !isWalking;

        if (isWalking)
            targetSpeed = runSpeed;               // 달리기 속도
        else if (isSitting)
            targetSpeed = sitSpeed;                // 앉기 속도
        else
            targetSpeed = walkSpeed;                // 기본 이동

        if (Health <= 1f && !IsDead && IsInjury)
            targetSpeed -= injurySpeed;            // 부상 시 속도 감소

        if (IsDead)
            targetSpeed = deadSpeed;               // 사망 시 속도 감소

        float camYaw = (playerCamera != null) ? playerCamera.transform.eulerAngles.y : transform.eulerAngles.y; // 카메라 방향

        Vector3 moveDir = Quaternion.Euler(0f, camYaw, 0f) * inputDir; // 카메라 기준 이동

        if (moveDir.sqrMagnitude > 0.001f)
        {
            float targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg; // 목표 회전

            float smoothedYaw = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetYaw,
                ref currentYawVelocity,
                rotationSmoothTime);               // 부드러운 회전

            //transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
        }

        if (cc.isGrounded && yVelocity < 0f)
            yVelocity = groundedStick;             // 바닥에 붙이기

        yVelocity += gravity * Time.deltaTime;     // 중력 적용

        Vector3 velocity = moveDir * targetSpeed;  // 이동 속도 적용
        velocity.y = yVelocity;                    // 수직 속도 적용

        cc.Move(velocity * Time.deltaTime);        // 실제 이동

        if (anim != null)                          // 애니메이션 상태 전달
        {
            anim.SetBool(IsMovingHash, isMoving);
            anim.SetBool(IsWalkingHash, isWalking);
            anim.SetBool(IsSittingHash, isSitting);
            anim.SetBool(IsDeadHash, isDead);
            anim.SetBool(IsRecoveringHash, isRecovering);
        }
    }

    // ---------------- Collision ----------------

    /*void OnTriggerEnter(Collider other)
    {
        if (!isLocalPlayer) return;                // 내 플레이어만 처리

        if (other.CompareTag("weapon"))
            Hit();                                 // 무기 충돌 시 피격
    }

    // ---------------- Damage ----------------

    void Hit()
    {
        Debug.Log("피격 당함");
        Health -= 1f;                              // 체력 감소
    }*/
}