//Deprecated, moved to AIBrain
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class GhostManager : NetworkBehaviour
{
    [Header("Settings")]
    public List<GameObject> ghosts = new List<GameObject>();
    public Transform spawn_point;
    public Transform waypoints;
    // Start is called before the first frame update
    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(TimedGhostSpawn());
    }

    IEnumerator TimedGhostSpawn()
    {
        yield return new WaitForSeconds(16f);
        SpawnGhost(0);
    }
    public void SpawnGhost(int index)
    {

        Vector3 pos = new Vector3(spawn_point.position.x, ghosts[index].transform.position.y + 0.1f, spawn_point.transform.position.z);
        GameObject g = Instantiate(ghosts[0], pos, ghosts[index].transform.rotation);
        NetworkServer.Spawn(g);
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!isServer)
        {
            return;
        }
    }
}
