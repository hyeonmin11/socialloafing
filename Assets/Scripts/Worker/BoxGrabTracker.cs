using UnityEngine;
using Oculus.Interaction; // Grabbable / PointerEvent / PointerEventType

[RequireComponent(typeof(Grabbable))]
public class BoxGrabTracker : MonoBehaviour
{
    [SerializeField] private Worker playerWorker; // XR Origin에 붙은 Worker를 드래그로 할당

    private Grabbable grabbable;
    private BoxData boxData;

    private bool picked;
    private float lastPickupTime;

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

                // 한 번만 집계
                if (!boxData.isComplete)
                {
                    boxData.isComplete = true;

                    if (playerWorker != null && playerWorker.WorkerData != null)
                    {
                        float taskSeconds = Mathf.Floor(Time.time - lastPickupTime);



                        var kpi = playerWorker.GetComponent<WorkerKpiSync>();
                        if (kpi != null && kpi.IsSpawned)
                        {
                            kpi.ReportTaskCompletedServerRpc(taskSeconds);
                            return;
                        }

                        var wd = playerWorker.WorkerData;
                        wd.DecreaseBoxCount();
                        wd.SetCompletionTimePerBox(taskSeconds);

                    }
                }
                break;
        }
    }
}



