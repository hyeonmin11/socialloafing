
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class DashboardWorkerDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text nameTag;
    [SerializeField] private TMP_Text numBox;
    [SerializeField] private TMP_Text completionTime;
    [SerializeField] private TMP_Text timer;


    private WorkerData workerData;
    private WorkerKpiSync kpi;
    private bool teamOnlyMode = false;

    public void SetWorker(WorkerData data, string displayName)
    {
        teamOnlyMode = false;
        workerData = data;

        if (nameTag) nameTag.text = displayName;
        if (completionTime) completionTime.gameObject.SetActive(true);
    }

    // TeamOnly 모드: 헤더만 설정 (집계값은 SetTeamOnlyBoxes로 갱신)
    public void SetTeamOnlyHeader(string displayName)
    {
        teamOnlyMode = true;
        workerData = null;

        if (nameTag) nameTag.text = displayName;
        if (completionTime) completionTime.gameObject.SetActive(false);
    }

    // TeamOnly 모드: 박스 합계 설정
    public void SetTeamOnlyBoxes(int sum)
    {
        if (numBox) numBox.text = sum.ToString();
    }

    // 남는 슬롯을 지우고 싶을 때
    public void Clear()
    {
        teamOnlyMode = false;
        workerData = null;
        if (nameTag) nameTag.text = "";
        if (numBox) numBox.text = "0";
        if (completionTime) completionTime.text = "";
    }

    private void Update()
    {
        if (teamOnlyMode) return; // TeamOnly는 외부 갱신만

        if (workerData == null) return;

        if (kpi != null)
        {
            numBox.text = $"{kpi.GetNumBox()}";
            if (completionTime) completionTime.text = $"{kpi.GetCompletionTimePerBox():0.#}sec";
            return;
        }

        if (numBox) numBox.text = $"{workerData.NumBox}";
        if (completionTime) completionTime.text = $"{workerData.CompletionTimePerBox}sec";
    }

    //멀티플레이어용 setworker//
    public void SetWorker(Worker worker, string displayName)
    {
        teamOnlyMode = false;
        workerData = worker ? worker.WorkerData : null;
        kpi = worker ? worker.GetComponent<WorkerKpiSync>() : null;

        if (nameTag) nameTag.text = displayName;
        if (completionTime) completionTime.gameObject.SetActive(true);
    }

    // ---------- 멀티(명시적) : KPI Sync를 외부에서 직접 넘기고 싶을 때 ----------
    public void SetWorker(WorkerData data, string displayName, WorkerKpiSync kpiOverride)
    {
        teamOnlyMode = false;
        workerData = data;
        kpi = kpiOverride;

        if (nameTag) nameTag.text = displayName;
        if (completionTime) completionTime.gameObject.SetActive(true);
    }



}

