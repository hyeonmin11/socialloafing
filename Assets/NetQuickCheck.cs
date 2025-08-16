using Unity.Netcode;
using UnityEngine;

public class NetQuickCheck : NetworkBehaviour
{
    [SerializeField] private Transform Root;

    private bool printedSpawn;
    private bool printedOwnerPath;
    private bool printedRemotePath;

    public override void OnNetworkSpawn()
    {
        if (!printedSpawn)
        {
            Debug.Log($"[Player] Spawned. IsOwner={IsOwner} IsServer={IsServer} " +
                      $"OwnerClientId={OwnerClientId} LocalClientId={NetworkManager.LocalClientId}");
            printedSpawn = true;
        }
    }

    void LateUpdate()
    {
        if (!Root) return;

        if (IsOwner)
        {
            if (!printedOwnerPath)
            {
                Debug.Log("[NetQuickCheck] Owner drives: Root -> Player");
                printedOwnerPath = true;
            }
        }
        else
        {
            if (!printedRemotePath)
            {
                Debug.Log("[NetQuickCheck] Remote drives: Player -> Root");
                printedRemotePath = true;
            }
        }
    }
}

