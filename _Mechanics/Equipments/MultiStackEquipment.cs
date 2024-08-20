using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System;
/// <summary>
/// MultiStackEquipment is for things like charms, where each time you are holding one instance
/// but each time player can bring x counts of it as one whole
/// </summary>
public class MultiStackEquipment : Equipment
{
    [SyncVar(hook = nameof(HookCountChangedClient))]
    [Header("Multi-Stack Equipment Setup")]
    [Tooltip("Set a count for this equipment")]
    public int mCount;
    [Tooltip("The instance object that will be network spawned when placing one multi-stack equipment")]
    public GameObject PF_InstanceObject;

    public void HookCountChangedClient(int oldVal, int newVal)
    {
        if (newVal <= 0)
        {
            //Switched to forward invoking because the it's difficult to handle event subscription if we subscribe in equipment manager
            //As we not only need to subscribe when an equipment is added, but also handling if an equipment is brought in by another player
            //Then, we would also need to track if we have already subscribed for an equipment, which is too much to track
            //This approach of simply invoking directly on the local player is much easier to manage and less error prone
            GameManager.instance.GetLocalPlayer().GetComponent<EquipmentManager>().OnEquipmentExhausted(this);
        }
    }

    /// <summary>
    /// Overrides for getting instance count left for this equipment (ie. how many charms left in this stack, how many salt piles this salt bottle has left)
    /// </summary>
    /// <returns></returns>
    public override int GetCount()
    {
        return mCount;
    }

    /// <summary>
    /// [Server] Decrements the count, if count reaches 0, makes the current multi-stack invisible
    /// </summary>
    [Server]
    public void ServerDecrementCount()
    {
        mCount--;
        if (mCount <= 0)
        {
            visible = false;
        }
    }
    [Command(requiresAuthority = false)]
    public void CMDPlaceInstance(Vector3 spawnPos, Quaternion spawnRot)
    {
        //Do not place if no count left
        if (mCount <= 0) return;
        ServerDecrementCount();
        GameObject _spawned = Instantiate(PF_InstanceObject, spawnPos, spawnRot);
        NetworkServer.Spawn(_spawned);
    }

    #region Overrides
    //Override so EquipmentManager and other scripts can distinguish type
    public override bool GetIsMultiStackEquipment()
    {
        return true;
    }
    #endregion
}
