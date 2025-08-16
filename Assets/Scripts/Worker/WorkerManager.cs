using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;

public class WorkerManager : NetworkBehaviour
{
    [SerializeField]
    private List<Worker> workers = new List<Worker>();

    public void RegisterWorker(Worker worker)
    {
        if (!workers.Contains(worker))
            workers.Add(worker);
    }

    public List<Worker> GetWorkers()
    {
        return workers;
    }

    public void ResetAllWorkers()
    {
        foreach (var w in workers)
        {
            w.WorkerData.ResetKpis();
            if (w.IsPlayer) continue;
            w.transform.SetPositionAndRotation(w.WorkerData.InitialPos, w.WorkerData.InitialRot);

            var mover = w.GetComponent<MovToDestination>();
            if (mover != null) mover.StopDelivery();   // 코루틴/상태 끊기

            var agent = w.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                // NavMesh 상에서만 Warp, 아니면 transform으로 강제
                agent.ResetPath();
                agent.velocity = Vector3.zero;

                // 안전 가드: NavMesh 위가 아니면 SamplePosition 후 Warp
                var pos = w.WorkerData.InitialPos;
                if (agent.isOnNavMesh)
                {
                    agent.Warp(pos);
                }
                else
                {
                    if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
                        agent.Warp(hit.position);
                    else
                        w.transform.position = pos; // 최후의 수단
                }

                // 회전은 transform 회전으로
                w.transform.rotation = w.WorkerData.InitialRot;
            }
        }
    }

    public void ResetAllWorkers_multi()
    {
        // ✅ 서버 전용
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        foreach (var w in workers)
        {
            if (w == null) continue;

            // KPI 리셋
            w.WorkerData.ResetKpis();

            // 플레이어는 위치 리셋하지 않음 (원한다면 분기 조정)
            if (w.IsPlayer)
            {
                // 플레이어 관련 코루틴/상태만 정지하고 싶으면 여기서 처리
                continue;
            }

            // 이동 코루틴 정지 (멀티 버전)
            var moverMulti = w.GetComponent<MovToDestination_multi>();
            if (moverMulti != null) moverMulti.StopDelivery();

            var t = w.transform;
            var agent = w.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var nt = w.GetComponent<NetworkTransform>(); // 서버 권한 동기화용
            Vector3 pos = w.WorkerData.InitialPos;
            Quaternion rot = w.WorkerData.InitialRot;

            // InitialPos/Rot이 비어있을 경우 대비(필요 시 보강)
            if (pos == default && rot == default)
            {
                pos = t.position;
                rot = t.rotation;
            }

            // ✅ NavMesh 위로 Warp 우선, 아니면 샘플 후 Warp
            if (agent != null)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;

                if (agent.isOnNavMesh)
                {
                    agent.Warp(pos);
                    t.rotation = rot;
                }
                else
                {
                    if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        agent.Warp(hit.position);
                        t.rotation = rot;
                    }
                    else
                    {
                        t.SetPositionAndRotation(pos, rot); // 최후의 수단
                    }
                }
            }
            else
            {
                t.SetPositionAndRotation(pos, rot);
            }

            // ✅ 네트워크 즉시 동기화
            if (nt != null && nt.IsSpawned)
            {
                nt.Teleport(t.position, t.rotation, t.localScale);
            }
        }
    }

}
