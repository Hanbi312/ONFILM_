using Fusion;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkTransform))]
public class KillerController : NetworkBehaviour
{
    // ─── Inspector ────────────────────────────────────────────
    [Header("Movement")]
    public float runSpeed  = 5.5f;
    public float walkSpeed = 2.5f;

    [Header("Gravity")]
    public float gravity       = -20f;
    public float groundedStick = -2f;

    [Header("Camera")]
    public GameObject    Cam;
    public Transform     cameraRoot;
    public Camera        playerCamera;
    public AudioListener audioListener;
    public float mouseSensitivity = 2.0f;
    public float pitchMin         = -35f;
    public float pitchMax         =  70f;
    public float cameraSmoothing  = 30f;

    [Header("Combat")]
    public float attackCooldown = 0.6f;
    public float attackLockTime = 0.35f;

    [Header("Carry")]
    public Vector3 carryOffset = new Vector3(0f, 0.5f, 0.8f);

    [Header("Light")]
    public Light killerLight;

    // ─── Networked ────────────────────────────────────────────
    [Networked] private float              NetYaw          { get; set; }
    [Networked] private float              NetPitch        { get; set; }
    [Networked] private float              YVelocity       { get; set; }
    [Networked] public  NetworkBool        IsAttacking     { get; set; }
    [Networked] private TickTimer          AttackCooldown  { get; set; }
    [Networked] private TickTimer          AttackLock      { get; set; }
    [Networked] public  NetworkBool        NetIsCarrying   { get; set; }
    [Networked] private NetworkBool        NetIsMoving     { get; set; }
    [Networked] private NetworkBool        NetDoAttack     { get; set; }
    [Networked] private NetworkString<_32> NetAttackTrigger{ get; set; }
    [Networked] private NetworkBool        NetLightOn      { get; set; }

    // ─── Private ──────────────────────────────────────────────
    private CharacterController cc;
    private Animator            anim;
    private NetworkTransform    netTransform;
    private ActorController     carriedActor;
    private bool                isFrozen;
    private float               localPitch;
    private bool                localPitchReady;
    private bool                lastAttackState;

    // 로컬 플레이어 악역 참조 (FusionLobbyManager에서 카메라 Yaw 전송용)
    public static KillerController LocalKiller { get; private set; }

    private static readonly int HashIsMoving  = Animator.StringToHash("IsMoving");
    private static readonly int HashIsCarrying= Animator.StringToHash("IsCarrying");
    private static readonly int HashPickup    = Animator.StringToHash("Pickup");

    // ═══════════════════════════════════════════════════════════
    // 생명주기
    // ═══════════════════════════════════════════════════════════
    private void Awake()
    {
        cc           = GetComponent<CharacterController>();
        anim         = GetComponentInChildren<Animator>();
        netTransform = GetComponent<NetworkTransform>();
        if (anim != null) anim.applyRootMotion = false;
    }

    public override void Spawned()
    {
        bool isLocal = HasInputAuthority;
        cc.enabled = false;

        if (Cam          != null) Cam.SetActive(isLocal);
        if (playerCamera != null) playerCamera.enabled  = isLocal;
        if (audioListener!= null) audioListener.enabled = isLocal;

        // 로컬 악역 등록 (카메라 Yaw 전송용)
        if (isLocal) LocalKiller = this;

        if (HasStateAuthority)
        {
            NetYaw           = transform.eulerAngles.y;
            NetPitch         = 0f;
            YVelocity        = 0f;
            isFrozen         = false;
            NetLightOn       = true;
            NetAttackTrigger = "Attack_shorts";
            cc.enabled       = true;
        }
        else
        {
            isFrozen        = false;
            localPitchReady = false;
        }

        // 로컬 플레이어: cameraRoot Y 회전 초기화 (프리팹 베이크값 제거)
        if (isLocal && cameraRoot != null)
            cameraRoot.localRotation = Quaternion.Euler(0f, 0f, 0f);

        if (isLocal)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
    }

    private void Update()
    {
        // 클라이언트: localPitch 초기 동기화
        if (HasInputAuthority && !HasStateAuthority
            && !localPitchReady && Object != null && Object.IsValid)
        {
            localPitch      = NetPitch;
            localPitchReady = true;
        }

        // 클라이언트: 마우스 Y 로컬 즉시 반영
        if (HasInputAuthority && !HasStateAuthority)
        {
            localPitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            localPitch  = Mathf.Clamp(localPitch, pitchMin, pitchMax);
        }

        UpdateAnimator();

        if (killerLight != null && Object != null && Object.IsValid)
            killerLight.enabled = NetLightOn;
    }

