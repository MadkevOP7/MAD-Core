// Copyright © 2024 by MADKEV Studio, all rights reserved

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.Video;
using UnityEngine.Timeline;
using UnityEngine.Playables;

public class HostMachine : NetworkBehaviour
{
    #region Const Defines
    // Balance settings
    private static float BASE_DAMAGE_AMOUNT = 0.0168f;
    #endregion
    [Header("Host Manager")]
    public Material hmMainMaterial;
    public HostMachineManager hm;
    public VideoPlayer screen1;
    public VideoPlayer screen2;
    public PlayableDirector timeline;
    public Animator anim;

    [Header("Control")]
    [SyncVar(hook = nameof(HookSetStateDestroyed))]
    public bool isAlive = true;

    [SyncVar(hook = nameof(HookSetAsMasterHM))]
    public bool isMasterHM = false;

    [Header("Storage")]
    [SyncVar]
    public float machine_Health = HostMachineManager.GLOBAL_REGULAR_MACHINE_MAX_HEALTH;
    [SyncVar]
    public float max_health = HostMachineManager.GLOBAL_REGULAR_MACHINE_MAX_HEALTH;
    [Header("Audios")]
    public AudioSource audioSource;
    public AudioClip errorAudio;
    public AudioClip shutdownAudio;
    List<uint> damagers = new List<uint>(); //Store network identity of damager
    List<uint> repairs = new List<uint>(); //Store network identity of damager

    bool isInInteractCooldown;
    //Where tf did this come from???
    static uint null_id = 892;
    //Player iD 892 = null iD;

    //Runtime
    uint originEffectApplyer = 892;
    public static void SetGlobalHMDamageAmount(float amount)
    {
        BASE_DAMAGE_AMOUNT = amount;
    }

    // If this has been discovered in the "past", thus showing on both past and present
    [SyncVar(hook = nameof(HookSetDiscovered))]
    public bool mIsDiscoveredAllTiemframe = false;

    // HM Challenge
    [SyncVar(hook = nameof(HookOnPendingChallengeChanged))]
    private bool mPendingChallenge = false;

    public bool GetIsPendingChallenge() { return mPendingChallenge; }
    private void HookOnPendingChallengeChanged(bool oldVal, bool newVal)
    {

    }
    public void HookSetDiscovered(bool oldVal, bool newVal)
    {
        LocalSetHostmachineTimeframeVisibility(newVal, true);
    }

    [Command(requiresAuthority = false)]
    public void CMDSetAsDiscovered()
    {
        mIsDiscoveredAllTiemframe = true;
    }
    //10/11/2023 - Change to hook to support late joiners
    public void HookSetStateDestroyed(bool oldVal, bool newVal)
    {
        if (newVal) return; //If machine is alive, skip

        Debug.Log(gameObject.name + " is destroyed");
        //Freeze effect
        anim.enabled = false;
        timeline.Pause();
        Material t = new Material(screen1.GetComponent<MeshRenderer>().material);
        t.EnableKeyword("_EMISSION");
        screen1.GetComponent<MeshRenderer>().material = t;
        screen2.GetComponent<Animator>().enabled = true;
        audioSource.clip = shutdownAudio;
        audioSource.volume = 0.5f;
        audioSource.Play();
        StartCoroutine(EnableObserverServerErrorRepeat());

        GameManager.instance.GetComponent<HostMachineManager>().OnHostMachineDestroyed(this);

        // Show red silhouette when destroyed
        EnableOutlineClient(true);
    }

    IEnumerator EnableObserverServerErrorRepeat()
    {
        yield return new WaitForSeconds(3);
        audioSource.clip = errorAudio;
        audioSource.loop = true;
        audioSource.Play();
    }

