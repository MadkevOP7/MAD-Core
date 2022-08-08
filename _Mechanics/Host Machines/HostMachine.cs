using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.Video;
using UnityEngine.Timeline;
using UnityEngine.Playables;

public class HostMachine : NetworkBehaviour
{
    [Header("Host Manager")]
    public HostMachineManager hm;
    public VideoPlayer screen1;
    public VideoPlayer screen2;
    public PlayableDirector timeline;
    public Animator anim;

    //Balance settings
    float repair =0.0086f;
    float damage = 0.01f;

    [Header("Control")]
    [SyncVar(hook =nameof(MachineKilled))]
    public bool isAlive = true;
   
    [Header("Storage")]
    [SyncVar]
    public float machine_Health = 100f;

    [Header("Audios")]
    public AudioSource audioSource;
    public AudioClip errorAudio;
    public AudioClip shutdownAudio;

    List<uint> damagers = new List<uint>(); //Store network identity of damager
    List<uint> repairs = new List<uint>(); //Store network identity of damager

    uint null_id = 892;
    //Player iD 892 = null iD;
    public void MachineKilled(bool oldVal, bool newVal)
    {
        if (!newVal)
        {
            Debug.Log(gameObject.name + " is shutdown");
            //Freeze
            isAlive = false;
            anim.enabled = false;
            timeline.Pause();
            //
            Material t = new Material(screen1.GetComponent<MeshRenderer>().material);
            t.EnableKeyword("_EMISSION");
            screen1.GetComponent<MeshRenderer>().material = t;
            screen2.GetComponent<Animator>().enabled = true;
            audioSource.clip = shutdownAudio;
            audioSource.volume = 0.5f;
            audioSource.Play();
            StartCoroutine(EnableObserverServerErrorRepeat());

            //Enable outline in yellow for all host clients
            GetComponent<Outline>().enabled = true;
            GetComponent<Outline>().OutlineColor = Color.yellow;

            //Call host machine manager to refresh status
            GameObject.FindGameObjectWithTag("Game Manager").GetComponent<HostMachineManager>().RefreshHostMachineStatus();
        }
        
    }

    IEnumerator EnableObserverServerErrorRepeat()
    {
        yield return new WaitForSeconds(3);
        audioSource.clip = errorAudio;
        audioSource.loop = true;
        audioSource.Play();
    }
    //Machine Damage and Repair
    [Command(requiresAuthority = false)]
    public void DamageMachine(uint id)
    {
        if (id == null_id) { return; }
        if (!damagers.Contains(id))
        {
            damagers.Add(id);
        }
    }

    [Command(requiresAuthority =false)]
    public void ChangeHealth(float v)
    {
        if (isAlive)
        {
            if (machine_Health <= 100 && machine_Health >= 0)
            {
                machine_Health += v;
            }
            if (machine_Health >= 100)
            {
                machine_Health = 100;
            }
            if (machine_Health <= 0)
            {
                isAlive = false;
                machine_Health = 0;
            }
        }
    }

    [Command(requiresAuthority = false)]
    public void RepairMachine(uint id)
    {
        if (id == null_id) { return; }
        if (!repairs.Contains(id))
        {
            repairs.Add(id);
        }
    }
    [Command(requiresAuthority = false)]
    public void UnRepairMachine(uint id)
    {
        if (id == null_id) { return; }
        if (repairs.Contains(id))
        {
            repairs.Remove(id);
        }
    }
    [Command(requiresAuthority = false)]
    public void UnDamageMachine(uint id)
    {
        if (id == null_id) { return; }
        if (damagers.Contains(id))
        {
            damagers.Remove(id);
        }
    }

    //MAIN Function
    [Command(requiresAuthority =false)]
    public void ProcessMachine()
    {
        //Remove non-interacting players
        foreach(uint i in damagers)
        {
            if (i == null_id||NetworkClient.spawned[i]==null)
            {
                damagers.Remove(i);
            }
        }
        foreach (uint i in repairs)
        {
            if (i == null_id || NetworkClient.spawned[i] == null)
            {
                repairs.Remove(i);
            }
        }

        int damageCount = damagers.Count;
        int repairCount = repairs.Count;
        float f_damage_rate =1;
        float f_repair_rate = 1;
        //Process penalty damage
        if (damageCount >= 3)
        {
            f_damage_rate = 0.55f;
        }
        else if (damageCount == 2)
        {
            f_damage_rate = 0.75f;
        }
        else if (damageCount == 1)
        {
            f_damage_rate = 1f;
        }

        //Process penalty repair
        if (repairCount >= 3)
        {
            f_repair_rate = 0.55f;
        }
        else if (repairCount == 2)
        {
            f_repair_rate = 0.75f;
        }
        else if (damageCount == 1)
        {
            f_repair_rate = 1f;
        }

        float finalDamage = damageCount * f_damage_rate * damage;
        float finalRepair = repairCount * f_repair_rate * repair;
        float healthChange = finalRepair - finalDamage;
        ChangeHealth(healthChange);
        //Debug.Log("D: " + damageCount + " R: " + repairCount);
    }
    //Local functions
    public void FixedUpdate()
    {
       
        //Server Function Here
        if (isServer)
        {
            //Server Process Host Machine Damage
            ProcessMachine();

        }
        
    }
    [Command(requiresAuthority = false)]
    public void CMDOutlineChange(GameObject t, bool enable)
    {
        if (enable)
        {
            EnableOutline(t.GetComponent<NetworkIdentity>().connectionToClient);
        }
        else
        {
            DisableOutline(t.GetComponent<NetworkIdentity>().connectionToClient);
        }
    }
    //Outlines
    [TargetRpc]
    public void EnableOutline(NetworkConnection target)
    {
        this.GetComponent<Outline>().enabled = true;
    }

    [TargetRpc]
    public void DisableOutline(NetworkConnection target)
    {
        this.GetComponent<Outline>().enabled = false;
    }
}
