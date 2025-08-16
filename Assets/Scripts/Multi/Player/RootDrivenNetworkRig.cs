using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RootDrivenNetworkRig : NetworkBehaviour
{
    [SerializeField] private Transform root; // Player 하위의 Root 지정

    void Reset()
    {
        // 자동으로 "Root" 찾기 (없으면 인스펙터에서 수동 지정)
        if (root == null)
        {
            var t = transform.Find("Root");
            if (t) root = t;
        }
    }

    void LateUpdate()
    {
        if (root == null) return;

        if (IsOwner)
        {
            // 1) Root의 '월드' 트랜스폼을 Player로 복사
            Vector3 wpos = root.position;
            Quaternion wrot = root.rotation;
            transform.SetPositionAndRotation(wpos, wrot);

            // 2) Root는 항상 부모(Local) 기준으로 원점에 두기
            //    (Player가 움직여도 카메라 리그가 어긋나지 않게 고정)
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
        }
        else
        {
            // 원격에서는 Player가 네트워크로 움직임 → Root는 항상 로컬 0으로만 유지
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
        }
    }
}
