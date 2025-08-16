using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;   
using System.Linq;
public class DashboardController_multi : MonoBehaviour
{
    [SerializeField] private List<DashboardWorkerDisplay> workerDisplays;
    private bool anonymous = false;
    private Coroutine timerRoutine;
    [SerializeField] private TMP_Text timerText;

    // TeamOnly 내부 상태
    private List<Worker> _allWorkers = new();
    private List<List<Worker>> _teams = new(); // [0]=플레이어팀, [1..4]=에이전트팀
    private Coroutine _teamOnlyLoop;
    [SerializeField] private DashboardKind dashboardKind = DashboardKind.Personal;

    private void Start()
    {
        var wm = FindObjectOfType<WorkerManager>();
        if (wm != null)
        {
            var workers = wm.GetWorkers();

            //SetWorkers(workers, anonymous);
        }
    }
    public void SetWorkers(List<Worker> workers, bool anonymous)
    {

        if (dashboardKind == DashboardKind.TeamOnly)
        {
            // TeamOnly: 5팀 집계 표시
            _allWorkers = workers ?? new List<Worker>();
            BuildTeamsForTeamOnly();

            // 팀 헤더 설정
            EnsureDisplayCount(5);
            if (workerDisplays.Count >= 5)
            {
                workerDisplays[0].SetTeamOnlyHeader("Player Team");
                workerDisplays[1].SetTeamOnlyHeader("Agent Team 1");
                workerDisplays[2].SetTeamOnlyHeader("Agent Team 2");
                workerDisplays[3].SetTeamOnlyHeader("Agent Team 3");
                workerDisplays[4].SetTeamOnlyHeader("Agent Team 4");
            }

            if (_teamOnlyLoop != null) StopCoroutine(_teamOnlyLoop);
            _teamOnlyLoop = StartCoroutine(UpdateTeamCountsLoop());

            return; // Personal/PersonalTeam 로직은 아래로 내리지 않음
        }

        var sorted = workers.OrderByDescending(w => w.IsPlayer).ToList();
        for (int i = 0; i < sorted.Count && i < workerDisplays.Count; i++)
        {
            string displayName = anonymous ? $"M{i}" : sorted[i].WorkerData.WorkerName;
            Debug.Log("Dashboardcontrol " + displayName);
            //workerDisplays[i].SetWorker(sorted[i].WorkerData, displayName);
            workerDisplays[i].SetWorker(sorted[i], displayName);
        }
    }

    public void StartTimer(int seconds, Action onFinished)
    {
        if (timerRoutine != null) StopCoroutine(timerRoutine);
        timerRoutine = StartCoroutine(TimerCo(seconds, onFinished));
    }

    private IEnumerator TimerCo(int total, Action onFinished)
    {
        int t = total;
        while (t >= 0)
        {
            if (timerText != null)
            {
                var ts = TimeSpan.FromSeconds(t);
                timerText.text = $"{ts.Minutes:00}:{ts.Seconds:00}";
            }
            yield return new WaitForSeconds(1f);
            t--;
        }
        onFinished?.Invoke();
    }
    // ---------------- TeamOnly 전용 ----------------

    private void BuildTeamsForTeamOnly()
    {
        _teams.Clear();

        var players = _allWorkers.Where(w => w && w.IsPlayer).ToList();
        var agents  = _allWorkers.Where(w => w && !w.IsPlayer).ToList();

        // 0번: 플레이어 2명
        var team0 = new List<Worker>();
        if (players.Count > 0) team0.Add(players[0]);
        if (players.Count > 1) team0.Add(players[1]);
        _teams.Add(team0);

        // 1~4번: 에이전트 2명씩
        int idx = 0;
        for (int t = 1; t <= 4; t++)
        {
            var team = new List<Worker>();
            if (idx < agents.Count) team.Add(agents[idx++]);
            if (idx < agents.Count) team.Add(agents[idx++]);
            _teams.Add(team);
        }

        while (_teams.Count < 5) _teams.Add(new List<Worker>());
    }

    private IEnumerator UpdateTeamCountsLoop()
    {
        var wait = new WaitForSeconds(0.2f); // 5 FPS
        while (true)
        {
            for (int i = 0; i < 5 && i < workerDisplays.Count; i++)
            {
                int sumBoxes = 0;
                var team = (i < _teams.Count) ? _teams[i] : null;
                if (team != null)
                {
                    foreach (var w in team)
                    {
                        //if (w != null && w.WorkerData != null)
                        var kpi = w.GetComponent<WorkerKpiSync>();
                        sumBoxes += kpi.GetNumBox();
                            //sumBoxes += w.WorkerData.NumBox; // 이름/시간 무시, 박스 개수만
                    }
                }
                workerDisplays[i].SetTeamOnlyBoxes(sumBoxes);
            }
            yield return wait;
        }
    }

    private void EnsureDisplayCount(int min)
    {
        if (workerDisplays == null) workerDisplays = new List<DashboardWorkerDisplay>();
        if (workerDisplays.Count < min)
            Debug.LogWarning($"DashboardController: workerDisplays needs at least {min} items for this layout.");
    }

}

