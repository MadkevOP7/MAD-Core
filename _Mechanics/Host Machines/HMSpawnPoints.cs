//Holder
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HMSpawnPoints : GlobalWaypoint
{
    public override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(Instance);
        }

        Instance = this;

        ProcessPoints();
    }

    public void ProcessPoints()
    {
        groups = new WaypointGroup[1];
        GameObject g = new GameObject("======Waypoint Group (HM Spawn Pts)======");
        g.AddComponent<WaypointGroup>();
        groups[0] = g.GetComponent<WaypointGroup>();
        groups[0].waypoints = new Waypoint[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            Waypoint w = transform.GetChild(i).GetComponent<Waypoint>();
            if (i < transform.childCount - 1)
            {
                w.next = transform.GetChild(i + 1).GetComponent<Waypoint>();
            }
            if (i > 0)
            {
                w.prev = transform.GetChild(i - 1).GetComponent<Waypoint>();
            }

            groups[0].waypoints[i] = w;
        }
    }

    public Waypoint[] GetSpawnPoints() { return groups[0].waypoints; }

    public int GetSpawnPointIndex(Waypoint spawnPoint)
    {
        return GetWaypointIndex(spawnPoint, groups[0]);
    }
}
