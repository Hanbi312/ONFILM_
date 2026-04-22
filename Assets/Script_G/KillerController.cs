using Fusion;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkTransform))]
public class KillerController : NetworkBehaviour
{
    [Header("Movement")]
    public GameObject Cam;
    public float walkSpeed = 2.5f;
    public float runSpeed = 5.5f;

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

    [Header("Combat")]
    public float attackCooldown = 0.6f;
    public float attackLockTime = 0.35f;

    [Header("Light")]
    public Light killerLight;

    [Header("Carry")]
    [SerializeField] public Vector3 carryOffset = new Vector3(0f, 0.5f, 0.8f);

    [Networked] private float NetYaw { get; set; }
    [Networked] private float NetPitch { get; set; }
    [Networked] private float YVelocity { get; set; }

    [Networked] public NetworkBool IsAttacking { get; set; }
    [Networked] private TickTimer AttackCooldownTimer { get; set; }
    [Networked] private TickTimer AttackLockTimer { get; set; }

    [Networked] private NetworkBool NetLightOn { get; set; }
    [Networked] private NetworkBool NetIsCarrying { get; set; }

    [Networked] private NetworkBool NetIsMoving { get; set; }
    [Networked] private NetworkBool NetIsWalking { get; set; }
    [Networked] private NetworkBool NetDoAttack { get; set; }
    [Networked] private NetworkString<_32> NetAttackTrigger { get; set; }

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsCarryingHash = Animator.StringToHash("IsCarrying");

    private float localFreezeTimer = 0f;
    private bool isFrozen = true;
    private bool lastAttackState = false;
    private CharacterController cc;
    private Animator anim;
    private NetworkTransform netTransform;
    private ActorController carriedActorRef = null;

    // 클라이언트 전용 로컬 카메라 회전값 (네트워크 변수 대신 사용)
    private float localYaw = 0f;
    private float localPitch = 0f;
    private bool localYawInitialized = false;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
        netTransform = GetComponent<NetworkTransform>();
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
            NetYaw = transform.eulerAngles.y;
            NetPitch = 0f;
            YVelocity = 0f;
            isFrozen = false;
            if (cc != null) cc.enabled = true;
            NetLightOn = true;
            NetAttackTrigger = "Attack";
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
            // 클라이언트 본인: 로컬 입력 기반 애니메이션 (즉시 반영)
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

        anim.SetBool(IsMovingHash, NetIsMoving);
        anim.SetBool(IsWalkingHash, NetIsWalking);
        anim.SetBool(IsCarryingHash, NetIsCarrying);

        if (NetDoAttack && !lastAttackState)
        {
            string trigger = NetAttackTrigger.ToString();
            if (!string.IsNullOrEmpty(trigger))
                anim.SetTrigger(trigger);
        }

        if (!NetDoAttack && lastAttackState)
        {
            string trigger = NetAttackTrigger.ToString();
            if (!string.IsNullOrEmpty(trigger))
                anim.ResetTrigger(trigger);
        }