    private void LateUpdate()
    {
        if (!HasInputAuthority || cameraRoot == null) return;
        float pitch = HasStateAuthority ? NetPitch : localPitch;
        cameraRoot.localRotation = Quaternion.Lerp(
            cameraRoot.localRotation,
            Quaternion.Euler(pitch, 0f, 0f),
            Time.deltaTime * cameraSmoothing);
    }

    // ═══════════════════════════════════════════════════════════
    // 네트워크 업데이트 (서버 전용)
    // ═══════════════════════════════════════════════════════════
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (isFrozen) return;
        if (!GetInput(out PlayerNetworkInput input)) return;

        HandleLook(input);
        HandleMovement(input);
        HandleAttack(input);
        HandleCarryPosition();
    }

    // ─── 회전 ─────────────────────────────────────────────────
    private void HandleLook(PlayerNetworkInput input)
    {
        // 클라이언트가 추적한 절대 Yaw를 그대로 사용 → 누적 오차, oscillation 없음
        // lookYaw=0은 킬러 초기화 전이므로 기존 NetYaw 유지
        if (!Mathf.Approximately(input.lookYaw, 0f))
            NetYaw = input.lookYaw;

        float pitchDelta = Mathf.Clamp(input.look.y * mouseSensitivity, -20f, 20f);
        NetPitch -= pitchDelta;
        NetPitch  = Mathf.Clamp(NetPitch, pitchMin, pitchMax);
        transform.rotation = Quaternion.Euler(0f, NetYaw, 0f);
    }

    // ─── 이동 ─────────────────────────────────────────────────
    private void HandleMovement(PlayerNetworkInput input)
    {
        if (!cc.enabled) return;

        // ActorController와 동일하게 WASD 4방향 입력 처리
        Vector3 inputDir = new Vector3(input.move.x, 0f, input.move.y).normalized;
        Vector3 moveDir  = (transform.forward * inputDir.z + transform.right * inputDir.x).normalized;

        if (cc.isGrounded && YVelocity < 0f) YVelocity = groundedStick;
        YVelocity = Mathf.Max(YVelocity + gravity * Runner.DeltaTime, -30f);

        cc.Move((moveDir * (IsAttacking ? walkSpeed : runSpeed)
                + Vector3.up * YVelocity) * Runner.DeltaTime);

        NetIsMoving = inputDir.sqrMagnitude > 0.001f;
    }

    // ─── 공격 ─────────────────────────────────────────────────
    private void HandleAttack(PlayerNetworkInput input)
    {
        if (IsAttacking)
        {
            if (AttackLock.Expired(Runner)) { IsAttacking = false; NetDoAttack = false; }
            return;
        }

        if (!AttackCooldown.ExpiredOrNotRunning(Runner)) return;

        if (input.buttons.IsSet(PlayerNetworkInput.ATTACK))
        {
            IsAttacking    = true;
            NetDoAttack    = true;
            AttackLock     = TickTimer.CreateFromSeconds(Runner, attackLockTime);
            AttackCooldown = TickTimer.CreateFromSeconds(Runner, attackCooldown);
        }
    }

    // ─── 시체 위치 동기화 ─────────────────────────────────────
    private void HandleCarryPosition()
    {
        if (!NetIsCarrying || carriedActor == null) return;

        if (carriedActor.Object == null || !carriedActor.Object.IsValid)
        {
            carriedActor = null; NetIsCarrying = false; return;
        }

        var actorCc = carriedActor.GetComponent<CharacterController>();
        if (actorCc != null) actorCc.enabled = false;

        carriedActor.transform.position = transform.position
            + transform.TransformDirection(carryOffset);
        carriedActor.transform.rotation = transform.rotation;
    }

    // ─── 충돌 ─────────────────────────────────────────────────
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!HasStateAuthority) return;
        if (hit.gameObject.CompareTag("wall") && YVelocity > 0f)
            YVelocity = 0f;
    }

    // ═══════════════════════════════════════════════════════════
    // 애니메이터
    // ═══════════════════════════════════════════════════════════
    private void UpdateAnimator()
    {
        if (anim == null || Object == null || !Object.IsValid) return;

        anim.SetBool(HashIsMoving,   NetIsMoving);
        anim.SetBool(HashIsCarrying, NetIsCarrying);

        if (NetDoAttack && !lastAttackState)
        {
            string t = NetAttackTrigger.ToString();
            if (!string.IsNullOrEmpty(t)) anim.SetTrigger(t);
        }
        if (!NetDoAttack && lastAttackState)
        {
            string t = NetAttackTrigger.ToString();
            if (!string.IsNullOrEmpty(t)) anim.ResetTrigger(t);
        }
        lastAttackState = NetDoAttack;
    }

    // ═══════════════════════════════════════════════════════════
    // 외부 호출
    // ═══════════════════════════════════════════════════════════
    public void SetLight(bool on)          { if (HasStateAuthority) NetLightOn        = on; }
    public void SetAttackTrigger(string t) { if (HasStateAuthority) NetAttackTrigger  = t;  }

    // ═══════════════════════════════════════════════════════════
    // RPC
    // ═══════════════════════════════════════════════════════════
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_Teleport(Vector3 pos, Quaternion rot)
    {
        cc.enabled = false;
        if (netTransform != null) netTransform.Teleport(pos, rot);
        else { transform.position = pos; transform.rotation = rot; }

        if (HasStateAuthority) { NetYaw = rot.eulerAngles.y; NetPitch = 0f; YVelocity = 0f; }
        if (HasInputAuthority && !HasStateAuthority) localPitchReady = false;
        cc.enabled = HasStateAuthority;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SetDoorLocked(bool locked)
    {
        isFrozen = locked;
        if (HasStateAuthority && cc != null) cc.enabled = !locked;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_PlayDoorAnimation() { if (anim != null) anim.SetTrigger("OpenDoor"); }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_PlayEmotion(string trigger)
    { if (anim != null && !string.IsNullOrEmpty(trigger)) anim.SetTrigger(trigger); }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_ReturnToIdle()
    {
        if (anim == null) return;
        string t = NetAttackTrigger.ToString();
        if (!string.IsNullOrEmpty(t)) anim.ResetTrigger(t);
        anim.Play("Breathing Idle");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_EquipWeapon(NetworkId weaponId)
    {
        if (!Runner.TryFindObject(weaponId, out var obj)) return;
        var follower = obj.GetComponent<WeaponFollower>()
                    ?? obj.gameObject.AddComponent<WeaponFollower>();
        follower.target      = FindBone(transform, "WeaponHolder");
        follower.ownerKiller = this;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StartCarry(NetworkId actorId)
    {
        if (!Runner.TryFindObject(actorId, out var obj)) return;
        var actor = obj.GetComponent<ActorController>();
        if (actor == null || !actor.IsDead) return;

        NetIsCarrying = true; actor.IsCarried = true; actor.NetIsCarried = true;
        carriedActor  = actor;

        var actorCc = actor.GetComponent<CharacterController>();
        if (actorCc != null) actorCc.enabled = false;

        RPC_SyncCarry(actorId, true);
        RPC_PlayPickup();
        actor.RPC_PlayBeingPickedUp();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StopCarry(NetworkId actorId)
    {
        if (!Runner.TryFindObject(actorId, out var obj)) return;
        var actor = obj.GetComponent<ActorController>();
        if (actor == null) return;

        NetIsCarrying = false; actor.IsCarried = false; actor.NetIsCarried = false;
        carriedActor  = null;

        var actorCc = actor.GetComponent<CharacterController>();
        if (actorCc != null) actorCc.enabled = !actor.IsDead;

        RPC_SyncCarry(actorId, false);
        actor.RPC_ReturnToDeathIdle();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayPickup() { if (anim != null) anim.SetTrigger(HashPickup); }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncCarry(NetworkId actorId, bool carrying)
    {
        NetIsCarrying = carrying;
        if (!Runner.TryFindObject(actorId, out var obj)) return;
        var actor = obj.GetComponent<ActorController>();
        if (actor != null) actor.NetIsCarried = carrying;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ResumeAfterMigration()
    {
        isFrozen = false;
        if (cc != null) cc.enabled = HasInputAuthority || HasStateAuthority;
        if (HasInputAuthority && !HasStateAuthority) localPitchReady = false;
        Debug.Log($"[KillerController] Migration 후 재개 | {gameObject.name}");
    }

    // ═══════════════════════════════════════════════════════════
    // 유틸
    // ═══════════════════════════════════════════════════════════
    private Transform FindBone(Transform root, string boneName)
    {
        if (root.name == boneName) return root;
        foreach (Transform child in root)
        {
            var found = FindBone(child, boneName);
            if (found != null) return found;
        }
        return null;
    }
}