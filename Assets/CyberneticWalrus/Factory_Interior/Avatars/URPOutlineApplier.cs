using UnityEngine;
using System.Linq;
using Unity.Netcode;
[DisallowMultipleComponent]
public class URPOutlineApplier : MonoBehaviour
{
    public Color outlineColor = Color.yellow;
    [Range(0f, 0.05f)] public float outlineWidth = 0.01f;
    public Material outlineMaterialTemplate; // 위 셰이더로 만든 머티리얼 할당

    void Awake()
    {

        // if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        //     return;

        if (outlineMaterialTemplate == null)
        {
            Debug.LogError("Outline 머티리얼을 URPOutlineApplier에 할당하세요.");
            return;
        }
        outlineMaterialTemplate.SetColor("_OutlineColor", outlineColor);
        outlineMaterialTemplate.SetFloat("_OutlineWidth", outlineWidth);

        var renderers = GetComponentsInChildren<Renderer>(true)
                        .Where(r => !(r is ParticleSystemRenderer)); // 파티클 제외

        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials.ToList();
            if (!mats.Contains(outlineMaterialTemplate))
            {
                mats.Add(new Material(outlineMaterialTemplate)); // 인스턴스화
                r.sharedMaterials = mats.ToArray();
            }
        }
    }
}
