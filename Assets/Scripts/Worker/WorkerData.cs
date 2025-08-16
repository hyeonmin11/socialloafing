using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Worker Data", menuName = "Scriptable Object/Worker Data", order = int.MaxValue)]
public class WorkerData : ScriptableObject
{
    [SerializeField]
    private string workerName;
    public string WorkerName
    {
        get { return workerName; }
        set { workerName = value; }
    }
    [SerializeField]
    private float completiontimeperBox = 1.0f;
    public float CompletionTimePerBox { get { return completiontimeperBox; } }
    [SerializeField]
    private int numBox = 16;
    public int NumBox { get { return numBox; } }
    [SerializeField]
    public string spawnPointID;
    public string dropoffLocationID;
    [SerializeField]
    private GameObject avatarPrefab;
    public GameObject AvatarPrefab => avatarPrefab;
    [SerializeField]
    private bool isFinish = false;
    public bool IsFinish
    {
        get { return isFinish; }
        set { isFinish = value; }
    }
    public void Finished()
    {
        isFinish = true;
    }
    public void DecreaseBoxCount()
    {
        numBox = Mathf.Max(0, numBox - 1);
    }
    // public void OverrideName(string newName)
    // {
    //     this.workerName = newName;
    // }
    public void SetCompletionTimePerBox(float sec) { completiontimeperBox = sec; }
    
    private Vector3 initial_pos;
    private Quaternion initial_rot;
    public Vector3 InitialPos
    {
        get => initial_pos;
        set
        {
            // 필요하면 검증/이벤트
            initial_pos = value;
        }
    }

    public Quaternion InitialRot
    {
        get => initial_rot;
        set
        {
            // 0~360 정규화 같은 검증 가능
            initial_rot = value;
        }
    }


    public void ResetKpis()
    {
        numBox = 16;
        completiontimeperBox = 1.0f;
        isFinish = false;
    }


}
