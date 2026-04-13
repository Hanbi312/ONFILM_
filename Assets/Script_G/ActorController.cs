using Fusion;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkTransform))]
public class ActorController : NetworkBehaviour
{
    [Header("Movement")]
    public GameObject Cam;
    public float walkSpeed = 2.5f;
    public float runSpeed = 5.5f;
    public float sitSpeed = 1.35f;
    public float deadSpeed = 0.5f;
    public float injuryRunSpeed = 4.5f;
    public float injuryWalkSpeed = 2f;
    public float injurySitSpeed = 1f;

    [Header("Gravity")]
    public float gravity = -20f;
    public float groundedStick = -2f;

    [Header("Spawn Stabilizing")]
    public float spawnFreezeSeconds = 0.30f;

    [Header("Camera")]
    public Transform cameraRoot;
    public Camera playerCamera;
    public AudioListener audioListener;
    public float mouseSensitivity = 2.0f;
    public float pitchMin = -35f;
    public float pitchMax = 70f;
    public float cameraSmoothing = 30f;

    [Header("Status")]
    public float defaultHealth = 2f;

    [Header("치료 설정")]
    [SerializeField] public float selfHealTime = 30f;   // 자가치료 완료 시간 (초)
    [SerializeField] public float otherHealTime = 15f;  // 타인 치료 완료 시간 (초)

    [Header("Light")]
    public Light actorLight;

    [Networked] public float Health { get; set; }
    [Networked] public NetworkBool IsDead { get; set; }
    [Networked] public NetworkBool SelfHeal { get; set; }
    [Networked] public float SelfHealTime { get; set; }
    [Networked] public NetworkBool IsInjury { get; set; }
    // IsCarried와 NetIsCarried를 단일 변수로 통합 (NetIsCarried로 일원화)
    [Networked] public NetworkBool NetIsCarried { get; set; }
    // 하위 호환 프로퍼티 - 내부적으로 NetIsCarried를 읽고 씀
    public bool IsCarried
    {
        get => NetIsCarried;
        set { if (HasStateAuthority) NetIsCarried = value; }
    }
    [Networked] public NetworkBool IsLockedByVillain { get; set; }
    [Networked] public NetworkBool IsBeingHealed { get; set; } // 타인에게 치료받는 중
    [Networked] public NetworkBool IsHealingOther { get; set; } // 타인을 치료 중

    [Networked] private float NetYaw { get; set; }
    [Networked] private float NetPitch { get; set; }
    [Networked] private float YVelocity { get; set; }

    [Networked] private NetworkBool NetIsMoving { get; set; }
    [Networked] private NetworkBool NetIsWalking { get; set; }
    [Networked] private NetworkBool NetIsSitting { get; set; }
    [Networked] private NetworkBool NetIsSittingIdle { get; set; }  // 제자리 앉기
    [Networked] private NetworkBool NetIsRecovering { get; set; }
    [Networked] private NetworkBool NetIsVaulting { get; set; }
    [Networked] private NetworkBool NetIsInjury { get; set; }
    [Networked] private NetworkBool NetLightOn { get; set; }
    [Networked] private NetworkBool NetIsDeadMoving { get; set; }
    [Networked] private NetworkBool NetCrashWall { get; set; }   // 벽 충돌 동기화

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsSittingHash = Animator.StringToHash("IsSitting");
    private static readonly int IsSittingIdleHash = Animator.StringToHash("IsSittingIdle"); // 제자리 앉기
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int IsRecoveringHash = Animator.StringToHash("IsRecovering");
    private static readonly int IsVaultingHash = Animator.StringToHash("IsVaulting");
    private static readonly int IsInjuryHash = Animator.StringToHash("IsInjury");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int IsCarriedHash = Animator.StringToHash("IsCarried");
    private static readonly int IsDeadMovingHash = Animator.StringToHash("IsDeadMoving");
    private static readonly int HealHash = Animator.StringToHash("Heal");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int FailHash = Animator.StringToHash("Fail");
    private static readonly int BeingPickedUpHash = Animator.StringToHash("BeingPickedUp");
    private static readonly int BeingPutDownHash = Animator.StringToHash("BeingPutDown");
    private const string BreathingIdleState = "Breathing Idle";
    private const string DeathIdleState = "Death_Idle";

