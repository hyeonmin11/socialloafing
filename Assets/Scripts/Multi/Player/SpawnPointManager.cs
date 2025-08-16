using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components; // NetworkTransform
using UnityEngine;

public class SpawnPointManager : NetworkBehaviour
{
    [Header("Exactly 2 spawn points (0 = Host, 1 = Client)")]
    [SerializeField] private List<Transform> startPoints = new List<Transform>(2); // [0], [1]

    [Header("Resources/WorkerData/... (asset names)")]
    [SerializeField] private string hostWorkerDataName   = "player";
    [SerializeField] private string clientWorkerDataName = "player2";

    [Header("Scene reference")]
    [SerializeField] private WorkerManager workerManager; // Manager 오브젝트의 WorkerManager

    // 플레이어 프리팹에 붙여도 호스트의 자기 인스턴스 1개만 동작
    private static bool s_registeredOnce;

    // 이미 처리한 클라
    private readonly HashSet<ulong> _assigned = new HashSet<ulong>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { enabled = false; return; }

        // 호스트의 "자기 플레이어" 인스턴스만 매니저 역할
        if (OwnerClientId != NetworkManager.ServerClientId) { enabled = false; return; }
        if (s_registeredOnce) { enabled = false; return; }
        s_registeredOnce = true;

        if (workerManager == null)
            workerManager = FindObjectOfType<WorkerManager>();

        // 안전 가드
        if (workerManager == null)
        {
            Debug.LogError("[SpawnPointManager] WorkerManager reference is missing.");
            enabled = false; return;
        }
        if (startPoints == null || startPoints.Count < 2 || startPoints[0] == null || startPoints[1] == null)
        {
            Debug.LogError("[SpawnPointManager] Please assign exactly 2 start points in the inspector.");
            enabled = false; return;
        }

        NetworkManager.OnClientConnectedCallback  += OnClientConnected;
        //NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

        // Host 자신 처리: 슬롯 0 + player1 + 서버측 등록
        AssignFor(NetworkManager.ServerClientId, 0, hostWorkerDataName);
        // 호스트는 자기 로컬 환경이므로 클라용 목록 전송은 필요 없음
    }

    private void OnDestroy()
    {
        if (IsServer && OwnerClientId == NetworkManager.ServerClientId && s_registeredOnce)
        {
            s_registeredOnce = false;
            if (NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback  -= OnClientConnected;
                //NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.ServerClientId) return; // 호스트는 이미 처리
        // 두 번째 접속자: 슬롯 1 + player2
        AssignFor(clientId, 1, clientWorkerDataName);

        // 새 클라에게 현재 접속 중인 "모든" 플레이어(호스트 포함)를 보내서
        // 해당 클라 로컬 WorkerManager에도 둘 다 등록
        SendAllWorkersToClient(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _assigned.Remove(clientId);
        // 서버쪽 리스트 정리(WorkerManager에 Unregister가 있다면 여기서 호출)
        if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var cc) && cc.PlayerObject != null)
        {
            var worker = cc.PlayerObject.GetComponent<Worker>();
            if (worker != null && workerManager != null)
            {
                // 선택: workerManager.UnregisterWorker(worker);
            }
        }
        // 클라이언트 쪽 리스트 정리는 각 클라의 despawn 처리/널 프루닝 로직에 맡김
    }

    private void AssignFor(ulong clientId, int index, string workerDataName)
    {
        if (_assigned.Contains(clientId)) return;
        index = Mathf.Clamp(index, 0, 1);

        var sp = startPoints[index];
        if (sp == null) return;

        _assigned.Add(clientId);

        // 대상 클라 지정 파라미터 (한 번 선언, 재사용)
        var targetParams = new ClientRpcParams {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };

        // 1) 해당 클라만 텔레포트
        AssignSpawnClientRpc(sp.position, sp.rotation, targetParams);

        // 2) WorkerData 할당(서버/해당 클라)
        SetWorkerDataServerSide(clientId, workerDataName);
        AssignWorkerDataClientRpc(workerDataName, targetParams);

        // 3) 서버의 WorkerManager 등록
        RegisterWorkerServerSide(clientId);
    }

    [ClientRpc]
    private void AssignSpawnClientRpc(Vector3 pos, Quaternion rot, ClientRpcParams rpcParams = default)
    {
        var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (player == null) return;

        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;

        if (player.TryGetComponent<NetworkTransform>(out var nt) && nt.IsSpawned)
            nt.Teleport(pos, rot, player.transform.localScale);
        else
            player.transform.SetPositionAndRotation(pos, rot);

        if (cc) cc.enabled = true;
    }

    [ClientRpc]
    private void AssignWorkerDataClientRpc(string workerDataName, ClientRpcParams rpcParams = default)
    {
        var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (player == null) return;

        var worker = player.GetComponent<Worker>();
        if (worker == null) return;

        var data = Resources.Load<WorkerData>($"WorkerData/{workerDataName}");
        if (data == null)
        {
            Debug.LogWarning($"[SpawnPointManager] (Client) WorkerData not found: Resources/WorkerData/{workerDataName}");
            return;
        }
        worker.WorkerData = data;
    }

    private void SetWorkerDataServerSide(ulong clientId, string workerDataName)
    {
        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var cc)) return;
        var playerObj = cc.PlayerObject;
        if (playerObj == null) return;

        var worker = playerObj.GetComponent<Worker>();
        if (worker == null) return;

        var data = Resources.Load<WorkerData>($"WorkerData/{workerDataName}");
        if (data == null)
        {
            Debug.LogWarning($"[SpawnPointManager] (Server) WorkerData not found: Resources/WorkerData/{workerDataName}");
            return;
        }
        worker.WorkerData = data;
    }

    private void RegisterWorkerServerSide(ulong clientId)
    {
        if (workerManager == null) return;
        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var cc)) return;
        var playerObj = cc.PlayerObject;
        if (playerObj == null) return;

        var worker = playerObj.GetComponent<Worker>();
        if (worker == null) return;

        workerManager.RegisterWorker(worker);
    }

    // ------ 새로 들어온 클라에게 현재 접속자 전부를 보내 로컬 WorkerManager에 등록 ------
    private void SendAllWorkersToClient(ulong targetClientId)
    {
        var list = new List<NetworkObjectReference>();
        foreach (var c in NetworkManager.ConnectedClientsList)
        {
            if (c?.PlayerObject != null)
                list.Add(new NetworkObjectReference(c.PlayerObject));
        }

        var targetParams = new ClientRpcParams {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { targetClientId } }
        };
        RegisterWorkersClientRpc(list.ToArray(), targetParams);
    }

    [ClientRpc]
    private void RegisterWorkersClientRpc(NetworkObjectReference[] workerRefs, ClientRpcParams rpcParams = default)
    {
        StartCoroutine(RegisterWorkersWhenReady(workerRefs));
    }

    private IEnumerator RegisterWorkersWhenReady(NetworkObjectReference[] workerRefs)
    {
        // WorkerManager가 늦게 뜨는 타이밍 대비(최대 2초 대기)
        WorkerManager wm = null;
        float t = 0f;
        while (wm == null && t < 2f)
        {
            wm = FindObjectOfType<WorkerManager>();
            if (wm != null) break;
            t += Time.deltaTime;
            yield return null;
        }
        if (wm == null) yield break;

        foreach (var wr in workerRefs)
        {
            if (wr.TryGet(out var no))
            {
                var worker = no.GetComponent<Worker>();
                if (worker != null)
                    wm.RegisterWorker(worker);
            }
        }
    }
}



