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

    [Header("Status")]
    public float defaultHealth = 2f;

    [Header("Light")]
    public Light actorLight;

    [Networked] public float Health { get; set; }
    [Networked] public NetworkBool IsDead { get; set; }
    [Networked] public NetworkBool SelfHeal { get; set; }
    [Networked] public float SelfHealTime { get; set; }
    [Networked] public NetworkBool IsInjury { get; set; }
    [Networked] public NetworkBool IsCarried { get; set; }
    [Networked] public NetworkBool NetIsCarried { get; set; }
    [Networked] public NetworkBool IsLockedByVillain { get; set; } // 악역 카메라 미니게임 중 이동 잠금

    [Networked] private float NetYaw { get; set; }
    [Networked] private float NetPitch { get; set; }
    [Networked] private float YVelocity { get; set; }

    [Networked] private NetworkBool NetIsMoving { get; set; }
    [Networked] private NetworkBool NetIsWalking { get; set; }
    [Networked] private NetworkBool NetIsSitting { get; set; }
    [Networked] private NetworkBool NetIsRecovering { get; set; }
    [Networked] private NetworkBool NetIsVaulting { get; set; }
    [Networked] private NetworkBool NetIsInjury { get; set; }
    [Networked] private NetworkBool NetLightOn { get; set; }

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsSittingHash = Animator.StringToHash("IsSitting");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int IsRecoveringHash = Animator.StringToHash("IsRecovering");
    private static readonly int IsVaultingHash = Animator.StringToHash("IsVaulting");
    private static readonly int IsInjuryHash = Animator.StringToHash("IsInjury");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int IsCarriedHash = Animator.StringToHash("IsCarried");

    private float localFreezeTimer = 0f;
    private bool isFrozen = true;
    private CharacterController cc;
    private Animator anim;
    private bool crashWall;

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
            Health = defaultHealth;
            NetYaw = transform.eulerAngles.y;
            NetPitch = 0f;
            YVelocity = 0f;
            isFrozen = true;
            localFreezeTimer = 0f;
            NetLightOn = true;
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

        bool isDeadAndStill = IsDead && !NetIsMoving && anim.GetCurrentAnimatorStateInfo(0).IsName("Death_Idle");
        anim.speed = isDeadAndStill ? 0f : 1f;

        anim.SetBool(IsMovingHash, NetIsMoving);
        anim.SetBool(IsWalkingHash, NetIsWalking);
        anim.SetBool(IsSittingHash, NetIsSitting);
        anim.SetBool(IsDeadHash, IsDead);
        anim.SetBool(IsRecoveringHash, NetIsRecovering);
        anim.SetBool(IsVaultingHash, NetIsVaulting);
        anim.SetBool(IsInjuryHash, NetIsInjury);
        anim.SetBool(IsCarriedHash, NetIsCarried);
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
        if (!HasStateAuthority) return;
        if (isFrozen) return;
        if (!GetInput(out PlayerNetworkInput input)) return;

        HandleLook(input);
        MoveAndAnimate(input);
        HealthCheck(input);
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

    private void MoveAndAnimate(PlayerNetworkInput input)
    {
        if (cc == null || !cc.enabled) return;

        // 악역 카메라 미니게임 중 이동 잠금
        if (IsLockedByVillain)
        {
            NetIsMoving = false;
            NetIsWalking = false;
            NetIsSitting = false;
            return;
        }

        Vector3 inputDir = new Vector3(input.move.x, 0f, input.move.y).normalized;

        bool runPressed = input.buttons.IsSet(PlayerNetworkInput.WALK);
        bool sitPressed = input.buttons.IsSet(PlayerNetworkInput.SIT);
        bool healPressed = input.buttons.IsSet(PlayerNetworkInput.HEAL);
        bool vaultPressed = input.buttons.IsSet(PlayerNetworkInput.VAULT);

        bool isMoving = inputDir.sqrMagnitude > 0.001f;
        bool isWalking = isMoving && runPressed && !IsDead;
        bool isSitting = isMoving && sitPressed && !IsDead;
        bool isRecovering = SelfHealTime > 0f && healPressed;
        bool isVaulting = crashWall && vaultPressed;

        float speed = 0f;
        if (isMoving) speed = runSpeed;
        if (isWalking) speed = walkSpeed;
        if (isSitting) speed = sitSpeed;
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

        NetIsMoving = isMoving;
        NetIsWalking = isWalking;
        NetIsSitting = isSitting;
        NetIsRecovering = isRecovering;
        NetIsVaulting = isVaulting;
        NetIsInjury = IsInjury;
    }

    private void HealthCheck(PlayerNetworkInput input)
    {
        if (Health <= 0f && !IsDead) IsDead = true;

        bool healPressed = input.buttons.IsSet(PlayerNetworkInput.HEAL);

        if (IsDead && !SelfHeal && healPressed)
        {
            SelfHealTime += Runner.DeltaTime;
            if (SelfHealTime >= 5f)
            {
                SelfHeal = true;
                SelfHealTime = 0f;
                Health += 1f;
                IsDead = false;
            }
        }
        else if (!healPressed)
        {
            SelfHealTime = 0f;
        }

        IsInjury = Health <= 1f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;
        if (other.CompareTag("weapon"))
        {
            bool wasInjury = IsInjury;
            Health -= 1f;

            if (wasInjury && Health <= 0f)
                RPC_PlayDeath();
            else
                RPC_PlayHit();
        }
        if (other.CompareTag("wall")) crashWall = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!HasStateAuthority) return;
        if (other.CompareTag("wall")) crashWall = false;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_PlayBeingPickedUp()
    {
        if (anim != null)
            anim.SetTrigger("BeingPickedUp");
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_PlayBeingPutDown()
    {
        if (anim != null)
            anim.SetTrigger("BeingPutDown");
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
        anim.ResetTrigger("Hit");
        anim.Play("Breathing Idle");
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_PlayFail()
    {
        if (anim != null)
            anim.SetTrigger("Fail");
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
            anim.SetTrigger("Death");
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
        IsCarried = false;
        NetIsCarried = false;
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RespawnAnimation()
    {
        if (anim == null) return;
        anim.Rebind();        // 모든 파라미터 초기화
        anim.Update(0f);      // 즉시 반영
        anim.Play("Breathing Idle");
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