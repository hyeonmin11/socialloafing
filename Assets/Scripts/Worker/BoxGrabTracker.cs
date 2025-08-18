using UnityEngine;
using Oculus.Interaction; // Grabbable / PointerEvent / PointerEventType
using Unity.Netcode;

[RequireComponent(typeof(Grabbable))]
public class BoxGrabTracker : MonoBehaviour
{
    [SerializeField] private Worker playerWorker; // XR Origin에 붙은 Worker를 드래그로 할당

    private Grabbable grabbable;
    private BoxData boxData;

    private bool picked;
    private float lastPickupTime;
    private static bool IsMultiplayerActive =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        boxData = GetComponent<BoxData>();

        // Grabbable이 발생시키는 포인터 이벤트 구독
        grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDestroy()
    {
        if (grabbable != null)
            grabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Select:
                // 집기 시작
                picked = true;
                lastPickupTime = Time.time;

                var rb = GetComponent<Rigidbody>();
                var col = GetComponent<Collider>();
                var obs = GetComponent<UnityEngine.AI.NavMeshObstacle>();
                if (rb) rb.isKinematic = true;
                if (col) col.enabled = false;
                if (obs) obs.enabled = false;

                break;

            case PointerEventType.Unselect:
                // 놓는 순간 완료 처리
                if (!picked) return;
                picked = false;

                rb  = GetComponent<Rigidbody>();
                col = GetComponent<Collider>();


                // ← 놓는 순간 물리 복구
                if (rb)  rb.isKinematic = false;
                if (col) col.enabled = true;
                if (boxData == null) return;

                float taskSeconds = Mathf.Floor(Time.time - lastPickupTime);

                if (!IsMultiplayerActive)
                {
                    // ====== 싱글 플레이어: 로컬 bool 사용 ======
                    if (!boxData.isComplete)
                    {
                        boxData.isComplete = true;

                        if (playerWorker != null && playerWorker.WorkerData != null)
                        {
                            // 로컬 KPI 반영
                            var wd = playerWorker.WorkerData;
                            wd.DecreaseBoxCount();
                            wd.SetCompletionTimePerBox(taskSeconds);
                        }
                    }
                }
                else
                {
                    // ====== 멀티 플레이어: 서버가 확정 ======
                    // BoxData 쪽에 정의한 ServerRpc로 네트워크 변수(IsComplete) 세팅
                    if (boxData.IsSpawned)
                    {
                        boxData.MarkCompleteServerRpc(); // 서버가 IsComplete.Value = true (idempotent)
                    }

                    // KPI 보고는 기존대로 서버 RPC 호출 (서버에서 중복 방지 로직이 있기를 권장)
                    var kpi = playerWorker ? playerWorker.GetComponent<WorkerKpiSync>() : null;
                    if (kpi != null && kpi.IsSpawned)
                    {
                        kpi.ReportTaskCompletedServerRpc(taskSeconds);
                    }
                }
                break;
                // 한 번만 집계
                // if (!boxData.isComplete)
                // {
                //     boxData.isComplete = true;

                //     if (playerWorker != null && playerWorker.WorkerData != null)
                //     {
                //         float taskSeconds = Mathf.Floor(Time.time - lastPickupTime);



                //         var kpi = playerWorker.GetComponent<WorkerKpiSync>();
                //         if (kpi != null && kpi.IsSpawned)
                //         {
                //             kpi.ReportTaskCompletedServerRpc(taskSeconds);
                //             return;
                //         }

                //         var wd = playerWorker.WorkerData;
                //         wd.DecreaseBoxCount();
                //         wd.SetCompletionTimePerBox(taskSeconds);

                //     }
                // }
                // break;
        }
    }
}



