using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxData : MonoBehaviour
{
    public bool isComplete = false;
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
