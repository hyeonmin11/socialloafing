using Unity.Netcode;
using UnityEngine;

public class HideSelfForOwner : NetworkBehaviour
{
    Renderer[] _renders;

    public override void OnNetworkSpawn()
    {
        _renders = GetComponentsInChildren<Renderer>(true);
        ApplyVisibility();
    }

    public override void OnGainedOwnership()  => ApplyVisibility();
    public override void OnLostOwnership()    => ApplyVisibility();

    void ApplyVisibility()
    {
        bool visible = !IsOwner; // 본인에겐 숨기고, 남들에겐 보이게
        if (_renders == null) return;
        foreach (var r in _renders)
        {
            r.enabled = visible;       // 그래픽만 끔(콜라이더/애니메이션은 유지)
            r.shadowCastingMode = visible ? 
                UnityEngine.Rendering.ShadowCastingMode.On :
                UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = visible;
        }
    }
}