    private float localFreezeTimer = 0f;
    private bool isFrozen = true;
    private CharacterController cc;
    private Animator anim;

    // 미니게임 중 카메라 좌우 시야용 로컬 Yaw (몸통 회전과 분리)
    private float localCameraYaw = 0f;

    // 클라이언트 전용 로컬 카메라 회전값 (네트워크 변수 대신 사용)
    private float localYaw = 0f;
    private float localPitch = 0f;
    private bool localYawInitialized = false;
    public bool LocalIsHealingOther { get; set; } // 비네트워크 - 즉시 반영용 (ApplyAnimatorFromInput 전용)

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
    }

    public override void Spawned()
    {
        bool isLocal = HasInputAuthority;

        if (cc != null) cc.enabled = false;

        if (Cam != null) Cam.SetActive(isLocal);
        if (playerCamera != null) playerCamera.enabled = isLocal;
        if (audioListener != null) audioListener.enabled = isLocal;

        if (HasStateAuthority)
        {
            if (Health <= 0f)
                Health = defaultHealth;

            NetYaw = transform.eulerAngles.y;
            NetPitch = 0f;
            YVelocity = 0f;
            isFrozen = false;
            // 서버는 모든 플레이어의 cc를 활성화 (입력 처리 주체)
            if (cc != null) cc.enabled = true;
            NetLightOn = true;
        }
        else
        {
            isFrozen = false;
            if (cc != null) cc.enabled = false;
            // FixedUpdateNetwork 첫 실행 시 NetYaw로 초기화되도록 플래그 리셋
            localYawInitialized = false;
        }

        if (isLocal)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (isFrozen && HasStateAuthority)
        {
            localFreezeTimer += Time.deltaTime;
            if (localFreezeTimer >= spawnFreezeSeconds)
            {
                isFrozen = false;
                if (cc != null) cc.enabled = true;
            }
        }

        if (HasStateAuthority)
        {
            // 서버: 네트워크 변수 기반 애니메이션
            ApplyAnimator();
        }
        else if (HasInputAuthority)
        {
            // 클라이언트 본인: 로컬 입력 기반 애니메이션 (매 렌더 프레임)
            ApplyAnimatorFromInput();
        }
        else
        {
            // 다른 플레이어 프록시: 네트워크 변수 기반 애니메이션
            ApplyAnimator();
        }

        ApplyLight();
    }

    private void ApplyAnimator()
    {
        if (anim == null) return;
        if (Object == null || !Object.IsValid) return;

        bool isDeadAndStill = IsDead && !NetIsMoving && anim.GetCurrentAnimatorStateInfo(0).IsName(DeathIdleState);
        anim.speed = isDeadAndStill ? 0f : 1f;

        anim.SetBool(IsMovingHash,      NetIsMoving);
        anim.SetBool(IsWalkingHash,     NetIsWalking);
        anim.SetBool(IsSittingHash,     NetIsSitting);
        anim.SetBool(IsSittingIdleHash, NetIsSittingIdle);
        anim.SetBool(IsDeadHash,        IsDead);
        anim.SetBool(IsRecoveringHash,  NetIsRecovering);
        anim.SetBool(IsVaultingHash,    NetIsVaulting);
        anim.SetBool(IsInjuryHash,      NetIsInjury);
        anim.SetBool(IsCarriedHash,     NetIsCarried);
        anim.SetBool(IsDeadMovingHash,  NetIsDeadMoving);
    }

    private void ApplyLight()
    {
        if (Object == null || !Object.IsValid) return;
        if (actorLight != null)
            actorLight.enabled = NetLightOn;
    }

    public void SetLight(bool on)
    {
        if (!HasStateAuthority) return;
        NetLightOn = on;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority && !HasInputAuthority) return;
        if (isFrozen) return;
        if (!GetInput(out PlayerNetworkInput input)) return;

        if (HasStateAuthority)
        {
            // 서버: 실제 이동 + 네트워크 변수 업데이트
            HandleLook(input);
            MoveAndAnimate(input);
            HealthCheck(input);
        }
        else if (HasInputAuthority)
        {
            // 클라이언트: 카메라 회전만 (애니메이션은 Update의 ApplyAnimatorFromInput에서 처리)
            HandleLookLocal(input);
        }
    }

    private void LateUpdate()
    {
        if (HasInputAuthority && cameraRoot != null)
        {
            // 클라이언트는 localPitch, 서버(호스트)는 NetPitch 사용
            float pitch = HasStateAuthority ? NetPitch : localPitch;

            if (IsInMiniGame())
            {
                Quaternion targetRot = Quaternion.Euler(pitch, localCameraYaw, 0f);
                cameraRoot.localRotation = Quaternion.Lerp(cameraRoot.localRotation, targetRot, Time.deltaTime * cameraSmoothing);
            }
            else
            {
                Quaternion targetRot = Quaternion.Euler(pitch, 0f, 0f);
                cameraRoot.localRotation = Quaternion.Lerp(cameraRoot.localRotation, targetRot, Time.deltaTime * cameraSmoothing);
            }
        }
    }

    // 클라이언트 본인 - Update()에서 직접 Input 읽어 애니메이션 처리
    private void ApplyAnimatorFromInput()
    {
        if (anim == null) return;

        // KeySetting 기반으로 이동 키 읽기 (InputProvider와 동일한 키 사용)
        float h = 0f, v = 0f;
        if (KeySetting.keys.TryGetValue(KeyAction.LEFT,  out var kL) && Input.GetKey(kL)) h -= 1f;
        if (KeySetting.keys.TryGetValue(KeyAction.RIGHT, out var kR) && Input.GetKey(kR)) h += 1f;
        if (KeySetting.keys.TryGetValue(KeyAction.UP,    out var kU) && Input.GetKey(kU)) v += 1f;
        if (KeySetting.keys.TryGetValue(KeyAction.DOWN,  out var kD) && Input.GetKey(kD)) v -= 1f;

        bool isMoving  = new Vector2(h, v).sqrMagnitude > 0.001f;
        // WALK(LeftShift) = 눌리면 걷기(느리게), 안 눌리면 기본 뛰기
        bool walkPressed = KeySetting.keys.TryGetValue(KeyAction.WALK,  out var kW) && Input.GetKey(kW);
        bool sitPressed  = KeySetting.keys.TryGetValue(KeyAction.SIT,   out var kS) && Input.GetKey(kS);
        bool healPressed = KeySetting.keys.TryGetValue(KeyAction.HEAL,  out var kH) && Input.GetKey(kH);
        bool vaultPressed= KeySetting.keys.TryGetValue(KeyAction.VAULT, out var kV) && Input.GetKey(kV);

        bool isWalking     = isMoving && walkPressed && !IsDead;
        bool isSitting     = isMoving && sitPressed  && !IsDead;
        bool isSittingIdle = !isMoving && sitPressed && !IsDead;
        // 로컬 플레이어는 LocalIsHealingOther 사용 (네트워크 딜레이 없이 즉시 반영)
        bool isRecovering  = !IsDead && ((IsInjury && (healPressed || IsBeingHealed)) || LocalIsHealingOther);
        bool isVaulting    = NetCrashWall && vaultPressed;

        bool isDeadAndStill = IsDead && !isMoving && anim.GetCurrentAnimatorStateInfo(0).IsName(DeathIdleState);
        anim.speed = isDeadAndStill ? 0f : 1f;

        anim.SetBool(IsMovingHash,      isMoving);
        anim.SetBool(IsWalkingHash,     isWalking);
        anim.SetBool(IsSittingHash,     isSitting);
        anim.SetBool(IsSittingIdleHash, isSittingIdle);
        anim.SetBool(IsDeadHash,        IsDead);
        anim.SetBool(IsRecoveringHash,  isRecovering);
        anim.SetBool(IsVaultingHash,    isVaulting);
        anim.SetBool(IsInjuryHash,      IsInjury);
        anim.SetBool(IsCarriedHash,     NetIsCarried);
        anim.SetBool(IsDeadMovingHash,  IsDead && isMoving);
    }

    private ActorInteraction actorInteractionCache;

    private bool IsInMiniGame()
    {
        if (actorInteractionCache == null)
            actorInteractionCache = GetComponent<ActorInteraction>();
        return actorInteractionCache != null && actorInteractionCache.IsInMiniGame;
    }

    // 클라이언트 로컬 카메라 회전 (네트워크 변수 쓰지 않음)
    private void HandleLookLocal(PlayerNetworkInput input)
    {
        localPitch -= input.look.y * mouseSensitivity;
        localPitch = Mathf.Clamp(localPitch, pitchMin, pitchMax);

        if (IsInMiniGame())
        {
            localCameraYaw += input.look.x * mouseSensitivity;
            localCameraYaw = Mathf.Clamp(localCameraYaw, -80f, 80f);
        }
        // transform.rotation은 NetworkTransform이 서버 NetYaw로 동기화
        // 여기서 직접 세팅하면 localYaw 초기화 타이밍 문제로 방향이 틀어짐
    }

    // 클라이언트 로컬 애니메이션 파라미터 업데이트
    private void ApplyAnimatorLocal(PlayerNetworkInput input)
    {
        if (anim == null) return;

        Vector3 inputDir = new Vector3(input.move.x, 0f, input.move.y).normalized;
        bool runPressed = input.buttons.IsSet(PlayerNetworkInput.WALK);
        bool sitPressed = input.buttons.IsSet(PlayerNetworkInput.SIT);
        bool healPressed = input.buttons.IsSet(PlayerNetworkInput.HEAL);
        bool vaultPressed = input.buttons.IsSet(PlayerNetworkInput.VAULT);

        bool isMoving      = inputDir.sqrMagnitude > 0.001f;
        bool isWalking     = isMoving && runPressed  && !IsDead;
        bool isSitting     = isMoving && sitPressed  && !IsDead;
        bool isSittingIdle = !isMoving && sitPressed && !IsDead;  // 제자리 앉기
        bool isRecovering  = !IsDead && ((IsInjury && (healPressed || IsBeingHealed)) || IsHealingOther);
        bool isVaulting    = NetCrashWall && vaultPressed;

        anim.SetBool(IsMovingHash,      isMoving);
        anim.SetBool(IsWalkingHash,     isWalking);
        anim.SetBool(IsSittingHash,     isSitting);
        anim.SetBool(IsSittingIdleHash, isSittingIdle);
        anim.SetBool(IsDeadHash,        IsDead);
        anim.SetBool(IsRecoveringHash,  isRecovering);
        anim.SetBool(IsVaultingHash,    isVaulting);
        anim.SetBool(IsInjuryHash,      IsInjury);
        anim.SetBool(IsCarriedHash,     NetIsCarried);
        anim.SetBool(IsDeadMovingHash,  IsDead && isMoving);
    }

    private void HandleLook(PlayerNetworkInput input)
    {
        NetPitch -= input.look.y * mouseSensitivity;
        NetPitch = Mathf.Clamp(NetPitch, pitchMin, pitchMax);

        if (!IsInMiniGame())
        {
            NetYaw += input.look.x * mouseSensitivity;
            transform.rotation = Quaternion.Euler(0f, NetYaw, 0f);
            localCameraYaw = 0f;
        }
        else
        {
            localCameraYaw += input.look.x * mouseSensitivity;
            localCameraYaw = Mathf.Clamp(localCameraYaw, -80f, 80f);
        }
    }

    private void MoveAndAnimate(PlayerNetworkInput input)
    {
        if (cc == null || !cc.enabled) return;

        // 악역 카메라 미니게임 중 이동 잠금 (취소 불가)
        if (IsLockedByVillain)
        {
            NetIsMoving = false;
            NetIsWalking = false;
            NetIsSitting = false;
            return;
        }

        // 연기자용 미니게임 중 WASD 입력 시 미니게임 취소 후 이동
        if (IsInMiniGame())
        {
            bool wasdPressed = input.move.sqrMagnitude > 0.001f;
            if (wasdPressed)
            {
                if (actorInteractionCache != null && actorInteractionCache.IsInActorMiniGame)
                    actorInteractionCache.CancelActorMiniGame();
                else
                {
                    NetIsMoving = false;
                    NetIsWalking = false;
                    NetIsSitting = false;
                    return;
                }
            }
            else
            {
                NetIsMoving = false;
                NetIsWalking = false;
                NetIsSitting = false;
                return;
            }
        }

        Vector3 inputDir = new Vector3(input.move.x, 0f, input.move.y).normalized;

        bool runPressed = input.buttons.IsSet(PlayerNetworkInput.WALK);
        bool sitPressed = input.buttons.IsSet(PlayerNetworkInput.SIT);
        bool healPressed = input.buttons.IsSet(PlayerNetworkInput.HEAL);
        bool vaultPressed = input.buttons.IsSet(PlayerNetworkInput.VAULT);

        bool isMoving      = inputDir.sqrMagnitude > 0.001f;
        bool isWalking     = isMoving && runPressed  && !IsDead;
        bool isSitting     = isMoving && sitPressed  && !IsDead;
        bool isSittingIdle = !isMoving && sitPressed && !IsDead;  // 제자리 앉기
        bool isRecovering  = !IsDead && ((IsInjury && (healPressed || IsBeingHealed)) || IsHealingOther);
        bool isVaulting    = NetCrashWall && vaultPressed;

        float speed = 0f;
        if (isMoving) speed = runSpeed;
        if (isWalking) speed = walkSpeed;
        if (isSitting) speed = sitSpeed;
        if (isSittingIdle) speed = 0f;  // 제자리 앉기는 이동 없음
        if (IsInjury)
        {
            speed = injuryRunSpeed;
            if (runPressed) speed = injuryWalkSpeed;
            if (sitPressed) speed = injurySitSpeed;
        }
        if (IsDead) speed = deadSpeed;
        if (isRecovering) speed = 0f;

        Vector3 moveDir = (transform.forward * inputDir.z + transform.right * inputDir.x).normalized;

        if (cc.isGrounded && YVelocity < 0f) YVelocity = groundedStick;
        YVelocity += gravity * Runner.DeltaTime;
        cc.Move((moveDir * speed + Vector3.up * YVelocity) * Runner.DeltaTime);

        NetIsMoving      = isMoving;
        NetIsWalking     = isWalking;
        NetIsSitting     = isSitting;
        NetIsSittingIdle = isSittingIdle;
        NetIsRecovering  = isRecovering;
        NetIsVaulting    = isVaulting;
        NetIsInjury      = IsInjury;
        NetIsDeadMoving  = IsDead && isMoving;
    }

    private void HealthCheck(PlayerNetworkInput input)
    {
        if (Health <= 0f && !IsDead) IsDead = true;

        bool healPressed = input.buttons.IsSet(PlayerNetworkInput.HEAL);

        // !SelfHeal 제거 - 이미 치료 중(SelfHeal=true)이어도 계속 진행 가능
        bool canHeal = IsInjury && !IsDead && !IsBeingHealed;

        if (healPressed && canHeal)
        {
            if (!SelfHeal) SelfHeal = true; // 처음 누를 때만 세팅
            SelfHealTime += Runner.DeltaTime;

            if (SelfHealTime >= selfHealTime)
            {
                SelfHealTime = 0f;
                Health = defaultHealth;
                SelfHeal = false;
                RPC_PlayHeal();
            }
        }
        else
        {
            // H 손 떼거나 치료 불가 상태 → 진행도는 유지, 플래그만 리셋
            if (SelfHeal)
                SelfHeal = false;
        }

        IsInjury = Health <= 1f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;
        if (other.CompareTag("weapon"))
        {
            // WeaponFollower의 ownerKiller로 직접 참조 (별도 NetworkObject라 GetComponentInParent 불가)
            KillerController killer = null;
            var follower = other.GetComponent<WeaponFollower>();
            if (follower != null)
                killer = follower.ownerKiller;

            // 공격 중이 아니면 데미지 없음
            if (killer == null || !killer.IsAttacking) return;

            bool wasInjury = IsInjury;
            Health -= 1f;
            // 피격 시 자가치료 진행도 초기화
            SelfHeal = false;
            SelfHealTime = 0f;

            if (wasInjury && Health <= 0f)
                RPC_PlayDeath();
            else
                RPC_PlayHit();
        }
        if (other.CompareTag("wall")) NetCrashWall = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!HasStateAuthority) return;
        if (other.CompareTag("wall")) NetCrashWall = false;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_PlayBeingPickedUp()
    {
        if (anim != null)
            anim.SetTrigger(BeingPickedUpHash);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_PlayBeingPutDown()
    {
        // 사용하지 않음 - 내려놓기 애니메이션 제거
    }

    // 내려놓일 때 바로 Death_Idle로 복귀
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ReturnToDeathIdle()
    {
        if (anim == null) return;
        anim.ResetTrigger(BeingPickedUpHash);
        anim.ResetTrigger(BeingPutDownHash);
        anim.Play(DeathIdleState);
        Debug.Log("[ActorController] 내려놓기 → Death_Idle 복귀");
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_PlayEmotion(string trigger)
    {
        if (anim != null && !string.IsNullOrEmpty(trigger))
            anim.SetTrigger(trigger);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_ReturnToIdle()
    {
        if (anim == null) return;
        anim.ResetTrigger(HitHash);
        anim.Play(BreathingIdleState);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_PlayFail()
    {
        if (anim != null)
            anim.SetTrigger(FailHash);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ResumeAfterMigration()
    {
        isFrozen = false;
        if (cc != null) cc.enabled = HasInputAuthority || HasStateAuthority;
        Debug.Log($"[ActorController] Migration 후 재개 | {gameObject.name}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayHeal()
    {
        if (anim != null)
            anim.SetTrigger(HealHash);
        Debug.Log("[ActorController] 자가치료 애니메이션 재생");
    }

    // 타인이 치료 중일 때 서버에서 게이지 누적 호출
    // healAmount: 이번 프레임 누적량 (Runner.DeltaTime 기반)
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_HealByOther(float healAmount)
    {
        if (!IsInjury || IsDead) return;

        IsBeingHealed = true;
        SelfHealTime += healAmount;

        if (SelfHealTime >= selfHealTime)
        {
            SelfHealTime = 0f;
            Health = defaultHealth;
            IsBeingHealed = false;
            SelfHeal = false;
            RPC_PlayHeal();
            Debug.Log("[ActorController] 타인 치료 완료");
        }    }

    // 타인 치료 중단 시 호출
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StopHealByOther()
    {
        IsBeingHealed = false;
    }

    // 타인 치료 시작/중단 시 치료하는 사람의 애니메이션 플래그 세팅
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetHealingOther(bool healing)
    {
        IsHealingOther = healing;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayHit()
    {
        if (anim != null)
            anim.SetTrigger(HitHash);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayDeath()
    {
        if (anim != null)
            anim.SetTrigger(DeathHash);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_NotifyFilmPickedUp()
    {
        GameStateManager.Instance?.OnFilmPickedUp();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_PickUpFilm(NetworkId filmId)
    {
        if (Runner.TryFindObject(filmId, out var obj))
            obj.GetComponent<FilmItem>()?.PickUp();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_PickUpScript(NetworkId scriptId)
    {
        if (Runner.TryFindObject(scriptId, out var obj))
            obj.GetComponent<ScriptItem>()?.PickUp();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TurnOffCamera(NetworkId cameraId)
    {
        if (Runner.TryFindObject(cameraId, out var obj))
            obj.GetComponent<SecurityCamera>()?.TurnOff();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Respawn(Vector3 position, Quaternion rotation)
    {
        // 체력 초기화
        Health = defaultHealth;
        IsDead = false;
        IsInjury = false;
        NetIsCarried = false;  // IsCarried 통합 - NetIsCarried 하나만 세팅
        IsLockedByVillain = false;
        SelfHeal = false;
        SelfHealTime = 0f;

        // 위치 이동
        if (cc != null) cc.enabled = false;
        transform.position = position;
        transform.rotation = rotation;
        NetYaw = rotation.eulerAngles.y;
        YVelocity = 0f;
        if (cc != null) cc.enabled = true;

        // 모든 클라이언트에 Idle 복귀
        RPC_RespawnAnimation();

        Debug.Log($"[ActorController] 리스폰 완료: {position} | 체력={Health}");
    }

    // 악역 카메라 이동 잠금/해제 - 클라이언트에서 직접 [Networked] 변수 접근 대신 RPC 경유
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetLockedByVillain(bool locked)
    {
        IsLockedByVillain = locked;
        Debug.Log($"[ActorController] 악역 카메라 이동 잠금: {locked}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RespawnAnimation()
    {
        if (anim == null) return;
        anim.Rebind();
        anim.Update(0f);
        anim.Play(BreathingIdleState);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Teleport(Vector3 position, Quaternion rotation)
    {
        if (cc != null) cc.enabled = false;
        transform.position = position;
        transform.rotation = rotation;
        NetYaw = rotation.eulerAngles.y;
        YVelocity = 0f;
        if (cc != null) cc.enabled = true;
        Debug.Log($"[ActorController] RPC_Teleport 완료: {position}");
    }
}