        lastAttackState = NetDoAttack;
    }

    private void ApplyLight()
    {
        if (Object == null || !Object.IsValid) return;
        if (killerLight != null)
            killerLight.enabled = NetLightOn;
    }

    public void SetLight(bool on)
    {
        if (!HasStateAuthority) return;
        NetLightOn = on;
    }

    public void SetAttackTrigger(string trigger)
    {
        if (!HasStateAuthority) return;
        NetAttackTrigger = trigger;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority && !HasInputAuthority) return;
        if (isFrozen) return;
        if (!GetInput(out PlayerNetworkInput input)) return;

        if (HasStateAuthority)
        {
            HandleLook(input);
            HandleAttack(input);
            MoveAndAnimate(input);

            if (NetIsCarrying && carriedActorRef != null)
            {
                var actorCc = carriedActorRef.GetComponent<CharacterController>();
                if (actorCc != null) actorCc.enabled = false;
                carriedActorRef.transform.position = transform.position + transform.TransformDirection(carryOffset);
                carriedActorRef.transform.rotation = transform.rotation;
            }
            else if (!NetIsCarrying && carriedActorRef != null)
            {
                var actorCc = carriedActorRef.GetComponent<CharacterController>();
                if (actorCc != null) actorCc.enabled = true;
                carriedActorRef = null;
            }
        }
        else if (HasInputAuthority)
        {
            // 클라이언트 본인: 로컬 카메라 회전만 처리 (이동은 서버가 담당)
            HandleLookLocal(input);
        }
    }

    private void LateUpdate()
    {
        if (HasInputAuthority && cameraRoot != null)
        {
            // 클라이언트는 localPitch, 서버(호스트)는 NetPitch 사용
            float pitch = HasStateAuthority ? NetPitch : localPitch;
            Quaternion targetRot = Quaternion.Euler(pitch, 0f, 0f);
            cameraRoot.localRotation = Quaternion.Lerp(cameraRoot.localRotation, targetRot, Time.deltaTime * cameraSmoothing);
        }
    }

    private void HandleLook(PlayerNetworkInput input)
    {
        NetYaw += input.look.x * mouseSensitivity;
        NetPitch -= input.look.y * mouseSensitivity;
        NetPitch = Mathf.Clamp(NetPitch, pitchMin, pitchMax);
        transform.rotation = Quaternion.Euler(0f, NetYaw, 0f);
    }

    // 클라이언트 로컬 카메라 회전 (네트워크 변수 쓰지 않음 → pitch만 즉시 반영)
    private void HandleLookLocal(PlayerNetworkInput input)
    {
        // 최초 1회: 서버의 NetYaw/NetPitch로 로컬 값 동기화
        if (!localYawInitialized)
        {
            localYaw = NetYaw;
            localPitch = NetPitch;
            localYawInitialized = true;
        }

        localPitch -= input.look.y * mouseSensitivity;
        localPitch = Mathf.Clamp(localPitch, pitchMin, pitchMax);

        // 3인칭: transform.rotation(yaw)은 NetworkTransform이 서버 NetYaw로 동기화
        // 로컬에서 직접 세팅하면 서버 이동 방향과 시각적 몸통 방향이 어긋남
    }

    // 클라이언트 본인 - Update()에서 직접 Input 읽어 애니메이션 처리 (즉시 반영)
    private void ApplyAnimatorFromInput()
    {
        if (anim == null) return;
        if (Object == null || !Object.IsValid) return;

        // 문 상호작용 중 이동 애니메이션 차단 (서버의 MoveAndAnimate와 동기화)
        if (IsDoorLocked)
        {
            anim.SetBool(IsMovingHash, false);
            anim.SetBool(IsWalkingHash, false);
            anim.SetBool(IsCarryingHash, NetIsCarrying);
            return;
        }

        // KeySetting 기반으로 이동 키 읽기 (InputProvider와 동일한 키 사용)
        float h = 0f, v = 0f;
        if (KeySetting.keys.TryGetValue(KeyAction.LEFT,  out var kL) && Input.GetKey(kL)) h -= 1f;
        if (KeySetting.keys.TryGetValue(KeyAction.RIGHT, out var kR) && Input.GetKey(kR)) h += 1f;
        if (KeySetting.keys.TryGetValue(KeyAction.UP,    out var kU) && Input.GetKey(kU)) v += 1f;
        if (KeySetting.keys.TryGetValue(KeyAction.DOWN,  out var kD) && Input.GetKey(kD)) v -= 1f;

        bool isMoving = new Vector2(h, v).sqrMagnitude > 0.001f;

        anim.SetBool(IsMovingHash, isMoving);
        anim.SetBool(IsWalkingHash, false);
        anim.SetBool(IsCarryingHash, NetIsCarrying);

        // 공격 트리거는 네트워크 변수 기반 유지 (서버 판정이라 로컬 예측 불가)
        if (NetDoAttack && !lastAttackState)
        {
            string trigger = NetAttackTrigger.ToString();
            if (!string.IsNullOrEmpty(trigger))
                anim.SetTrigger(trigger);
        }

        if (!NetDoAttack && lastAttackState)
        {
            string trigger = NetAttackTrigger.ToString();
            if (!string.IsNullOrEmpty(trigger))
                anim.ResetTrigger(trigger);
        }

        lastAttackState = NetDoAttack;
    }

    private void HandleAttack(PlayerNetworkInput input)
    {
        bool attackPressed = input.buttons.IsSet(PlayerNetworkInput.ATTACK);

        if (IsAttacking)
        {
            if (AttackLockTimer.Expired(Runner))
            {
                IsAttacking = false;
                NetDoAttack = false;
            }
            return;
        }

        if (!AttackCooldownTimer.ExpiredOrNotRunning(Runner)) return;

        if (attackPressed)
        {
            IsAttacking = true;
            NetDoAttack = true;
            AttackLockTimer = TickTimer.CreateFromSeconds(Runner, attackLockTime);
            AttackCooldownTimer = TickTimer.CreateFromSeconds(Runner, attackCooldown);
        }
    }

    private void MoveAndAnimate(PlayerNetworkInput input)
    {
        if (cc == null || !cc.enabled) return;

        // 문 상호작용 중 이동 잠금
        if (IsDoorLocked)
        {
            NetIsMoving = false;
            NetIsWalking = false;
            ApplyGravityOnly();
            return;
        }

        Vector3 inputDir = new Vector3(input.move.x, 0f, input.move.y).normalized;
        bool isMoving = inputDir.sqrMagnitude > 0.001f;

        float speed = runSpeed;
        Vector3 moveDir = (transform.forward * inputDir.z + transform.right * inputDir.x).normalized;

        if (cc.isGrounded && YVelocity < 0f) YVelocity = groundedStick;
        YVelocity += gravity * Runner.DeltaTime;
        cc.Move((moveDir * speed + Vector3.up * YVelocity) * Runner.DeltaTime);

        NetIsMoving = isMoving;
        NetIsWalking = false;
    }

    private void ApplyGravityOnly()
    {
        if (cc == null || !cc.enabled) return;
        if (cc.isGrounded && YVelocity < 0f) YVelocity = groundedStick;
        YVelocity += gravity * Runner.DeltaTime;
        cc.Move(Vector3.up * YVelocity * Runner.DeltaTime);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ResumeAfterMigration()
    {
        isFrozen = false;
        if (cc != null) cc.enabled = HasInputAuthority || HasStateAuthority;
        Debug.Log($"[KillerController] Migration 후 재개 | {gameObject.name}");
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
        anim.ResetTrigger("OpenDoor");
        anim.Play("Breathing Idle");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_EquipWeapon(NetworkId weaponId)
    {
        if (!Runner.TryFindObject(weaponId, out var weaponObj)) return;

        var follower = weaponObj.GetComponent<WeaponFollower>();
        if (follower == null)
            follower = weaponObj.gameObject.AddComponent<WeaponFollower>();

        follower.target = FindBoneRecursive(transform, "hand.R");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StartCarry(NetworkId actorId)
    {
        if (!Runner.TryFindObject(actorId, out var actorObj)) return;
        var actor = actorObj.GetComponent<ActorController>();
        if (actor == null) return;

        if (!actor.IsDead) return;

        NetIsCarrying = true;
        actor.IsCarried = true;
        actor.NetIsCarried = true;
        carriedActorRef = actor;

        var actorCc = actor.GetComponent<CharacterController>();
        if (actorCc != null) actorCc.enabled = false;

        RPC_SyncCarry(actorId, true);
        RPC_PlayPickup();
        actor.RPC_PlayBeingPickedUp();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayPickup()
    {
        if (anim != null)
            anim.SetTrigger("Pickup");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StopCarry(NetworkId actorId)
    {
        if (!Runner.TryFindObject(actorId, out var actorObj)) return;
        var actor = actorObj.GetComponent<ActorController>();
        if (actor == null) return;

        NetIsCarrying = false;
        actor.IsCarried = false;
        actor.NetIsCarried = false;
        carriedActorRef = null;

        var actorCc = actor.GetComponent<CharacterController>();
        if (actorCc != null) actorCc.enabled = true;

        RPC_SyncCarry(actorId, false);
        // 내려놓기 애니메이션 없이 바로 Death_Idle로 복귀
        actor.RPC_ReturnToDeathIdle();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayPutDown()
    {
        // 사용하지 않음 - 내려놓기 애니메이션 제거
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncCarry(NetworkId actorId, bool carrying)
    {
        NetIsCarrying = carrying;
        if (!Runner.TryFindObject(actorId, out var actorObj)) return;
        var actor = actorObj.GetComponent<ActorController>();
        if (actor != null) actor.NetIsCarried = carrying;
    }

    private Transform FindBoneRecursive(Transform parent, string boneName)
    {
        if (parent.name == boneName) return parent;
        foreach (Transform child in parent)
        {
            var found = FindBoneRecursive(child, boneName);
            if (found != null) return found;
        }
        return null;
    }

    // 문 상호작용용 텔레포트
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_Teleport(Vector3 position, Quaternion rotation)
    {
        if (cc != null) cc.enabled = false;

        if (netTransform != null)
            netTransform.Teleport(position, rotation);
        else
        {
            transform.position = position;
            transform.rotation = rotation;
        }

        if (HasStateAuthority)
        {
            NetYaw = rotation.eulerAngles.y;
            NetPitch = 0f;
            YVelocity = 0f;
        }

        // 클라 본인: 다음 FixedUpdateNetwork에서 NetYaw로 localYaw를 재동기화
        if (HasInputAuthority && !HasStateAuthority)
            localYawInitialized = false;

        // 이동은 서버만 구동 → 클라는 cc 비활성 유지
        if (cc != null)
            cc.enabled = HasStateAuthority;

        Debug.Log($"[KillerController] RPC_Teleport 완료: {position}");
    }

    // 문 상호작용 중 이동 잠금
    [Networked] public NetworkBool IsDoorLocked { get; set; }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetDoorLocked(bool locked)
    {
        IsDoorLocked = locked;
        Debug.Log($"[KillerController] IsDoorLocked = {locked}");
    }

    // 문 열기 애니메이션
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_PlayDoorAnimation()
    {
        if (anim != null)
            anim.SetTrigger("OpenDoor");
        Debug.Log("[KillerController] 문 열기 애니메이션 재생");
    }
}