    /// <summary>
    /// Main function to damage machine. 
    /// </summary>
    /// <param name="id"></param>
    /// <param name="applyOriginEffect">Whether to use this machine as origin and sync damage across to all host machines (effect)</param>
    [Command(requiresAuthority = false)]
    public void DamageMachine(uint id, bool applyOriginEffect)
    {
        if (id == null_id) { return; }
        if (!damagers.Contains(id))
        {
            damagers.Add(id);
            if (applyOriginEffect)
            {
                originEffectApplyer = id;
                hm.PlayConnectAllHMRayEffect(this);
            }
            //Invoke OnHostMachineAttacked callback with cool down
            if (!isInInteractCooldown)
            {
                //This is on the server
                AIBrain.Instance.OnHostMachineAttacked(this);
                isInInteractCooldown = true;
                StartCoroutine(IEHMInteractCooldown());
            }
        }
    }
    IEnumerator IEHMInteractCooldown()
    {
        yield return new WaitForSeconds(LIMENDefine.GetHMInteractCooldownTime(GameManager.instance._currentDifficulty) * 60);
        isInInteractCooldown = false;
    }
    [ServerCallback]
    public void ChangeHealth(float v)
    {
        if (!isAlive) return;

        if (machine_Health >= 0)
        {
            machine_Health += v;
        }

        if (machine_Health <= 0)
        {
            // Uses hook instead of RPC to support late joiner
            isAlive = false; //Prevent race condition
            AIBrain.Instance.OnHostMachineDestroyed(this);
            machine_Health = 0;

            // Apply case for main machine to destroy all others
            if (isMasterHM)
                GameManager.instance.GetHostMachineManager().DamageAllMachines(-max_health);
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
            //Stop effect applying when stops damaging machine
            if (id == originEffectApplyer)
            {
                originEffectApplyer = null_id;
                hm.StopConnectAllHMRayEffect();
            }
            damagers.Remove(id);
        }
    }
    public void ProcessMachine()
    {
        //Remove non-interacting players
        for (int i = damagers.Count - 1; i >= 0; --i)
        {
            if (damagers[i] == null_id || NetworkClient.spawned[damagers[i]] == null)
            {
                if (damagers[i] == originEffectApplyer)
                {
                    originEffectApplyer = null_id;
                    hm.StopConnectAllHMRayEffect();
                }
                damagers.RemoveAt(i);
            }
        }

        int damageCount = damagers.Count;
        if (damageCount == 0) return;
        float f_damage_rate = 1;

        // Process penalty damage
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

        float finalDamage = damageCount * f_damage_rate * BASE_DAMAGE_AMOUNT;

        // If we have a specified origin applier, damage all machines with this amount
        if (originEffectApplyer == null_id)
        {
            ChangeHealth(-finalDamage);
        }
        else
        {
            hm.DamageAllMachines(-finalDamage);
        }
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

    public void EnableOutlineClient(bool state)
    {
        GetComponent<Outline>().enabled = state;
    }

    public void HookSetAsMasterHM(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            int expCount = 2; //We have 2 screens, here as a safe check
            foreach (var m in gameObject.GetComponentsInChildren<MeshRenderer>())
            {
                if (m.gameObject.name.ToLower().Contains("screen"))
                {
                    m.material = hmMainMaterial;
                    expCount--;
                }
            }

            if (expCount != 0)
            {
                Debug.LogError("Failed to set HM master, check setup");
            }
        }
    }

    /// <summary>
    /// If local hide, all MeshRenderer, collider, audio source would be turned off and NavMeshObstacle Disabled
    /// </summary>
    /// <param name="state"></param>
    public void LocalSetHostmachineTimeframeVisibility(bool state, bool isFromHook = false)
    {
        //If we are already discovered, skip any future calls
        //But, from the hook we need to force set
        if (!isFromHook && mIsDiscoveredAllTiemframe) return;

        foreach (MeshRenderer render in GetComponentsInChildren<MeshRenderer>())
        {
            render.enabled = state;
        }

        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = state;
        }

        GetComponent<UnityEngine.AI.NavMeshObstacle>().enabled = state;
        foreach (AudioSource audio in GetComponentsInChildren<AudioSource>())
        {
            audio.enabled = state;
        }

        //Enable the collider on the root with trigger
        Collider rootColl = GetComponent<Collider>();
        rootColl.enabled = true;
        rootColl.isTrigger = !state;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        GameManager.instance.GetHostMachineManager().RegisterHostMachine(this);
        //Because we start in the present, all host machine start invisible
        LocalSetHostmachineTimeframeVisibility(false);
    }
}
