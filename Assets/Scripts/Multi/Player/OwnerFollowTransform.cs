using Unity.Netcode;
using UnityEngine;

public class OwnerFollowTransform : NetworkBehaviour
{
    public Transform target;                // Root
    public Vector3 positionOffset;          // 필요하면 오프셋
    public Vector3 eulerOffset;

    public override void OnNetworkSpawn()
    {
        // 오너만 직접 움직임 계산
        enabled = IsOwner;
    }

    void LateUpdate()
    {
        if (target == null) return;
        transform.position = target.TransformPoint(positionOffset);
        transform.rotation = target.rotation * Quaternion.Euler(eulerOffset);
    }
}