// using System.Collections.Generic;
// using Unity.Netcode;
// using Unity.Netcode.Components; // NetworkTransform / (직접 만든) ClientNetworkTransform
// using UnityEngine;

// public class SpawnPointManager : NetworkBehaviour
// {
//     [Header("Exactly 2 spawn points (0 = Host, 1 = Client)")]
//     [SerializeField] private List<Transform> startPoints = new List<Transform>(2); // [0], [1] 인스펙터에 지정

//     [Header("Resources/WorkerData/...  (asset names)")]
//     [SerializeField] private string hostWorkerDataName = "player1";
//     [SerializeField] private string clientWorkerDataName = "player2";

//     // 플레이어 프리팹에 붙여도 호스트의 자기 인스턴스 1개만 동작하게
//     private static bool s_registeredOnce;

//     // 이미 자리 배정된 클라이언트 목록
//     private readonly HashSet<ulong> _assigned = new HashSet<ulong>();

//     [SerializeField] private WorkerManager workerManager;

//     public override void OnNetworkSpawn()
//     {
//         if (!IsServer) { enabled = false; return; }

//         // 호스트의 로컬 플레이어 인스턴스만 매니저 역할
//         if (OwnerClientId != NetworkManager.ServerClientId) { enabled = false; return; }

//         if (s_registeredOnce) { enabled = false; return; }
//         s_registeredOnce = true;

//         workerManager = FindObjectOfType<WorkerManager>();


//         // 안전 가드
//         if (startPoints == null || startPoints.Count < 2 || startPoints[0] == null || startPoints[1] == null)
//         {
//             Debug.LogError("[SpawnPointManager] Please assign exactly 2 start points in the inspector.");
//             enabled = false;
//             return;
//         }

//         NetworkManager.OnClientConnectedCallback += OnClientConnected;

//         // Host 자신을 즉시 슬롯 0에 배치 + player1 할당
//         AssignFor(NetworkManager.ServerClientId, 0, hostWorkerDataName);
//     }

