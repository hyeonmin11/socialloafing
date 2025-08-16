using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class BoxManager : NetworkBehaviour
{
    [SerializeField] private List<BoxData> boxDatas = new List<BoxData>();
    private struct BoxSnapshot
    {
        public Transform parent;         // 원래 부모
        public Vector3 localPos;         // 원래 부모 기준 위치
        public Quaternion localRot;      // 원래 부모 기준 회전
        public Vector3 localScale;       // 원래 스케일
        public Vector3 worldPos;         // 백업용(부모가 사라졌을 때)
        public Quaternion worldRot;
        public bool hadRigidbody;
        public bool initialKinematic;
    }

    private Dictionary<BoxData, BoxSnapshot> _snapshots = new();

    // 초기 Transform 기록용
    // private Dictionary<BoxData, Vector3> initialPositions = new Dictionary<BoxData, Vector3>();
    // private Dictionary<BoxData, Quaternion> initialRotations = new Dictionary<BoxData, Quaternion>();

    private void Awake()
    {
        // 씬에 존재하는 모든 BoxData 오브젝트 기록
        var allBoxes = FindObjectsOfType<BoxData>();
        foreach (var box in allBoxes)
        {

            // initialPositions[box] = box.transform.position;
            // initialRotations[box] = box.transform.rotation;
            if (box == null) continue;
            var t = box.transform;
            var rb = box.GetComponent<Rigidbody>();

            _snapshots[box] = new BoxSnapshot
            {
                parent = t.parent,
                localPos = t.localPosition,
                localRot = t.localRotation,
                localScale = t.localScale,
                worldPos = t.position,
                worldRot = t.rotation,
                hadRigidbody = rb != null,
                initialKinematic = rb != null ? rb.isKinematic : false
            };
            boxDatas.Add(box);
        }
    }

    public void ResetAllBoxes()
    {
        foreach (var box in boxDatas)
        {
            ResetOneBox(box);
        }
    }

    public void ResetOneBox(BoxData box)
    {
        if (box == null) return;
        if (!_snapshots.TryGetValue(box, out var snap))
        {
            // 처음 본 박스면 즉시 스냅샷을 만들고 끝
            var t0 = box.transform;
            _snapshots[box] = new BoxSnapshot
            {
                parent = t0.parent,
                localPos = t0.localPosition,
                localRot = t0.localRotation,
                localScale = t0.localScale,
                worldPos = t0.position,
                worldRot = t0.rotation,
                hadRigidbody = t0.GetComponent<Rigidbody>() != null,
                initialKinematic = t0.GetComponent<Rigidbody>() ? t0.GetComponent<Rigidbody>().isKinematic : false
            };
            return;
        }

        var t = box.transform;

        // 1) 현재 부모 해제(혹시 손/컨테이너에 붙어있을 수 있음)
        //t.SetParent(null, true);

        // 2) 물리 정지 + 초기 kinematic 복원
        var rb = box.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = snap.initialKinematic;
        }


        //bool isNetworked = false;

        if (snap.parent != null)
        {

            t.SetParent(snap.parent, false);


            // 부모 기준 로컬 변환 복원
            t.localPosition = snap.localPos;
            t.localRotation = snap.localRot;
            t.localScale = snap.localScale;
        }
        else
        {
            t.position = snap.worldPos;
            t.rotation = snap.worldRot;
            t.localScale = snap.localScale;

        }

        var col = box.GetComponent<Collider>();
        if (col) col.enabled = true;
        var obs = box.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obs) obs.enabled = true;

        box.isComplete = false;
    }
    
    public void ResetAllBoxes_multi()
    {
        // ✅ 서버 전용
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        foreach (var box in boxDatas)
        {
            ResetOneBox_multi(box);
        }
    }

    public void ResetOneBox_multi(BoxData box)
    {
        if (box == null) return;

        // 스냅샷 없으면 한 번 채우고 리턴(원래 로직 유지)
        if (!_snapshots.TryGetValue(box, out var snap))
        {
            var t0 = box.transform;
            var rb0 = t0.GetComponent<Rigidbody>();
            _snapshots[box] = new BoxSnapshot
            {
                parent = t0.parent,
                localPos = t0.localPosition,
                localRot = t0.localRotation,
                localScale = t0.localScale,
                worldPos = t0.position,
                worldRot = t0.rotation,
                hadRigidbody = rb0 != null,
                initialKinematic = rb0 ? rb0.isKinematic : false
            };
            return;
        }

        var t = box.transform;

        // 물리/장애물 잠시 안전하게 비활성
        var rb  = box.GetComponent<Rigidbody>();
        var col = box.GetComponent<Collider>();
        var obs = box.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (rb)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = snap.initialKinematic;
        }
        if (col) col.enabled = false;
        if (obs) obs.enabled = false;

        // 부모 복원
        if (snap.parent != null)
        {
            // 부모로 되돌릴 때 월드좌표를 스냅샷대로 만들려면 false로 두고 로컬값 세팅
            t.SetParent(snap.parent, false);
        }
        else
        {
            // 최상위로
            t.SetParent(null, true);
        }

        // 네트워크 컴포넌트 확인
        var no = box.GetComponent<NetworkObject>();
        var nt = box.GetComponent<NetworkTransform>();
        //var cnt = box.GetComponent<ClientNetworkTransform>();

        // ✅ 서버 권한으로 강제 동기화
        // - NetworkTransform(서버 권한): Teleport로 즉시 반영
        // - ClientNetworkTransform(오너 권한): 서버가 못 쓰므로 소유권을 서버로 바꿔 Teleport
        //   (박스는 월드 오브젝트라 서버 오너십이 보통 권장)

        // 목표 pose 계산 (부모 기준 로컬 스냅샷 → 월드로 환산)
        Vector3 worldPos;
        Quaternion worldRot;
        if (snap.parent != null)
        {
            worldPos = snap.parent.TransformPoint(snap.localPos);
            worldRot = snap.parent.rotation * snap.localRot;
        }
        else
        {
            worldPos = snap.worldPos;
            worldRot = snap.worldRot;
        }

        // 실제 배치 + 네트워크 텔레포트
        if (no != null && no.IsSpawned)
        {
            // CNT면 서버가 못 쓰므로 서버 오너십으로 전환
            if (no.OwnerClientId != NetworkManager.ServerClientId)
            {
                no.ChangeOwnership(NetworkManager.ServerClientId);
            }

            if (nt != null)
            {
                // 서버 권한 NetworkTransform
                nt.Teleport(worldPos, worldRot, snap.localScale);
            }
            else
            {
                // NT가 없어도 최후 수단으로 직접 배치
                t.SetPositionAndRotation(worldPos, worldRot);
                t.localScale = snap.localScale;
            }
        }
        else
        {
            // 네트워크 대상 아님(씬 오브젝트)이거나 아직 스폰 전
            t.SetPositionAndRotation(worldPos, worldRot);
            t.localScale = snap.localScale;
        }

        // 콜라이더/장애물 복구
        if (col) col.enabled = true;
        if (obs) obs.enabled = true;

        // 상태 초기화
        box.isComplete = false;
    }

}



