using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// - 씬 안의 네트워크 Player Worker를 찾아
///   "{workerName}{groupSuffix}" 그룹의 BoxGrabTracker.playerWorker에 정확히 주입.
/// - 클라이언트에서도 원격(호스트) Worker를 찾아 바인딩.
/// - Fallback: 그룹을 못 찾으면 BoxData.assignedWorkerData.WorkerName으로 매칭.
/// </summary>
public class BoxGrabAutoBinder : MonoBehaviour
{
    [Header("이름 규칙")]
    [Tooltip("박스 그룹 이름은 {workerName} + groupSuffix 와 같아야 함. 예) player_boxes / player2_boxes")]
    public string groupSuffix = "_boxes";

    [Header("대기/재시도")]
    [Tooltip("폴링 간격(초)")]
    public float pollInterval = 0.3f;
    [Tooltip("최대 대기 시간(초, 0이면 무제한)")]
    public float maxWaitSeconds = 12f;

    [Header("대상 워커 이름(선택)")]
    [Tooltip("호스트/클라이언트 워커 이름을 고정하고 싶으면 지정 (비워두면 자동 탐색)")]
    public string hostWorkerName  = "player";
    public string clientWorkerName = "player2";

    [Header("Fallback 옵션")]
    [Tooltip("그룹을 못 찾으면 BoxData.assignedWorkerData.WorkerName으로 매칭 시도")]
    public bool fallbackMatchByAssignedWorkerData = true;

    private bool _assigning = false;

