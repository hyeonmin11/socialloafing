using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class EnsureChildOwnership : NetworkBehaviour
{
    [SerializeField] private NetworkObject childRoot;

    public override void OnNetworkSpawn()
    {
        if (!IsServer || childRoot == null) return;
        StartCoroutine(EnsureCo());
    }

    private IEnumerator EnsureCo()
    {
        while (!childRoot.IsSpawned) yield return null;
        if (childRoot.OwnerClientId != OwnerClientId)
            childRoot.ChangeOwnership(OwnerClientId);
    }
}

