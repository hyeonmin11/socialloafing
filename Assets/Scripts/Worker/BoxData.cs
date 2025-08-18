using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class BoxData : NetworkBehaviour
{
    public bool isComplete = false;
    public NetworkVariable<bool> IsComplete = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    [ServerRpc(RequireOwnership = false)]
    public void MarkCompleteServerRpc()
    {
        if (!IsComplete.Value)
            IsComplete.Value = true;
    }
    public WorkerData assignedWorkerData = null;


    void Awake()
    {
        AssignBoxToWorkers();
    }
    // void Start()
    // {
    //     AssignBoxToWorkers();
    // }

    private void AssignBoxToWorkers()
    {
        string workerName = transform.parent.name.Replace("_boxes", "").ToLower();
        //Debug.Log("BoxData workername" + workerName);
        WorkerData[] allWorkers = Resources.LoadAll<WorkerData>("");
        foreach (WorkerData w in allWorkers)
        {
            //Debug.Log("BoxData w.workername" + w.WorkerName.ToLower());
            if (w.WorkerName.ToLower() == workerName)
            {
                assignedWorkerData = w;
                Debug.Log("BoxData assigned" + assignedWorkerData.WorkerName.ToLower());
                return;
            }
        }

    }
}