//     private void OnDestroy()
//     {
//         if (IsServer && OwnerClientId == NetworkManager.ServerClientId && s_registeredOnce)
//         {
//             s_registeredOnce = false;
//             if (NetworkManager != null)
//                 NetworkManager.OnClientConnectedCallback -= OnClientConnected;
//         }
//     }

//     private void OnClientConnected(ulong clientId)
//     {
//         if (clientId == NetworkManager.ServerClientId) return; // 호스트는 이미 처리

//         // 두 번째 접속자 → 슬롯 1 + player2
//         AssignFor(clientId, 1, clientWorkerDataName);
//         SendAllWorkersToClient(clientId);
//     }

//     private void AssignFor(ulong clientId, int index, string workerDataName)
//     {
//         if (_assigned.Contains(clientId)) return;
//         index = Mathf.Clamp(index, 0, 1);

//         var sp = startPoints[index];
//         if (sp == null) return;

//         _assigned.Add(clientId);

//         // 해당 클라만 텔레포트
//         var send = new ClientRpcParams
//         {
//             Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
//         };
//         AssignSpawnClientRpc(sp.position, sp.rotation, send);

//         // WorkerData 할당(서버/클라 모두)
//         SetWorkerDataServerSide(clientId, workerDataName);
//         AssignWorkerDataClientRpc(workerDataName, send);

//         RegisterWorkerServerSide(clientId);
//         if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var cc) && cc.PlayerObject != null)
//         {
//             send = new ClientRpcParams
//             {
//                 Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
//             };
//             var workerRef = new NetworkObjectReference(cc.PlayerObject);
//             RegisterWorkerClientRpc(workerRef, send);
//         }
//     }

//     [ClientRpc]
//     private void AssignSpawnClientRpc(Vector3 pos, Quaternion rot, ClientRpcParams rpcParams = default)
//     {
//         var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
//         if (player == null) return;

//         // 텔레포트 시 CC를 잠깐 껐다 켜면 XR/CC가 위치 덮어쓰는 문제 감소
//         var cc = player.GetComponent<CharacterController>();
//         if (cc) cc.enabled = false;

//         if (player.TryGetComponent<NetworkTransform>(out var nt) && nt.IsSpawned)
//         {
//             nt.Teleport(pos, rot, player.transform.localScale);
//         }
//         else
//         {
//             // (직접 구현한) ClientNetworkTransform를 쓰는 경우도 Teleport 가능
//             var cnt = player.GetComponent<NetworkTransform>(); // CNT가 NT 상속이므로 동일 호출
//             if (cnt != null)
//                 cnt.Teleport(pos, rot, player.transform.localScale);
//             else
//                 player.transform.SetPositionAndRotation(pos, rot);
//         }

//         if (cc) cc.enabled = true;
//     }

//     [ClientRpc]
//     private void AssignWorkerDataClientRpc(string workerDataName, ClientRpcParams rpcParams = default)
//     {
//         var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
//         if (player == null) return;

//         var worker = player.GetComponent<Worker>();
//         if (worker == null) return;

//         var data = Resources.Load<WorkerData>($"WorkerData/{workerDataName}");
//         if (data == null)
//         {
//             Debug.LogWarning($"[SpawnPointManager] (Client) WorkerData not found: Resources/WorkerData/{workerDataName}");
//             return;
//         }
//         worker.WorkerData = data;
//     }

//     private void SetWorkerDataServerSide(ulong clientId, string workerDataName)
//     {
//         if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var cc)) return;
//         var playerObj = cc.PlayerObject;
//         if (playerObj == null) return;

//         var worker = playerObj.GetComponent<Worker>();
//         if (worker == null) return;

//         var data = Resources.Load<WorkerData>($"WorkerData/{workerDataName}");
//         if (data == null)
//         {
//             Debug.LogWarning($"[SpawnPointManager] (Server) WorkerData not found: Resources/WorkerData/{workerDataName}");
//             return;
//         }
//         worker.WorkerData = data;
//     }

//     private void RegisterWorkerServerSide(ulong clientId)
//     {
//         if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var cc)) return;
//         var playerObj = cc.PlayerObject;
//         if (playerObj == null) return;

//         var worker = playerObj.GetComponent<Worker>();
//         if (worker == null) return;

//         workerManager.RegisterWorker(worker);
//     }
//     [ClientRpc]
//     private void RegisterWorkerClientRpc(NetworkObjectReference workerRef, ClientRpcParams rpcParams = default)
//     {
//         if (!workerRef.TryGet(out var no)) return;
//         var worker = no.GetComponent<Worker>();
//         var wm = FindObjectOfType<WorkerManager>();
//         if (worker != null && wm != null)
//             wm.RegisterWorker(worker);
//     }
//     private void SendAllWorkersToClient(ulong targetClientId)
//     {
//         var list = new List<NetworkObjectReference>();
//         foreach (var c in NetworkManager.ConnectedClientsList)
//         {
//             if (c?.PlayerObject != null)
//                 list.Add(new NetworkObjectReference(c.PlayerObject));
//         }

