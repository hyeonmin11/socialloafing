// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;
// using TMPro;
// using UnityEngine;

// public class DashboardController : MonoBehaviour
// {
//     [SerializeField] private List<DashboardWorkerDisplay> workerDisplays;
//     [SerializeField] private TMP_Text timerText;

//     [SerializeField] private DashboardKind dashboardKind = DashboardKind.Personal;

//     private bool anonymous = false;
//     private Coroutine timerRoutine;

//     // TeamOnly 내부 상태
//     private List<Worker> _allWorkers = new();
//     private List<List<Worker>> _teams = new(); // [0]=플레이어팀, [1..4]=에이전트팀
//     private Coroutine _teamOnlyLoop;

//     private void OnDisable()
//     {
//         if (_teamOnlyLoop != null) { StopCoroutine(_teamOnlyLoop); _teamOnlyLoop = null; }
//         if (timerRoutine != null) { StopCoroutine(timerRoutine);   timerRoutine = null; }
//     }

//     // === 외부 진입점 ===
//     public void SetWorkers(List<Worker> workers, bool isAnonymous)
//     {
//         anonymous   = isAnonymous;
//         _allWorkers = workers ?? new List<Worker>();

//         if (dashboardKind == DashboardKind.TeamOnly)
//         {
//             // TeamOnly: 5팀 집계 표시
//             BuildTeamsForTeamOnly();

//             EnsureDisplayCount(5);

//             // 팀 헤더 설정
//             if (workerDisplays.Count >= 5)
//             {
//                 workerDisplays[0].SetTeamOnlyHeader("Player Team");
//                 workerDisplays[1].SetTeamOnlyHeader("Agent Team 1");
//                 workerDisplays[2].SetTeamOnlyHeader("Agent Team 2");
//                 workerDisplays[3].SetTeamOnlyHeader("Agent Team 3");
//                 workerDisplays[4].SetTeamOnlyHeader("Agent Team 4");
//             }

//             // 루프 재시작
//             if (_teamOnlyLoop != null) StopCoroutine(_teamOnlyLoop);
//             _teamOnlyLoop = StartCoroutine(UpdateTeamCountsLoop());

//             // 개인 모드 표시 남아있다면 정리
//             ClearExtraDisplays(startIndex: 5);
//             return;
//         }

//         // 개인/개인+팀 모드: 가장 앞에 플레이어들 먼저 오도록 정렬
//         if (_teamOnlyLoop != null) { StopCoroutine(_teamOnlyLoop); _teamOnlyLoop = null; }

//         var sorted = _allWorkers
//             .Where(w => w != null)
//             .OrderByDescending(w => w.IsPlayer)
//             .ToList();

//         // 채우기
//         for (int i = 0; i < workerDisplays.Count; i++)
//         {
//             if (i < sorted.Count)
//             {
//                 var w = sorted[i];
//                 string displayName = anonymous ? $"M{i}" : (w.WorkerData != null ? w.WorkerData.WorkerName : $"W{i}");
//                 // 멀티 호환: Worker를 넘기면 내부에서 WorkerKpiSync 자동 사용
//                 workerDisplays[i].SetWorker(w, displayName);
//             }
//             else
//             {
//                 workerDisplays[i].Clear();
//             }
//         }
//     }

//     public void StartTimer(int seconds, Action onFinished)
//     {
//         if (timerRoutine != null) StopCoroutine(timerRoutine);
//         timerRoutine = StartCoroutine(TimerCo(seconds, onFinished));
//     }

//     private IEnumerator TimerCo(int total, Action onFinished)
//     {
//         int t = total;
//         while (t >= 0)
//         {
//             if (timerText != null)
//             {
//                 var ts = TimeSpan.FromSeconds(t);
//                 timerText.text = $"{ts.Minutes:00}:{ts.Seconds:00}";
//             }
//             yield return new WaitForSeconds(1f);
//             t--;
//         }
//         onFinished?.Invoke();
//     }

//     // ---------------- TeamOnly 전용 ----------------

//     private void BuildTeamsForTeamOnly()
//     {
//         _teams.Clear();

//         var players = _allWorkers.Where(w => w && w.IsPlayer).ToList();
//         var agents  = _allWorkers.Where(w => w && !w.IsPlayer).ToList();

//         // 0번: 플레이어 2명
//         var team0 = new List<Worker>();
//         if (players.Count > 0) team0.Add(players[0]);
//         if (players.Count > 1) team0.Add(players[1]);
//         _teams.Add(team0);

