using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class TerrorZone : NetworkBehaviour
{
    [Header("Audio")]
    public AudioSource m_heartbeat;
    public Player player;

    private List<uint> ghosts = new List<uint>();

    private void Update()
    {
        if (isLocalPlayer)
        {
            ProcessHeartbeat();
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "TerrorZone"&&!player.isKiller&&isLocalPlayer)
        {
            AddGhost(other.GetComponentInParent<NetworkIdentity>().netId);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if(other.tag == "TerrorZone"&&!player.isKiller&&isLocalPlayer)
        {
            RemoveGhost(other.GetComponentInParent<NetworkIdentity>().netId);
        }
    }

    public void ProcessHeartbeat()
    {
        //Process null/disconnected ghosts
        for (int i = ghosts.Count - 1; i >= 0; --i)
        {
            if (!NetworkClient.spawned.ContainsKey(ghosts[i]))
            {
                ghosts.RemoveAt(i);
            }
        }
        if (ghosts.Count == 0)
        {
            m_heartbeat.Stop();
            return;
        }
        if (!m_heartbeat.isPlaying)
        {
            m_heartbeat.Play();
        }
    }

    public void AddGhost(uint g)
    {
        foreach(uint i in ghosts)
        {
            if (i == g)
            {
                return;
            }
        }
        ghosts.Add(g);
    }

    public void RemoveGhost(uint g)
    {
        for (int i = ghosts.Count - 1; i >= 0; --i)
        {
            if (ghosts[i] == g)
            {
                ghosts.RemoveAt(i);
                break;
            }
        }
    }
}