//         var targetParams = new ClientRpcParams
//         {
//             Send = new ClientRpcSendParams { TargetClientIds = new[] { targetClientId } }
//         };
//         RegisterWorkersClientRpc(list.ToArray(), targetParams);
//     }
//     [ClientRpc]
//     private void RegisterWorkersClientRpc(NetworkObjectReference[] workerRefs, ClientRpcParams rpcParams = default)
//     {
//         StartCoroutine(RegisterWorkersWhenReady(workerRefs));
//     }

//     private IEnumerator RegisterWorkersWhenReady(NetworkObjectReference[] workerRefs)
//     {
//         // WorkerManager가 늦게 뜨는 타이밍 대비(최대 2초 대기)
//         WorkerManager wm = null;
//         float t = 0f;
//         while (wm == null && t < 2f)
//         {
//             wm = FindObjectOfType<WorkerManager>();
//             if (wm != null) break;
//             t += Time.deltaTime;
//             yield return null;
//         }
//         if (wm == null) yield break;

//         foreach (var wr in workerRefs)
//         {
//             if (wr.TryGet(out var no))
//             {
//                 var worker = no.GetComponent<Worker>();
//                 if (worker != null)
//                     wm.RegisterWorker(worker);
//             }
//         }
//     }
// }


// using Unity.Netcode;
// using UnityEngine;
// using System.Collections.Generic;
// using Unity.Netcode.Components;
// public class SpawnPointManager : NetworkBehaviour
// {
//     [SerializeField] private List<Transform> startPoints = new(); // 0,1,...

//     private readonly List<ulong> joined = new();

//     public override void OnNetworkSpawn()
//     {
//         if (!IsServer) { enabled = false; return; }
//         NetworkManager.OnClientConnectedCallback += OnClientConnected;
//     }

//     private void OnDestroy()
//     {
//         if (NetworkManager != null)
//             NetworkManager.OnClientConnectedCallback -= OnClientConnected;
//     }

//     private void OnClientConnected(ulong clientId)
//     {
//         joined.Add(clientId);
//         int idx = Mathf.Clamp(joined.Count - 1, 0, startPoints.Count - 1);

//         var sp = startPoints[idx];
//         if (sp == null) return;

//         // 🔹 서버가 직접 위치를 바꾸는 대신, 해당 클라에게만 "그 자리로 텔레포트해"라고 지시
//         var send = new ClientRpcParams {
//             Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
//         };
//         AssignSpawnClientRpc(sp.position, sp.rotation, send);
//     }

//     [ClientRpc]
//     private void AssignSpawnClientRpc(Vector3 pos, Quaternion rot, ClientRpcParams rpcParams = default)
//     {
//         // 이 클라의 로컬 플레이어만 움직인다
//         var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
//         if (player == null) return;

//         // ClientNetworkTransform/NetworkTransform에 동일하게 동작
//         var nt = player.GetComponent<NetworkTransform>();
//         if (nt != null)
//         {
//             nt.Teleport(pos, rot, player.transform.localScale);
//         }
//         else
//         {
//             // 혹시 NT가 없다면 마지막 안전망
//             player.transform.SetPositionAndRotation(pos, rot);
//         }

//         // CharacterController 쓰면 순간 텔레포트 전에 비활성→이동→재활성 권장
//         // var cc = player.GetComponent<CharacterController>();
//         // if (cc) { cc.enabled = false; player.transform.SetPositionAndRotation(pos, rot); cc.enabled = true; }
//     }
// }



// using System.Collections.Generic;
// using Unity.Netcode;
// using UnityEngine;

// public class SpawnPointManager : NetworkBehaviour
// {
//     [SerializeField] private List<Transform> startPoints = new(); // 0, 1에 각각 배치

//     private readonly List<ulong> joined = new();

//     public override void OnNetworkSpawn()
//     {
//         if (!IsServer) { enabled = false; return; }
//         NetworkManager.OnClientConnectedCallback += OnClientConnected;
//     }
//     private void OnDestroy()
//     {
//         if (NetworkManager != null)
//             NetworkManager.OnClientConnectedCallback -= OnClientConnected;
//     }
//     private void OnClientConnected(ulong clientId)
//     {
//         joined.Add(clientId);
//         int idx = Mathf.Clamp(joined.Count - 1, 0, startPoints.Count - 1);

//         if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var cc)) return;
//         var playerObj = cc.PlayerObject;
//         if (playerObj == null) return;

//         var sp = startPoints[idx];
//         if (sp != null)
//             playerObj.transform.SetPositionAndRotation(sp.position, sp.rotation);
//     }
// }

