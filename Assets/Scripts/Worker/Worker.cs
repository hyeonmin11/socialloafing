using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Worker : MonoBehaviour
{
    [SerializeField] private bool isPlayer = false;   // ✅ 플레이어 구분용
    public bool IsPlayer
    {
        get => isPlayer;
        set => isPlayer = value;
    }

    [SerializeField]
    private WorkerData workerData;
    public WorkerData WorkerData
    {
    get { return workerData; }
    set { workerData = value;
          SpawnAvatar();
        }
    }

    private void SpawnAvatar()
    {
        if (workerData == null || workerData.AvatarPrefab == null) return;
        if (workerData.AvatarPrefab != null)
        {
            GameObject avatar = Instantiate(workerData.AvatarPrefab, transform);
            avatar.transform.localPosition = Vector3.zero;
            avatar.transform.localRotation = Quaternion.identity;

            // if (workerData.WorkerName.ToLower() == "isabella")
            // {
            //     Vector3 pos = avatar.transform.localPosition;
            //     pos.y = 0.8f;
            //     avatar.transform.localPosition = pos;
            // }
        }
    }
    public void WatchWorkerInfo()
    {
        Debug.Log("Worker Name " + workerData.WorkerName);
        Debug.Log("Worker Box " + workerData.NumBox);
        Debug.Log("Worker completiontimeperBox " + workerData.CompletionTimePerBox);
    }


}