    private void OnEnable()
    {
        StartCoroutine(AssignLoop());

        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnServerStarted            += OnServerStartedHandler;
            nm.OnClientConnectedCallback  += OnClientChangedHandler;
            nm.OnClientDisconnectCallback += OnClientChangedHandler;
        }
    }

    private void OnDisable()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnServerStarted            -= OnServerStartedHandler;
            nm.OnClientConnectedCallback  -= OnClientChangedHandler;
            nm.OnClientDisconnectCallback -= OnClientChangedHandler;
        }
    }

    private void OnServerStartedHandler()                 => StartCoroutine(AssignLoop());
    private void OnClientChangedHandler(ulong _clientId)  => StartCoroutine(AssignLoop());

    private IEnumerator AssignLoop()
    {
        if (_assigning) yield break;
        _assigning = true;

        // 씬 안정화
        yield return null; yield return null;

        // ✅ 클라이언트인 경우, "내 로컬 PlayerObject"가 스폰될 때까지 대기
        var nm = NetworkManager.Singleton;
        if (nm != null && !nm.IsServer)
        {
            float waitedLocal = 0f;
            while ((nm.LocalClient == null) ||
                   (nm.LocalClient.PlayerObject == null) ||
                   !nm.LocalClient.PlayerObject.IsSpawned)
            {
                if (maxWaitSeconds > 0f && waitedLocal >= maxWaitSeconds) break;
                yield return new WaitForSeconds(pollInterval);
                waitedLocal += pollInterval;
            }
        }

        float waited = 0f;
        while (true)
        {
            // 1) 현재 씬의 모든 플레이어 Worker를 수집
            var workers = FindAllPlayerWorkersInScene(); // name -> Worker

            // 2) 바인딩 대상 확정
            Worker hostW   = null;
            Worker clientW = null;

            // 이름이 지정돼 있으면 우선 이름으로
            if (!string.IsNullOrEmpty(hostWorkerName) && workers.TryGetValue(hostWorkerName, out var _h)) hostW = _h;
            if (!string.IsNullOrEmpty(clientWorkerName) && workers.TryGetValue(clientWorkerName, out var _c)) clientW = _c;

            // 이름으로 못 찾았으면, 소유자 기준으로 보조 탐색
            if (nm != null)
            {
                foreach (var kv in workers)
                {
                    var no = kv.Value.GetComponentInParent<NetworkObject>();
                    if (!no) continue;

                    if (hostW == null && no.OwnerClientId == NetworkManager.ServerClientId)
                        hostW = kv.Value;

                    if (clientW == null && no.OwnerClientId != NetworkManager.ServerClientId)
                        clientW = kv.Value;
                }
            }

            // 3) 바인딩 수행 (있는 것만 먼저 붙이고, 둘 다 성공할 때까지 재시도)
            int assignedTotal = 0;
            if (hostW != null)   assignedTotal += AssignForWorkerName(GetWorkerName(hostW), hostW);
            if (clientW != null) assignedTotal += AssignForWorkerName(GetWorkerName(clientW), clientW);

            // 둘 다 그룹에 주입되었는지 확인
            bool hostDone = hostW == null ? true : GroupHasAnyTracker(GetWorkerName(hostW) + groupSuffix);
            bool clientDone = clientW == null ? true : GroupHasAnyTracker(GetWorkerName(clientW) + groupSuffix);

            // ※ 위 확인은 "트래커가 존재"하는지만 보고, 실제 주입 성공은 로그로 확인하세요.
            // 더 엄밀히 하려면 리플렉션으로 각 트래커의 playerWorker를 검사해도 됩니다.

            if ((hostW != null) && (clientW != null) && assignedTotal > 0)
            {
                Debug.Log($"[BoxGrabAutoBinder] 바인딩 시도: host={GetWorkerName(hostW)}, client={GetWorkerName(clientW)} / 이번에 주입 {assignedTotal}개");
            }

            // 최소 한 번은 주입 시도를 하고, 둘 다 잡히면 종료
            if (hostW != null && clientW != null && assignedTotal > 0)
                break;

            if (maxWaitSeconds > 0f && waited >= maxWaitSeconds)
            {
                Debug.LogWarning("[BoxGrabAutoBinder] 제한시간 내에 두 플레이어 모두 바인딩하지 못했습니다. (이름/스폰 타이밍 확인)");
                break;
            }

            yield return new WaitForSeconds(pollInterval);
            waited += pollInterval;
        }

        _assigning = false;
    }

    private string GetWorkerName(Worker w)
    {
        var wd = w ? w.WorkerData : null;
        return (wd != null && !string.IsNullOrEmpty(wd.WorkerName)) ? wd.WorkerName : w.name;
    }

    /// <summary>
    /// 씬에서 "플레이어 Worker"를 수집한다.
    /// - Worker가 루트가 아니어도 OK: 부모에서 NetworkObject 탐색
    /// - IsPlayerObject 또는 WorkerKpiSync 존재로 플레이어 판별
    /// </summary>
    private Dictionary<string, Worker> FindAllPlayerWorkersInScene()
    {
        var result = new Dictionary<string, Worker>();
        var allWorkers = FindObjectsOfType<Worker>(true);

        foreach (var w in allWorkers)
        {
            // ✅ 부모에서 NetworkObject를 찾기 (루트가 아니어도 검출)
            var no = w.GetComponentInParent<NetworkObject>();
            if (no == null || !no.IsSpawned) continue;

            bool isLikelyPlayer =
                no.IsPlayerObject || (w.GetComponentInParent<WorkerKpiSync>() != null);
            if (!isLikelyPlayer) continue;

            var name = GetWorkerName(w);
            if (string.IsNullOrEmpty(name)) continue;

            result[name] = w; // 동명이인 발생 시 마지막 것을 사용
        }

        // 디버그 출력
        if (result.Count > 0)
        {
            string names = string.Join(", ", result.Keys);
            Debug.Log($"[BoxGrabAutoBinder] 탐지된 플레이어: {names}");
        }
        return result;
    }

    /// <summary>
    /// 1) 그룹 이름({workerName}{groupSuffix})으로 먼저 연결
    /// 2) 없으면 Fallback: BoxData.assignedWorkerData.WorkerName 매칭
    /// </summary>
    private int AssignForWorkerName(string workerName, Worker playerWorker)
    {
        int assigned = 0;

        // 1) 그룹 이름으로 찾기
        string groupName = workerName + groupSuffix;
        var groupGo = GameObject.Find(groupName);
        if (groupGo != null)
        {
            var trackers = groupGo.GetComponentsInChildren<BoxGrabTracker>(true);
            foreach (var t in trackers)
            {
                if (SetPlayerWorkerOnTracker(t, playerWorker))
                    assigned++;
            }
            if (trackers.Length == 0)
                Debug.LogWarning($"[BoxGrabAutoBinder] 그룹 '{groupName}' 내 BoxGrabTracker 없음");
        }
        else
        {
            Debug.LogWarning($"[BoxGrabAutoBinder] 그룹 '{groupName}' 오브젝트를 찾지 못함");
        }

        // 2) 그룹이 없거나 주입 0개면 Fallback
        if (assigned == 0 && fallbackMatchByAssignedWorkerData)
        {
            var allTrackers = FindObjectsOfType<BoxGrabTracker>(true);
            foreach (var t in allTrackers)
            {
                var bd = t.GetComponent<BoxData>();
                if (bd != null && bd.assignedWorkerData != null &&
                    bd.assignedWorkerData.WorkerName == workerName)
                {
                    if (SetPlayerWorkerOnTracker(t, playerWorker))
                        assigned++;
                }
            }
        }

        if (assigned > 0)
            Debug.Log($"[BoxGrabAutoBinder] '{workerName}'에게 {assigned}개 트래커 주입 완료.");
        return assigned;
    }

    private bool GroupHasAnyTracker(string groupName)
    {
        var go = GameObject.Find(groupName);
        if (!go) return false;
        return go.GetComponentInChildren<BoxGrabTracker>(true) != null;
    }

    // ---------- Tracker에 안전하게 Worker 주입 ----------
    private static readonly MethodInfo _setter =
        typeof(BoxGrabTracker).GetMethod("SetPlayerWorker",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo _playerWorkerField =
        typeof(BoxGrabTracker).GetField("playerWorker",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private bool SetPlayerWorkerOnTracker(BoxGrabTracker tracker, Worker playerWorker)
    {
        if (tracker == null || playerWorker == null) return false;

        // 1) 세터 우선
        if (_setter != null)
        {
            _setter.Invoke(tracker, new object[] { playerWorker });
            return true;
        }

        // 2) private 직렬필드 직접 주입
        if (_playerWorkerField != null)
        {
            _playerWorkerField.SetValue(tracker, playerWorker);
            return true;
        }

        Debug.LogWarning($"[BoxGrabAutoBinder] {tracker.name}에 playerWorker 주입 실패(세터/필드 없음).");
        return false;
    }
}




// using System.Collections;
// using System.Collections.Generic;
// using System.Reflection;
// using Unity.Netcode;
// using UnityEngine;

// /// <summary>
// /// - 씬 안의 모든 네트워크 Player Worker를 찾아
// ///   "{workerName}{groupSuffix}" 그룹 아래 BoxGrabTracker.playerWorker에 정확히 주입.
// /// - 클라이언트에서도 원격(호스트) Worker를 찾아 바인딩하므로,
// ///   클라 에디터에서 "player_box → player", "player2_box → player2"가 정확히 설정됨.
// /// - Fallback: 그룹을 못 찾으면 BoxData.assignedWorkerData.WorkerName으로 매칭.
// /// </summary>
// public class BoxGrabAutoBinder : MonoBehaviour
// {
//     [Header("이름 규칙")]
//     [Tooltip("박스 그룹 이름은 {workerName} + groupSuffix 와 같아야 함. 예) player_boxes / player2_boxes")]
//     public string groupSuffix = "_boxes";

//     [Header("대기/재시도")]
//     [Tooltip("최초 바인딩/재바인딩 대기 중 폴링 간격(초)")]
//     public float pollInterval = 0.3f;
//     [Tooltip("최대 대기 시간(초) (0이면 무제한 대기)")]
//     public float maxWaitSeconds = 10f;

//     [Header("Fallback 옵션")]
//     [Tooltip("그룹을 못 찾으면 BoxData.assignedWorkerData.WorkerName으로 매칭 시도")]
//     public bool fallbackMatchByAssignedWorkerData = true;

//     // 동시 실행 방지
//     private bool _assigning = false;

//     private void OnEnable()
//     {
//         StartCoroutine(AssignLoop());

//         if (NetworkManager.Singleton != null)
//         {
//             NetworkManager.Singleton.OnServerStarted            += OnNetChange;
//             NetworkManager.Singleton.OnClientConnectedCallback  += _ => OnNetChange();
//             NetworkManager.Singleton.OnClientDisconnectCallback += _ => OnNetChange();
//         }
//     }

//     private void OnDisable()
//     {
//         if (NetworkManager.Singleton != null)
//         {
//             NetworkManager.Singleton.OnServerStarted            -= OnNetChange;
//             NetworkManager.Singleton.OnClientConnectedCallback  -= _ => OnNetChange();
//             NetworkManager.Singleton.OnClientDisconnectCallback -= _ => OnNetChange();
//         }
//     }

//     private void OnNetChange() => StartCoroutine(AssignLoop());

//     private IEnumerator AssignLoop()
//     {
//         if (_assigning) yield break;
//         _assigning = true;

//         // 씬 안정화 2프레임
//         yield return null; yield return null;

//         float waited = 0f;
//         while (true)
//         {
//             // 1) 씬에서 모든 Player Worker 수집 (호스트/클라 모두 동작)
//             var workers = FindAllPlayerWorkersInScene(); // key=workerName, val=Worker
//             if (workers.Count >= 1) // 최소 1명이라도 찾히면 시도(대개 2명 찾힘)
//             {
//                 int sum = 0;
//                 foreach (var kv in workers)
//                 {
//                     sum += AssignForWorkerName(kv.Key, kv.Value);
//                 }

//                 if (sum > 0)
//                 {
//                     Debug.Log($"[BoxGrabAutoBinder] 바인딩 완료: 총 {sum}개 BoxGrabTracker에 주입.");
//                     break; // 한 번이라도 성공했으면 종료(필요하면 주석)
//                 }
//             }

//             if (maxWaitSeconds > 0f && waited >= maxWaitSeconds)
//             {
//                 Debug.LogWarning("[BoxGrabAutoBinder] 제한시간 내 바인딩 실패. 씬/이름 규칙을 확인하세요.");
//                 break;
//             }

//             yield return new WaitForSeconds(pollInterval);
//             waited += pollInterval;
//         }

//         _assigning = false;
//     }

//     /// <summary>
//     /// 씬에서 네트워크로 스폰된 "플레이어 Worker"들을 찾는다.
//     /// - 호스트/클라 모두에서 동작: 원격 플레이어도 클라 씬에 미러링되어 있음
//     /// - WorkerData.WorkerName을 key로 사용
//     /// </summary>
//     private Dictionary<string, Worker> FindAllPlayerWorkersInScene()
//     {
//         var result = new Dictionary<string, Worker>();
//         var allWorkers = FindObjectsOfType<Worker>(true);

//         foreach (var w in allWorkers)
//         {
//             var no = w.GetComponent<NetworkObject>();
//             if (no == null || !no.IsSpawned) continue; // 네트워크 미스폰 제외

//             // 플레이어 판별: PlayerObject거나, Player 프리팹 구조(WorkerKpiSync 존재)로 판단
//             bool isLikelyPlayer =
//                 (no.IsPlayerObject) ||
//                 (w.GetComponent<WorkerKpiSync>() != null);

//             if (!isLikelyPlayer) continue;

//             var wd = w.WorkerData;
//             if (wd == null || string.IsNullOrEmpty(wd.WorkerName)) continue;

//             // 동명이인 방지: 마지막으로 본 인스턴스를 사용(대개 1개)
//             result[wd.WorkerName] = w;
//         }

//         return result;
//     }

//     /// <summary>
//     /// 1) 그룹 이름({workerName}{groupSuffix})으로 먼저 연결
//     /// 2) 없으면 Fallback: BoxData.assignedWorkerData.WorkerName 매칭
//     /// </summary>
//     private int AssignForWorkerName(string workerName, Worker playerWorker)
//     {
//         int assigned = 0;

//         // 1) 그룹 이름으로 찾기 (예: "player_boxes", "player2_boxes")
//         string groupName = workerName + groupSuffix;
//         var groupGo = GameObject.Find(groupName);
//         if (groupGo != null)
//         {
//             var trackers = groupGo.GetComponentsInChildren<BoxGrabTracker>(true);
//             foreach (var t in trackers)
//             {
//                 if (SetPlayerWorkerOnTracker(t, playerWorker))
//                     assigned++;
//             }
//         }

//         // 2) 그룹이 없거나 0개면 Fallback
//         if (assigned == 0 && fallbackMatchByAssignedWorkerData)
//         {
//             var allTrackers = FindObjectsOfType<BoxGrabTracker>(true);
//             foreach (var t in allTrackers)
//             {
//                 var bd = t.GetComponent<BoxData>();
//                 if (bd != null && bd.assignedWorkerData != null &&
//                     bd.assignedWorkerData.WorkerName == workerName)
//                 {
//                     if (SetPlayerWorkerOnTracker(t, playerWorker))
//                         assigned++;
//                 }
//             }
//         }

//         if (assigned > 0)
//             Debug.Log($"[BoxGrabAutoBinder] '{workerName}'에 {assigned}개 트래커 주입({groupName}).");
//         return assigned;
//     }

//     // ---------- Tracker에 안전하게 Worker 주입 ----------

//     private static readonly MethodInfo _setter =
//         typeof(BoxGrabTracker).GetMethod("SetPlayerWorker",
//             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

//     private static readonly FieldInfo _playerWorkerField =
//         typeof(BoxGrabTracker).GetField("playerWorker",
//             BindingFlags.NonPublic | BindingFlags.Instance);

//     private bool SetPlayerWorkerOnTracker(BoxGrabTracker tracker, Worker playerWorker)
//     {
//         if (tracker == null || playerWorker == null) return false;

//         // 1) 공개/비공개 세터 우선
//         if (_setter != null)
//         {
//             _setter.Invoke(tracker, new object[] { playerWorker });
//             return true;
//         }

//         // 2) 마지막 수단: private 직렬필드 직접 주입
//         if (_playerWorkerField != null)
//         {
//             _playerWorkerField.SetValue(tracker, playerWorker);
//             return true;
//         }

//         Debug.LogWarning($"[BoxGrabAutoBinder] {tracker.name}에 playerWorker 주입 실패(세터/필드 없음).");
//         return false;
//     }
// }




