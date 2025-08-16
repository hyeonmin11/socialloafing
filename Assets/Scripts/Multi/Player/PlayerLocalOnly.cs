using Unity.Netcode;
using UnityEngine;

public class PlayerLocalOnly : NetworkBehaviour
{
    [SerializeField] private GameObject localOnlyRoot; // XR 카메라/컨트롤러가 들어있는 루트
    [SerializeField] private GameObject localOnlyRoot2;
    public override void OnNetworkSpawn()
    {
        if (localOnlyRoot != null) localOnlyRoot.SetActive(IsOwner);
        if (localOnlyRoot2 != null) localOnlyRoot2.SetActive(IsOwner);
    }
}

