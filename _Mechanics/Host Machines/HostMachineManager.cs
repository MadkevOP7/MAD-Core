//Host Machine Spawning © MADKEV Studio
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
public class HostMachineManager : NetworkBehaviour
{
    //Runs on start on hosting player to spawn killer's life support

    [Header("Components")]
    public HMSpawnPoints hmSpawnPoints;
    private List<Transform> spawn_locations = new List<Transform>();
    private List<Transform> final_spawn_locations = new List<Transform>();
    public int spawn_count = 5; //Currently total of 5 host machines
    public GameObject prefab;
    public static List<HostMachine> host_Machines = new List<HostMachine>();

    [Header("Settings")]
    public float v_stack_range; //x and z sensitivity to prevent vertical stacking
    public float min_h_distance = 17f;
    [SyncVar]
    public int destroy_count = 3; //# of host machines to destroy equals number of players
    public bool allDestroyed = false;

    #region Game State Refresh
    //Sets allDestroyed to true if destroy_count number of hostmachiens are destroyed
    [Command(requiresAuthority =false)]
    public void RefreshHostMachineStatus()
    {
        
        int n = 0;
        foreach(HostMachine h in host_Machines)
        {
            if (!h.isAlive)
            {
                n++;
            }
        }
        if (n >= destroy_count)
        {
            allDestroyed = true;
            GetComponent<GameManager>().SetWinState(1); //Survivors win, destroyed all host machines
        }
    }
    #endregion
    //Initiliaze Destroy Count
    [Command(requiresAuthority = false)]
    public void CMDSetDestroyCount(int c)
    {
        destroy_count = c;
    }
    //Central Initializing
    public void InitiliazeHostMachines()
    {
        allDestroyed = false;
        if (hmSpawnPoints == null)
        {
            Debug.Log("Error Getting HM Spawn Points Root");
            return;
        }
        spawn_locations.Clear();
        foreach(Transform child in hmSpawnPoints.transform)
        {
            if (child != hmSpawnPoints.transform)
            {
                spawn_locations.Add(child);
            }
        }
        ServerSpawnMachines();
    }
    public override void OnStartServer()
    {
        base.OnStartServer();

        if (SceneManager.GetActiveScene().name != "Lobby")
        {
            Debug.Log("Starting Host Machine Server");

            InitiliazeHostMachines();
        }
        

    }
    

    //Randomly chooses spawn locations
    public void RandomSpawnLocations()
    {
        Transform tempGO;
        for (int i = 0; i < spawn_locations.Count; i++)
        {
            int rnd = Random.Range(0, spawn_locations.Count);
            tempGO = spawn_locations[rnd];
            spawn_locations[rnd] = spawn_locations[i];
            spawn_locations[i] = tempGO;
        }
    }

    public void ServerSpawnMachines()
    {
        RandomSpawnLocations();
        for (int k = 0; k < spawn_locations.Count; k++)
        {
            if (QualityLocation(spawn_locations[k]))
            {
                final_spawn_locations.Add(spawn_locations[k]);
            }
        }
        Debug.Log("Balanced Spawn Possible Locations: " + final_spawn_locations.Count);
        if (final_spawn_locations.Count < spawn_count)
        {
            Debug.Log("Warning: System wasn't able to randomize and balance machines up to Spawn Count");
            Debug.Log("Need Backup Fill " + (spawn_count - final_spawn_locations.Count) + " spawn points");
            //Backup fill
            while (final_spawn_locations.Count < spawn_count)
            {
                foreach(Transform t in spawn_locations)
                {
                    if (!final_spawn_locations.Contains(t))
                    {
                        final_spawn_locations.Add(t);
                    }
                }
            }
            Debug.Log("Backup completed new count is " + final_spawn_locations.Count);
        }
        for (int i = 0; i < spawn_count; i++)
        {
           
            if (i < final_spawn_locations.Count)
            {
                HostMachine hm = Instantiate(prefab, final_spawn_locations[i].position, final_spawn_locations[i].rotation).GetComponent<HostMachine>();
                NetworkServer.Spawn(hm.gameObject);
                hm.hm = this;
                host_Machines.Add(hm);
            }
        }

        Debug.Log("Host Machine spawn success");
    }

    public bool QualityLocation(Transform t)
    {
        foreach(Transform temp in final_spawn_locations)
        {
            //Check vertical stacking
            if (Mathf.Abs(temp.position.z - t.position.z) < v_stack_range || Mathf.Abs(temp.position.x - t.position.x) < v_stack_range)
            {
                Debug.Log(t.transform.name + " fails: Vertical Stacking");
                return false;
            }

            //Check for ray passing (same area)
            if (!Physics.Linecast(temp.position, t.position))
            {
                Debug.Log(t.transform.name + " fails: Same area");
                return false;
            }
            float xDiff = temp.position.x - t.position.x;
            float zDiff = temp.position.z - t.position.z;
            if(Mathf.Sqrt((xDiff * xDiff) + (zDiff * zDiff)) < min_h_distance)
            {
                Debug.Log(t.transform.name + " fails: too close");
                return false;
            }
                
        }
        Debug.Log(t.transform.name + " passes checks");
        return true;
    }
    [Command(requiresAuthority = false)]
    public void EnableOutlines(GameObject target)
    {
        foreach(HostMachine h in host_Machines)
        {
            h.CMDOutlineChange(target, true);
        }
    }


}
