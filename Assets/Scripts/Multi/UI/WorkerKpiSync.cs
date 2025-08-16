using Unity.Netcode;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Worker))]
public class WorkerKpiSync : NetworkBehaviour
{
    public NetworkVariable<int>   NV_NumBox = new(-1,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> NV_CompletionTimePerBox = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Worker _worker;
    private Coroutine _pollCo;

    private void Awake()
    {
        _worker = GetComponent<Worker>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && _worker != null && _worker.WorkerData != null)
            _pollCo = StartCoroutine(PollRoutine()); // 서버가 주기적으로 WorkerData 값을 NV로 반영
    }

    public override void OnNetworkDespawn()
    {
        if (_pollCo != null) StopCoroutine(_pollCo);
    }

    private IEnumerator PollRoutine()
    {
        int   lastNum  = int.MinValue;
        float lastTime = float.MinValue;

        while (true)
        {
            var wd = _worker.WorkerData;
            if (wd != null)
            {
                if (wd.NumBox != lastNum)
                {
                    NV_NumBox.Value = wd.NumBox;
                    lastNum = wd.NumBox;
                }
                if (!Mathf.Approximately(wd.CompletionTimePerBox, lastTime))
                {
                    NV_CompletionTimePerBox.Value = wd.CompletionTimePerBox;
                    lastTime = wd.CompletionTimePerBox;
                }
            }
            yield return new WaitForSeconds(0.2f); // 5Hz 갱신
        }
    }


    // ✅ 클라이언트가 완료를 서버에 보고
    [ServerRpc(RequireOwnership = false)]
    public void ReportTaskCompletedServerRpc(float taskSeconds)
    {
        if (_worker == null || _worker.WorkerData == null) return;

        // 서버가 진짜 값을 갱신(권위)
        _worker.WorkerData.DecreaseBoxCount();
        _worker.WorkerData.SetCompletionTimePerBox(Mathf.Floor(taskSeconds));

        // 원한다면 즉시 NV도 갱신(폴링 기다릴 필요 없음)
        NV_NumBox.Value = _worker.WorkerData.NumBox;
        NV_CompletionTimePerBox.Value = _worker.WorkerData.CompletionTimePerBox;
    }


    // UI에서 편하게 읽도록 헬퍼
    public int GetNumBox() => IsServer ? _worker?.WorkerData?.NumBox ?? -1 : NV_NumBox.Value;
    public float GetCompletionTimePerBox() => IsServer ? _worker?.WorkerData?.CompletionTimePerBox ?? 0f : NV_CompletionTimePerBox.Value;
}

