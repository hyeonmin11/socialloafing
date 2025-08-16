using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.AI.NavMeshAgent;

public class MovToDestination : MonoBehaviour
{
    // Start is called before the first frame update
    public Transform target;
    private UnityEngine.AI.NavMeshAgent agent;
    private Worker worker;
    private string workername;
    private Dictionary<string, Transform> destinationMap;
    private bool ispickupBox = false;
    private BoxData targetbox;
    private Animator _animator;
    private Coroutine deliveryCo;
    [SerializeField] private Transform carryAnchor;

    private float pickUpStartTime;

    void Awake()
    {
        destinationMap = new Dictionary<string, Transform>();
        foreach (var dp in FindObjectsOfType<DropoffPoint>())
        {
            destinationMap[dp.dropoffpointID] = dp.transform;
        }

    }
    void Start()
    {
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        worker = GetComponent<Worker>();
        workername = worker.WorkerData.WorkerName;
        _animator = GetComponentInChildren<Animator>();

        //StartCoroutine(DeliveryRoutine());


    }

    private IEnumerator PickUpBoxCoroutine()
    {

        BoxData[] allboxes = FindObjectsOfType<BoxData>();

        foreach (BoxData box in allboxes)
        {
            if (!box.isComplete && box.assignedWorkerData.WorkerName == workername)
            {
                targetbox = box;

                break;
            }
        }

        //////////////////////
        if (targetbox == null)
        {
            //Debug.LogError($"{workername} has no targetbox assigned before PickUpBoxCoroutine.");
            yield break;
        }
        //////////////////////
        agent.SetDestination(targetbox.transform.position);
        while (agent.pathPending)
        {
            yield return null;
        }
        yield return null;
        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance || agent.velocity.sqrMagnitude > 0f)
        {
            yield return null;
        }

        //////////////////////
        if (targetbox == null)
        {
            //Debug.LogError($"{workername} has no targetbox assigned before PickUpBoxCoroutine.");
            yield break;
        }
        //////////////////////



        var rb = targetbox.GetComponent<Rigidbody>();
        var col = targetbox.GetComponent<Collider>();
        var obs = targetbox.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (rb) rb.isKinematic = true;
        if (col) col.enabled = false;
        if (obs) obs.enabled = false;


        ispickupBox = true;

        pickUpStartTime = Time.time;
    }


    private IEnumerator DropOffBoxCoroutine()
    {
        agent.SetDestination(destinationMap[worker.WorkerData.dropoffLocationID].position);
        while (agent.pathPending)
        {
            yield return null;
        }
        yield return null;
        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance || agent.velocity.sqrMagnitude > 0f) // 
        {

            yield return null;
            UpdateHeldBoxPosition();
        }
        //////////////////////
        if (targetbox == null)
        {
            //Debug.LogError($"{workername} has no targetbox assigned before DropOffBoxCoroutine.");
            yield break;
        }
        //////////////////////

        var rb  = targetbox.GetComponent<Rigidbody>();
        var col = targetbox.GetComponent<Collider>();

        if (col) col.enabled = true;
        if (rb)  rb.isKinematic = false;


        targetbox.isComplete = true;
        worker.WorkerData.DecreaseBoxCount();
        targetbox = null;
        ispickupBox = false;

        float elapsed = Mathf.Floor(Time.time - pickUpStartTime);
        worker.WorkerData.SetCompletionTimePerBox(elapsed);

        StartCoroutine(isfinish());
        if (targetbox == null)
        {
            //Debug.LogError($"{workername} has no targetbox assigned before DropOffBoxCoroutine.");
            yield break;
        }


        /////////////////////////////////
        // targetbox.transform.SetParent(null);
        // targetbox.GetComponent<Rigidbody>().isKinematic = false;

        // // 현재 위치 기준으로 조금 옆에 내려놓기
        // Vector3 dropPos = transform.position + transform.forward * 1.0f;
        // targetbox.transform.position = dropPos;
        /////////////////////////////////




        // targetbox.isComplete = true;
        // worker.WorkerData.DecreaseBoxCount();
        // targetbox = null;
        // ispickupBox = false;

        // StartCoroutine(isfinish());
    }

    private IEnumerator isfinish()
    {
        // worker에게 할당된 box가 전부 완료되면 처음 자리로 돌아간다
        if (worker.WorkerData.NumBox == 0)
        {
            foreach (var sp in FindObjectsOfType<SpawnPoint>())
            {
                if (worker.WorkerData.spawnPointID == sp.spawnPointID)
                {
                    Vector3 dest = sp.transform.position;
                    agent.SetDestination(dest);
                    while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance || agent.velocity.sqrMagnitude > 0f)
                    {
                        yield return null;
                    }
                    worker.WorkerData.Finished();
                    break;
                }
            }
        }

    }

    private IEnumerator DeliveryRoutine()
    {
        while (worker.WorkerData.NumBox > 0)
        {
            yield return StartCoroutine(PickUpBoxCoroutine());
            yield return StartCoroutine(DropOffBoxCoroutine());
        }

        deliveryCo = null;
    }



    // void LateUpdate()
    // {
    //     if (ispickupBox && targetbox != null)
    //     {
    //         UpdateHeldBoxPosition();
    //     }
    // }
    private void UpdateHeldBoxPosition()
    {
        if (targetbox == null)
        {
            //Debug.LogError($"{workername} has no targetbox assigned before UpdateHeldBoxPosition.");
            return;
        }

        Transform avatar = transform.GetChild(0); // Brian@Walking(Clone)
        float visualHeight = 1.6f;

        foreach (Transform child in avatar)
        {
            if (child.name.Contains("Body"))
            {
                SkinnedMeshRenderer smr = child.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    visualHeight = smr.bounds.size.y;
                    break;
                }
            }
        }

        Vector3 forward = transform.forward * 0.5f;
        Vector3 upward = Vector3.up * (visualHeight * 0.5f);
        Vector3 worldTargetPos = transform.position + forward + upward;

        // 상자 위치 직접 갱신
        targetbox.transform.position = worldTargetPos;
    }
    public void BeginDelivery()
    {
        if (deliveryCo != null) return;
        deliveryCo = StartCoroutine(DeliveryRoutine());
    }
    public void StopDelivery()
    {
        if (deliveryCo != null)
        {
            StopCoroutine(deliveryCo);
            deliveryCo = null;
        }
        ispickupBox = false;
        targetbox = null;
        // 필요 시 이동 정지
        if (agent != null)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }
    private void RecomputeCarryAnchor()
    {
        float visualHeight = 1.6f;

        // 아바타 루트(첫 자식) 찾기
        if (transform.childCount > 0)
        {
            Transform avatar = transform.GetChild(0); // Brian@Walking(Clone) 가정
            foreach (Transform child in avatar)
            {
                if (child.name.Contains("Body"))
                {
                    var smr = child.GetComponent<SkinnedMeshRenderer>();
                    if (smr != null)
                    {
                        visualHeight = smr.bounds.size.y; // 실제 시각적 높이
                        break;
                    }
                }
            }
        }

        // 네가 쓰던 규칙: 정면 0.5m, 키의 절반 높이
        float forwardZ = 0.5f;
        float upY = visualHeight * 0.5f;

        // anchor는 워커의 자식이므로 localPosition으로 잡으면,
        // 워커의 앞/위 기준 자동 추적됨
        carryAnchor.localPosition = new Vector3(0f, upY, forwardZ);
        carryAnchor.localRotation = Quaternion.identity;
    }

}
