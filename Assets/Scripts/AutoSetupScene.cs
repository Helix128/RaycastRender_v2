using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoSetupScene : MonoBehaviour
{
    private void Awake()
    {
        foreach(MeshFilter mr in FindObjectsOfType<MeshFilter>())
        {
            Destroy(mr.GetComponent<Collider>());
            mr.gameObject.AddComponent<MeshCollider>().sharedMesh = mr.sharedMesh;
            
        }
    }
}
