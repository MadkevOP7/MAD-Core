//© 2022 by MADKEV Studio, all rights reserved
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Mirror;
public class EquipmentManager : NetworkBehaviour
{
    public Player player;
    public bool canUse = true;
    public bool isLocalPlayerBool;
    public Camera cam;
    public Material preview_material;
    public LayerMask layer;
    [Header("Audio")]
    public AudioClip equip_audio;
    public AudioMixerGroup mixer;
    private AudioSource m_audio;
    [Header("Player Interaction")]
    public PlayerMouseLook mouse_look;
    public InteractionDetection interaction;
    public Transform hand;
    public FSMCaller fsmCaller;
    public Transform hand_follow;
    public Transform shoulder_follow; //For mounted flashlight
    public Transform flashlight_point;
    public float aim_y_offset = 0.2f;
    //Equipments not synced across clients for optmization, use getters
    [Header("Equipments")]
    public static int equipment_storage_limit = 10;
    public List<Equipment> equipments = new List<Equipment>(); //Store this on localPlayer

    //[SyncVar(hook =nameof(UpdateCurrentFollowTransform))]
    public Equipment current;

    //[SyncVar]
    public Equipment current_persistent; //Persistent handling
    [Header("Debug")]
    [SyncVar]
    public bool isHandUp;
    public int selectionIndex = -1;
    public bool hasPersistentItem; //Can only have one persistent item spanwed each time, and to respawn need first destroy
    public bool is_OS_Mode;
    int hand_id;
    [Header("Placeable Debug")]
    public GameObject preview;
    public PlacementPreview pr;
    //Camera os mode
    Coroutine moveCameraFocusCo;
    static Vector3 tablet_camera_aim_rot = new Vector3(39.226f, 0.962f, 0.561f);
    Vector3 temp_camera_pos;
    Quaternion temp_camera_rot;
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        if (!isLocalPlayerBool)
        {
            isLocalPlayerBool = true;
            InitializeEquipmentReference();
        }
    }

    public void InitializeEquipmentReference()
    {
        equipments = EquipmentStorage.Instance.InitializePlayerEquipments(this.connectionToClient, netId, isLocalPlayerBool);
        //StartCoroutine(TimedEquipmentInitialization());
    }
    IEnumerator TimedEquipmentInitialization() //Delay till all players have connected
    {
        while (!canUse)
        {
            yield return null;
        }
        equipments = EquipmentStorage.Instance.InitializePlayerEquipments(this.connectionToClient, netId, isLocalPlayerBool);
    }

    public void ProcessPlaceableEquipment()
    {

        // Set origin of ray to 'center of screen' and direction of ray to 'cameraview'
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5F, 0.5F, 0F));
        RaycastHit hit; // Variable reading information about the collider hit
        if (Physics.Raycast(ray, out hit, 4f, layer))
        {
            if ((hit.transform != this.transform && hit.transform.parent != this.transform.parent || hit.transform.parent == null && hit.transform != this.transform) && (hit.transform != current.transform))
            {
                //Spawn preview
                if (preview == null || pr == null)
                {
                    if (current.placement_preview != null)
                    {
                        preview = Instantiate(current.placement_preview, Vector3.zero, Quaternion.identity);
                        preview.layer = 2;
                    }
                    pr = preview.AddComponent<PlacementPreview>();
                }

                preview.transform.position = hit.point;
                preview.transform.rotation = Quaternion.LookRotation(hit.normal);
                preview.transform.position += preview.transform.forward * current.placement_offset;
                if (Input.GetKeyDown(KeyCode.F) && pr.canPlace)
                {
                    Debug.Log("Placing item: " + current.m_name);
                    current.ServerPlaceItem(preview.transform.position, preview.transform.rotation);
                    OffloadPlacementPreview();
                    //Handles ik and canUse turn off
                    DropUnEqip();
                }
            }
        }
    }

    public void OffloadPlacementPreview()
    {
        //Ofload pointing to preview and pr, destroy and set null locally since preview object is local instantiated
        if (preview != null)
        {
            Destroy(preview.gameObject);
        }
        if (pr != null)
        {
            Destroy(pr.gameObject);
        }
        preview = null;
        pr = null;
    }
    void ProcessOSMode()
    {
        //Reverse bool as change is applied last to prevent Update() executing before setup finalization
        if (!is_OS_Mode)
        {
            EquipTablet();
        }
        else
        {
            UnEquipTablet();
        }

        is_OS_Mode = !is_OS_Mode;
    }
    // Update is called once per frame
    void Update()
    {

        if (!isLocalPlayerBool) return;
        if (!canUse)
        {
            if (current != null || current_persistent != null)
            {
                ForceUnEqip();
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ProcessOSMode();
        }
        if (!is_OS_Mode)
        {
            if (canUse && current != null && current.is_placeable)
            {
                //already checked for canUse, so no need to worry about missing camera reference
                ProcessPlaceableEquipment();
            }

            //handle dropping equipment
            if (Input.GetKeyDown(KeyCode.Q))
            {
                DropEquipment();
            }
            //Handle dropping hand (when have equipment and selected equipment allow handdrop)
            if (Input.GetMouseButtonDown(2))
            {
                if (isHandUp)
                {
                    if (current != null && current.allowDropArm)
                    {
                        ShowHand(-2);
                    }
                }
                else
                {
                    if (current != null)
                    {
                        ShowHand(hand_id);
                    }
                }
            }
            HandleInventory((int)Input.mouseScrollDelta.y);
            if (canUse && current != null && current.require_aim)
            {
                fsmCaller.aim = flashlight_point.position.y + aim_y_offset;
                CMDAimHand(flashlight_point.position.y + aim_y_offset); //Passes y aim value for ik sync, + 0.2 to offset a bit up
            }


        }


    }
    [Command(requiresAuthority = false)]
    public void CMDAimHand(float aim)
    {
        RPCAimHand(aim);
    }

    [ClientRpc(includeOwner = false)]
    public void RPCAimHand(float aim)
    {
        fsmCaller.aim = aim;
    }
    public void HandleInventory(int i)
    {
        if (i == 0) return;
        UpdateSelectionIndex(i);
        if (selectionIndex < 0 || selectionIndex > equipments.Count - 1)
        {
            UnEquip();
            return;
        }
        if (selectionIndex != -1 && equipments[selectionIndex].is_persistent)
        {
            if (!hasPersistentItem)
            {
                Equip(selectionIndex);
                hasPersistentItem = true;
            }
        }
        else
        {
            Equip(selectionIndex);
        }
    }

    public void DropEquipment()
    {
        if (current == null || current.is_permanent) return;
        //if (GetComponent<BuildingManager>().enabled) return; //Prevent button fighting w/ building manager
        if (current != null)
        {
            current.ServerSetCanPickUp(true);
        }
        else if (current_persistent != null)
        {
            current_persistent.ServerSetCanPickUp(true);
        }
        DropUnEqip();
    }
    public void PickUpEquipment(Equipment e)
    {
        if (e == null)
        {
            return;
        }
        if (!e.IsPickupable())
        {
            return;
        }
        if (equipments.Count < equipment_storage_limit)
        {
            e.pickUp_event++;
            e.isCreatedFromSave = false;
            equipments.Add(e);
            Equip(equipments.IndexOf(e));
        }
        else
        {
            //[Space] for implementations notifying the player that its inventory is full
        }

    }

    public void UpdateSelectionIndex(int g)
    {
        selectionIndex += g;
        //Clamp
        if (selectionIndex < -1)
        {
            selectionIndex = -1;
        }
        if (selectionIndex > equipments.Count - 1)
        {
            selectionIndex = equipments.Count - 1;
        }
    }
    #region Hand Control

    //Call this for showhand, handles local and state sync to client
    public void ShowHand(int visible)
    {
        fsmCaller.ShowHand(visible);
        CMDShowHand(visible);
    }
    [Command(requiresAuthority = false)]
    public void CMDShowHand(int t)
    {
        if (t == -1)
        {
            isHandUp = false;
        }
        else
        {
            isHandUp = true;
        }
        RPCShowHand(t);
    }

    //Owner handles local to prevent self lag
    [ClientRpc(includeOwner = false)]
    public void RPCShowHand(int s)
    {
        if (s == -1)
        {
            isHandUp = false;
        }
        else
        {
            isHandUp = true;
        }
        fsmCaller.ShowHand(s);
    }
    #endregion
    #region Equipment Control
    public void DropAllEquipments()
    {
        OffloadPlacementPreview();
        foreach (Equipment e in equipments)
        {
            if (e.is_permanent)
            {
                continue;
            }
            e.OnDropped();
            e.OnUnEquip();
            e.canUse = false;
            e.ServerSetCanPickUp(true); //Drop equipment
        }
        equipments.Clear();
        current = null;
        current_persistent = null;
    }
    //C-05/21/2022 Replaced hook with ClientRpc
    [ClientRpc]
    public void UpdateCurrentFollowTransform(Vector3 pos, Vector3 rot)
    {
        //if (newVal != null)
        //{
        //    hand_follow.localPosition = newVal.GetComponent<Equipment>().position;
        //    hand_follow.localEulerAngles = newVal.GetComponent<Equipment>().rotation;

        //}
        hand_follow.localPosition = pos;
        hand_follow.localEulerAngles = rot;
    }
    public void UnEquip()
    {
        ShowHand(-1);
        if (current != null && !current.is_persistent)
        {
            if (current.is_placeable)
            {
                OffloadPlacementPreview();
            }
            current.canUse = false;
            current.OnUnEquip();
            ServerShow(current, false);
            current = null;
        }
    }
    public void DropUnEqip()
    {

        ShowHand(-1);
        if (current != null && !current.is_permanent)
        {
            if (current.is_placeable)
            {
                OffloadPlacementPreview();
            }
            string n = current.m_name;
            for (int i = equipments.Count - 1; i >= 0; --i)
            {
                if (equipments[i].m_name == n)
                {
                    equipments.RemoveAt(i);
                    break;
                }
            }

            current.canUse = false;
            current.OnUnEquip();
            current.OnDropped();
            current = null;
        }
        else if (current_persistent != null)
        {
            string n = current_persistent.m_name;
            for (int i = equipments.Count - 1; i >= 0; --i)
            {
                if (equipments[i].m_name == n)
                {
                    equipments.RemoveAt(i);
                    break;
                }
            }

            current_persistent.canUse = false;
            current_persistent.OnUnEquip();
            current_persistent.OnDropped();
            current_persistent = null;
            hasPersistentItem = false;
        }

    }
    public void ForceUnEqip()
    {

        ShowHand(-1);
        if (current != null)
        {
            if (current.is_placeable)
            {
                OffloadPlacementPreview();
            }
            current.canUse = false;
            current.OnUnEquip();
            ServerShow(current, false);
        }
        hasPersistentItem = false;
        if (current_persistent != null)
        {
            current_persistent.canUse = false;
            current_persistent.OnUnEquip();
            ServerShow(current_persistent, false);
        }
        current_persistent = null;
        current = null;
    }
    public void UnEqipPersistent()
    {
        ShowHand(-1);
        if (current_persistent != null)
        {
            current_persistent.canUse = false;
            current_persistent.OnUnEquip();
            ServerShow(current_persistent, false);
            current_persistent = null;
            hasPersistentItem = false;
        }
    }

    public void EquipTablet()
    {

        interaction.DisablePlayerUI();
        player.FreezeCameraAnimator();
        //player.SetAnimatorSpeed(0);
        mouse_look.enabled = false;
        UnEquip();
        Equipment e = EquipmentStorage.Instance.observe_tablet;
        current = e;
        EquipmentStorage.Instance.observe_tablet.GetComponent<ObserveOS>().Launch(cam);
        hand_id = e.HandIKType;
        ShowHand(hand_id);
        CMDEquip(e);
        temp_camera_pos = cam.transform.localPosition;
        temp_camera_rot = cam.transform.localRotation;
        moveCameraFocusCo = StartCoroutine(DelayCameraAim(e.transform));
    }
    IEnumerator DelayCameraAim(Transform t)
    {
        while (Quaternion.Angle(cam.transform.localRotation, Quaternion.Euler(tablet_camera_aim_rot)) > 0.01f)
        {
            cam.transform.localRotation = Quaternion.Slerp(cam.transform.localRotation, Quaternion.Euler(tablet_camera_aim_rot), Time.deltaTime * 6f);
            yield return null;
        }
    }

    //Compare 2 vector3 if their difference less than diff, return true, not currently used
    public bool DiffVector3(Vector3 a, Vector3 b, float diff)
    {
        if (Mathf.Abs(a.x - b.x) < diff && Mathf.Abs(a.y - b.y) < diff && Mathf.Abs(a.z - b.z) < diff)
        {
            return true;
        }
        return false;
    }
    public void UnEquipTablet()
    {
        if (moveCameraFocusCo != null)
        {
            StopCoroutine(moveCameraFocusCo);
        }
        interaction.EnablePlayerUI();
        player.UnFreezeCameraAnimator();
        //player.SetAnimatorSpeed(1);
        cam.transform.localPosition = temp_camera_pos;
        cam.transform.localRotation = temp_camera_rot;
        mouse_look.enabled = true;
        EquipmentStorage.Instance.observe_tablet.GetComponent<ObserveOS>().Close();
        UnEquip();
        Equip(selectionIndex);
    }
    public void Equip(int index)
    {
        if (index < 0 || index > equipments.Count - 1)
        {
            UnEquip();
            return;
        }
        UnEquip();
        current = equipments[index];
        if (equipments[index].is_persistent)
        {
            current_persistent = equipments[index];
        }
        hand_id = equipments[index].HandIKType;
        ShowHand(hand_id);
        CMDEquip(equipments[index]);
        //LocalPlay2DAudio(equip_audio, .086f, true); Commented for now, audio kind of annoying
        //CMDSpawn(index);
    }

    public void LocalPlay2DAudio(AudioClip clip, float volume, bool singlePlay)
    {
        if (!isLocalPlayerBool)
        {
            return;
        }
        if (m_audio == null)
        {
            m_audio = gameObject.AddComponent<AudioSource>();
            m_audio.outputAudioMixerGroup = mixer;
        }
        m_audio.volume = volume;
        m_audio.spatialBlend = 0;
        if (singlePlay)
        {
            if (!m_audio.isPlaying)
            {
                m_audio.clip = clip;
                m_audio.Play();
            }
        }
        else
        {
            m_audio.PlayOneShot(clip);
        }
    }

    [Command(requiresAuthority = false)]
    public void CMDEquip(Equipment e)
    { 
        e.visible = true;
        e.ServerSetCanPickUp(false);
        e.player = GetComponent<Player>();
        e.p_follow = netId;

        e.CMDRefresh();
        UpdateCurrentFollowTransform(e.position, e.rotation);
        RPCInitializeEquipment(e);
    }

    [Command(requiresAuthority = false)]
    public void ServerShow(Equipment e, bool visible)
    {
        e.visible = visible;
    }

    [TargetRpc]
    public void RPCInitializeEquipment(Equipment e)
    {
        //Enable Canuse
        e.canUse = true;
    }
    #endregion
}
