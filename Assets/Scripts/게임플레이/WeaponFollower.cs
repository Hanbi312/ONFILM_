using UnityEngine;

/// <summary>
/// WeaponFollower - hand.R 본을 찾아서 무기가 손에 붙어 움직이도록 함
/// </summary>
public class WeaponFollower : MonoBehaviour
{
    public Transform target; // hand.R 본
    public KillerController ownerKiller; // 이 무기를 소유한 킬러 (데미지 판정용)
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffset = Vector3.zero;

    private void Start()
    {
        // target이 없으면 부모 캐릭터에서 hand.R 본 자동 탐색
        if (target == null)
        {
            var root = transform.parent;
            while (root != null && root.parent != null)
                root = root.parent;

            if (root != null)
                target = FindBoneRecursive(root, "hand.R");

            if (target == null)
                Debug.LogWarning("[WeaponFollower] hand.R 본을 찾을 수 없음!");
            else
                Debug.Log($"[WeaponFollower] hand.R 찾음: {target.name}");
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;
        transform.position = target.position + target.TransformDirection(positionOffset);
        transform.rotation = target.rotation * Quaternion.Euler(rotationOffset);
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