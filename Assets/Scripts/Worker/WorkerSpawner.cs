using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum WorkerType
{
    Fast, Slow
}
public class WorkerSpawner : MonoBehaviour
{
    [SerializeField]
    private List<WorkerData> workerDatas;

    [SerializeField]
    private GameObject workerPrefab;

    private Dictionary<string, Transform> spawnPointMap;
    [SerializeField]
    public WorkerManager workerManager;

    void Awake()
    {
        // 씬에 있는 모든 SpawnPoint 수집
        spawnPointMap = new Dictionary<string, Transform>();
        foreach (var sp in FindObjectsOfType<SpawnPoint>())
        {
            spawnPointMap[sp.spawnPointID] = sp.transform;
        }

        foreach (var data in workerDatas)
        {
            var worker = SpawnWorker(data);
            if (worker != null)
                worker.WatchWorkerInfo();
        }
    }

    // void Start()
    // {
    //     foreach (var data in workerDatas)
    //     {
    //         var worker = SpawnWorker(data);
    //         if (worker != null)
    //             worker.WatchWorkerInfo();
    //     }
    // }

    public Worker SpawnWorker(WorkerData data)
    {
        if (!spawnPointMap.TryGetValue(data.spawnPointID, out var spawnTransform))
        {
            Debug.LogWarning($"Spawn point '{data.spawnPointID}' not found.");
            return null;
        }

        GameObject obj = Instantiate(workerPrefab, spawnTransform.position, spawnTransform.rotation);
        Worker newWorker = obj.GetComponent<Worker>();
        data.InitialPos = spawnTransform.position;
        data.InitialRot = spawnTransform.rotation;
        newWorker.WorkerData = data;


        workerManager.RegisterWorker(newWorker);

        return newWorker;
    }
}

