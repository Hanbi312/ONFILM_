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

    [Header("Combat")]
    public float attackCooldown = 0.6f;
    public float attackLockTime = 0.35f;

    [Header("Light")]
    public Light killerLight;

    [Networked] private float NetYaw { get; set; }
    [Networked] private float NetPitch { get; set; }
    [Networked] private float YVelocity { get; set; }

    [Networked] private NetworkBool IsAttacking { get; set; }
    [Networked] private TickTimer AttackCooldownTimer { get; set; }
    [Networked] private TickTimer AttackLockTimer { get; set; }

    [Networked] private NetworkBool NetLightOn { get; set; }

    [Networked] private NetworkBool NetIsMoving { get; set; }
    [Networked] private NetworkBool NetIsWalking { get; set; }
    [Networked] private NetworkBool NetDoAttack { get; set; }
    [Networked] private NetworkString<_32> NetAttackTrigger { get; set; } // 무기별 공격 트리거

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

    private float localFreezeTimer = 0f;
    private bool isFrozen = true;
    private bool lastAttackState = false;
    private CharacterController cc;
    private Animator anim;

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
            NetYaw = transform.eulerAngles.y;
            NetPitch = 0f;
            YVelocity = 0f;
            isFrozen = true;
            localFreezeTimer = 0f;
            NetLightOn = true;
            NetAttackTrigger = "Attack"; // 기본 트리거
        }
        else
        {
            isFrozen = false;
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

        ApplyAnimator();
        ApplyLight();
    }

    private void ApplyAnimator()
    {
        if (anim == null) return;
        if (Object == null || !Object.IsValid) return;

        anim.SetBool(IsMovingHash, NetIsMoving);
        anim.SetBool(IsWalkingHash, NetIsWalking);

        // 공격 트리거: false→true 엣지에서만 발동, 무기별 트리거 사용
        if (NetDoAttack && !lastAttackState)
        {
            string trigger = NetAttackTrigger.ToString();
            if (!string.IsNullOrEmpty(trigger))
                anim.SetTrigger(trigger);
        }

        // 공격 끝나면 트리거 리셋
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

    // 무기별 공격 트리거 설정 (서버에서 호출)
    public void SetAttackTrigger(string trigger)
    {
        if (!HasStateAuthority) return;
        NetAttackTrigger = trigger;
        Debug.Log($"[KillerController] 공격 트리거 설정: {trigger}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (isFrozen) return;
        if (!GetInput(out PlayerNetworkInput input)) return;

        HandleLook(input);
        HandleAttack(input);
        MoveAndAnimate(input);
    }

    private void LateUpdate()
    {
        if (HasInputAuthority && cameraRoot != null)
            cameraRoot.localRotation = Quaternion.Euler(NetPitch, 0f, 0f);
    }

    private void HandleLook(PlayerNetworkInput input)
    {
        NetYaw += input.look.x * mouseSensitivity;
        NetPitch -= input.look.y * mouseSensitivity;
        NetPitch = Mathf.Clamp(NetPitch, pitchMin, pitchMax);
        transform.rotation = Quaternion.Euler(0f, NetYaw, 0f);
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

        if (IsAttacking)
        {
            NetIsMoving = false;
            NetIsWalking = false;
            ApplyGravityOnly();
            return;
        }

        Vector3 inputDir = new Vector3(input.move.x, 0f, input.move.y).normalized;
        bool isMoving = inputDir.sqrMagnitude > 0.001f;
        bool isWalking = isMoving && input.buttons.IsSet(PlayerNetworkInput.WALK); // Shift: 느린 걷기

        float speed = isWalking ? walkSpeed : runSpeed;
        Vector3 moveDir = (transform.forward * inputDir.z + transform.right * inputDir.x).normalized;

        if (cc.isGrounded && YVelocity < 0f) YVelocity = groundedStick;
        YVelocity += gravity * Runner.DeltaTime;
        cc.Move((moveDir * speed + Vector3.up * YVelocity) * Runner.DeltaTime);

        NetIsMoving = isMoving;
        NetIsWalking = isWalking;
    }

    private void ApplyGravityOnly()
    {
        if (cc == null || !cc.enabled) return;
        if (cc.isGrounded && YVelocity < 0f) YVelocity = groundedStick;
        YVelocity += gravity * Runner.DeltaTime;
        cc.Move(Vector3.up * YVelocity * Runner.DeltaTime);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_EquipWeapon(NetworkId weaponId)
    {
        if (!Runner.TryFindObject(weaponId, out var weaponObj)) return;

        var follower = weaponObj.GetComponent<WeaponFollower>();
        if (follower == null)
            follower = weaponObj.gameObject.AddComponent<WeaponFollower>();

        follower.target = FindBoneRecursive(transform, "hand.R");
        Debug.Log($"[KillerController] RPC_EquipWeapon | target={follower.target?.name}");
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
}