//         // 1~4번: 에이전트 2명씩
//         int idx = 0;
//         for (int t = 1; t <= 4; t++)
//         {
//             var team = new List<Worker>();
//             if (idx < agents.Count) team.Add(agents[idx++]);
//             if (idx < agents.Count) team.Add(agents[idx++]);
//             _teams.Add(team);
//         }

//         while (_teams.Count < 5) _teams.Add(new List<Worker>());
//     }

//     private IEnumerator UpdateTeamCountsLoop()
//     {
//         var wait = new WaitForSeconds(0.2f); // 5 FPS
//         while (true)
//         {
//             for (int i = 0; i < 5 && i < workerDisplays.Count; i++)
//             {
//                 int sumBoxes = 0;
//                 var team = (i < _teams.Count) ? _teams[i] : null;

//                 if (team != null)
//                 {
//                     foreach (var w in team)
//                     {
//                         if (w == null) continue;

//                         // 멀티 동기화 우선
//                         var kpi = w.GetComponent<WorkerKpiSync>();
//                         if (kpi != null)
//                         {
//                             sumBoxes += kpi.GetNumBox();
//                         }
//                         else if (w.WorkerData != null) // 싱글/폴백
//                         {
//                             sumBoxes += w.WorkerData.NumBox;
//                         }
//                     }
//                 }
//                 workerDisplays[i].SetTeamOnlyBoxes(sumBoxes);
//             }
//             yield return wait;
//         }
//     }

//     // ---------------- 유틸 ----------------

//     private void EnsureDisplayCount(int min)
//     {
//         if (workerDisplays == null) workerDisplays = new List<DashboardWorkerDisplay>();
//         if (workerDisplays.Count < min)
//             Debug.LogWarning($"DashboardController: workerDisplays needs at least {min} items for this layout.");
//     }

//     private void ClearExtraDisplays(int startIndex)
//     {
//         if (workerDisplays == null) return;
//         for (int i = startIndex; i < workerDisplays.Count; i++)
//             workerDisplays[i].Clear();
//     }
// }



using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;   
using System.Linq;
public class DashboardController : MonoBehaviour
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
            _allWorkers = workers ?? new List<Worker>();
            BuildTeamsForTeamOnly();

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
            return;
        }

        // ------- Personal(개인 카드) 정렬: Player 다음에 Mia가 오도록 -------
        var list = (workers ?? new List<Worker>()).Where(w => w != null).ToList();
        var player = list.FirstOrDefault(w => w.IsPlayer);
        var mia    = FindWorkerByName(list, "Mia");

        // 기본: 플레이어 우선 내림차순
        var sorted = list.OrderByDescending(w => w.IsPlayer).ToList();

        // 플레이어 다음에 Mia를 배치(둘 다 있을 때, Mia가 이미 2번째가 아니면 재배치)
        if (player != null && mia != null && mia != player)
        {
            // 일단 Mia 제거 후
            sorted.Remove(mia);

            // 플레이어 인덱스 바로 다음에 삽입
            int pIndex = sorted.IndexOf(player);
            if (pIndex < 0) pIndex = 0;
            int targetIndex = Mathf.Min(pIndex + 1, sorted.Count);
            sorted.Insert(targetIndex, mia);
        }

        // UI 반영
        for (int i = 0; i < sorted.Count && i < workerDisplays.Count; i++)
        {
            string displayName = anonymous ? $"M{i}" : sorted[i].WorkerData.WorkerName;
            workerDisplays[i].SetWorker(sorted[i].WorkerData, displayName);
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

        // --- 싱글 모드 가정: 플레이어 1명 ---
        var player = (players.Count > 0) ? players[0] : null;
        var mia    = FindWorkerByName(_allWorkers, "Mia");

        // 0번 팀: 플레이어 + Mia (있으면)
        var team0 = new List<Worker>();
        if (player != null) team0.Add(player);
        if (mia != null && mia != player) team0.Add(mia);
        _teams.Add(team0);

        // 에이전트 풀에서 Mia를 제거(중복 배치 방지)
        if (mia != null) agents.Remove(mia);

        // 1~4번: 남은 에이전트를 2명씩 채우기
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
                        if (w != null && w.WorkerData != null)
                            sumBoxes += w.WorkerData.NumBox; // 이름/시간 무시, 박스 개수만
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

    private static Worker FindWorkerByName(IEnumerable<Worker> list, string name)
    {
        if (list == null) return null;
        return list.FirstOrDefault(w =>
            w != null &&
            w.WorkerData != null &&
            string.Equals(w.WorkerData.WorkerName, name, System.StringComparison.OrdinalIgnoreCase));
    }


}
