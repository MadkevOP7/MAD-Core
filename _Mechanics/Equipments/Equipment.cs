//© 2022 by MADKEV Studio, all rights reserved
//Handling each individual equipment, local class, handle client sync through EquipmentManager
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class Equipment : NetworkBehaviour
{
    public Player player;
    [Header("Equipment Info")]
    public Sprite image;
    public int id = -1; //For EquipmentStorage and save/load
    public string m_name = "Default Equipment";
    private AudioSource m_audio;
    public AudioClip dropped_sound;
    public bool require_aim;
    public bool allowDropArm = true; //Hold object down like running with phone, allow for smaller objects and false for larger ones
    public bool canUse; //Holds LocalPlayer can USe
    public bool is_persistent; //For flashlight which doesn't auto unEqip
    public bool is_placeable; //For equipments that can be placed, (cameras, sensors, etc)
    public bool is_permanent; //For tablet, which players can't drop
    public GameObject placement_preview; //For placing previews without network identity
    public bool constraint_preview_direction = true; //Have object always face forward, such as motion detectors always face away from wall, etc
    public float placement_offset = 2f; //Placement deviation from wall, etc Moves in the direction of facing (v3.forward)
    public int HandIKType = 0;
    //0 = default phone hand ik raise, 1 = Motion Detector grab -1 = none
    [Header("Initialization Storage")]
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale;

    [Header("Pseudo Events")]
    public int pickUp_event;
    public int placement_event; //In Update() of individual script, compare count with this, which will increment to invoke a function in each equipment
    public int unEquip_event;
    public int dropped_event;
    [Header("Placement")]
    public bool placed; //For placeable equipments, local
    [SyncVar(hook =nameof(HookPlacementPos))]
    public Vector3 placement_pos;
    [SyncVar(hook =nameof(HookPlacementRot))]
    public Quaternion placement_rot;
    [Header("Runtime")]
    [SyncVar(hook = nameof(HookVisible))]
    public bool visible;
    public bool binded; //for determining when the first player has binded, then if the player becomes null equipment drops (player disconnect)
    [SyncVar(hook =nameof(UpdateFollow))]
    public uint p_follow; //On the server, change this for updating owner
    public Transform follow;
    public Vector3 disconnect_drop_pos; //Last follow pos
    [SyncVar(hook = nameof(Drop))]
    private bool canPickUp = false;
    public bool needRefresh; //Not syncvar, update through hook and each individual equipment type
    public Camera player_cam;

    public bool isCreatedFromSave = false; //On the server only, do not sync
    public void LateUpdate(){
        //Observer view syncs in lateupdate()
        if (!canUse&&!canPickUp)
        {
            Sync();
        }
    }
    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!isCreatedFromSave)
        {
            HookVisible(false, false);
        }

    }
    private void OnCollisionEnter(Collision collision)
    {
        //Hits ground
        if(collision.relativeVelocity.magnitude > 0.68f&&collision.gameObject.layer!=7)
        {
            Play3DAudio(dropped_sound, 0.268f);
        }

        //Sync transform data
        if (isServer)
        {
            ServerSetTransformData(transform.position, transform.eulerAngles);
        }
    }

    public void Play3DAudio(AudioClip clip, float volume)
    {
        if (m_audio == null)
        {
            m_audio = gameObject.AddComponent<AudioSource>();
        }
        m_audio.spatialBlend = 1;
        m_audio.maxDistance = 3;
        m_audio.PlayOneShot(clip, volume);
    }
    private void Update()
    {
        //Disconnect check
        if (player == null&&binded)//If equipment owner disconnects, drop the equipment
        {
            //Binded player has disconnected
            if (!is_permanent)
            {
                ServerSetCanPickUp(true); //Sets can pickup, which drops equipment
            }
            else
            {
                ServerDestroy();
            }
            return;
        }
        //Localplayer view syncs in Update
        if (canUse&&!canPickUp)
        {
            Sync();
        }
        if (follow != null)
        {
            disconnect_drop_pos = follow.transform.position;
        }
    }

    #region Events
    //[TODO] Fires when equipment is unequipped
    public void OnUnEquip()
    {
        unEquip_event++;
    }
    public void OnDropped()
    {
        dropped_event++;
    }
    #endregion
    #region Sync
    #region Transform Override
    [Command(requiresAuthority = false)]
    public void ServerSetTransformData(Vector3 position, Vector3 rotation)
    {
        RPCSetPosition(position);
        RPCSetRotation(rotation);
    }
    [Command(requiresAuthority = false)]
    public void ServerSetPosition(Vector3 position)
    {
        RPCSetPosition(position);
    }

    [ClientRpc]
    void RPCSetPosition(Vector3 position)
    {
        transform.position = position;
    }

    [Command(requiresAuthority = false)]
    public void ServerSetRotation(Vector3 rotation)
    {
        RPCSetRotation(rotation);
    }

    [ClientRpc]
    void RPCSetRotation(Vector3 rotation)
    {
        transform.eulerAngles = rotation;
    }
    #endregion

    public void UpdateFollow(uint oldVal, uint netVal)
    {
        if (HandIKType == -1)
        {
            follow = NetworkClient.spawned[p_follow].GetComponent<EquipmentManager>().shoulder_follow;
        }
        else
        {
            follow = NetworkClient.spawned[p_follow].GetComponent<EquipmentManager>().hand_follow;
        }
        player = NetworkClient.spawned[p_follow].GetComponent<Player>();
        if (player != null && player.mainCamera != null)
        {
            player_cam = player.mainCamera;
        }
        binded = true;
    }
    
    public void Sync()
    {
        if (!visible)
        {
            return;
        }
        
        if (follow != null&&visible&&!placed)
        {
            transform.position = follow.position;
            transform.rotation = follow.rotation;
        }

       
    }
    public void HookVisible(bool oldVal, bool newVal)
    {
        if (!newVal)
        {
            transform.position = new Vector3(-9999, -9999, -9999);
            canUse = false;
        }
    }
    #endregion

    #region Pseudo Events
    public void OnEquipmentPlaced()
    {
        placement_event++;
    }
    #endregion
    #region Placement
    [Command(requiresAuthority = false)]
    public void ServerPlaceItem(Vector3 pos, Quaternion rot)
    {
        placement_pos = pos;
        placement_rot = rot;
    }

    public void HookPlacementPos(Vector3 oldVal, Vector3 newVal)
    {
        transform.position = newVal;
        placed = true;
        UpdateRotation(placement_rot);
    }

    public void HookPlacementRot(Quaternion oldVal, Quaternion newVal)
    {
        UpdateRotation(newVal);
    }

    
    //Prevent hook rotation not called again if position is the same
    void UpdateRotation(Quaternion rot)
    {
        transform.rotation = rot;
        placed = true;
        ServerSetCanPickUp(true);
    }
    #endregion
    //Hook, called on each client by syncvar
    public void Drop(bool oldVal, bool newVal)
    {
        
        if (newVal)
        {
            if (placed)
            {
                //Item is placed
                //Handle physics
                if (GetComponent<BoxCollider>() == null)
                {
                    this.gameObject.AddComponent<BoxCollider>();
                }

                if (GetComponent<Rigidbody>() == null)
                {
                    this.gameObject.AddComponent<Rigidbody>();
                }
                BoxCollider bx = GetComponent<BoxCollider>();
                Rigidbody rd = GetComponent<Rigidbody>();
                bx.isTrigger = true; //For now, we disable physics collision (with raycast detection on) to prevent items from blocking players
                rd.detectCollisions = true;
                rd.useGravity = false;
                rd.isKinematic = true;
                canUse = false;
                this.gameObject.tag = "Equipment";
                OnEquipmentPlaced();
                return;
            }

            //Item is being dropped
            if (binded)
            {
                transform.position = disconnect_drop_pos;
            }
            //Handle Multiply size by 2 as dropped items are too small
            transform.localScale *= 2;
            //Handle physics
            if (GetComponent<BoxCollider>() == null)
            {
                this.gameObject.AddComponent<BoxCollider>();  
            }

            if (GetComponent<Rigidbody>() == null)
            {
                this.gameObject.AddComponent<Rigidbody>();
            }
            BoxCollider b = GetComponent<BoxCollider>();
            Rigidbody r = GetComponent<Rigidbody>();
            r.detectCollisions = true;
            b.isTrigger = false;
            r.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            r.useGravity = true;
            r.isKinematic = false;
            //If loaded back from save, do not add force and throw
            if (!isCreatedFromSave)
            {
                Vector3 forwardDir = player != null ? player.transform.forward : this.transform.forward;
                Vector3 upwardDir = player != null ? player.transform.up : this.transform.up;
                r.AddForce(forwardDir * 4, ForceMode.Impulse);
                r.AddForce(upwardDir * 1, ForceMode.Impulse);
     
            }   
            canUse = false;
            this.gameObject.tag = "Equipment";
        }
        else
        {
            //Item is being attempted to pick up
            if (!placed)
            {
                transform.localScale /= 2;
            }
            //Reset Physics

            if (GetComponent<BoxCollider>() != null)
            {
                this.gameObject.GetComponent<BoxCollider>().isTrigger = true;
            }
            if (GetComponent<Rigidbody>() != null)
            {
                Rigidbody r = GetComponent<Rigidbody>();
                r.useGravity = false;
                r.isKinematic = true;
                r.detectCollisions = false;
            }
            placed = false;

            //Being picked up
            needRefresh = true;
            
        }
        
    }

    [Command(requiresAuthority =false)]
    public void ServerDestroy()
    {
        NetworkServer.Destroy(this.gameObject);
    }

    [Command(requiresAuthority = false)]
    public void ServerSetCanPickUp(bool state)
    {
        if (canPickUp == state)
        {
            return;
        }
        canPickUp = state;
        visible = true;
        Debug.Log(m_name + " has been set as pickupable: "+canPickUp);
    }

    public bool IsPickupable()
    {
        return canPickUp;
    }

    [Command(requiresAuthority = false)]
    public void CMDRefresh()
    {
        RPCRefresh();
    }

    [ClientRpc()]
    public void RPCRefresh()
    {
        needRefresh = true;
    }
}
