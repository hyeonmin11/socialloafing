using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class inittransform : MonoBehaviour
{
    private Worker w;
    private WorkerData wd;
    private void Awake()
    {
        w = GetComponent<Worker>();
        wd = w.WorkerData;
        wd.InitialPos = transform.position;
        wd.InitialRot = transform.rotation;
    }
}
