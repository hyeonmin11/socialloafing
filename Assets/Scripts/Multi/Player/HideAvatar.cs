using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

public class HideMySophieForOwner : NetworkBehaviour
{
    [SerializeField] GameObject sophieRoot;   // Sophie@Walking 루트 할당
    [SerializeField] bool disableShadows = true;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner || sophieRoot == null) return;   // 내 클라이언트에서만 실행
        var renders = sophieRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renders)
        {
            r.enabled = false;                        // 그래픽만 숨김
            if (disableShadows)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
    }
}
