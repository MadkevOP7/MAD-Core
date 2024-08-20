//© 2022 by MADKEV Studio, all rights reserved
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Mirror;
using Rewired;
using UnityEngine.SceneManagement;
public class EquipmentManager : NetworkBehaviour
{
    #region Defines
    private static Vector3 AIM_ROTATION_TABLET_OVERRIDE = new Vector3(39.226f, 0.962f, 0.561f);
    #endregion
    public Player player;
    [HideInInspector]
    public bool canUse = true;
    [HideInInspector]
    public bool forceBlockTabletSwitch = false; //For things like Lobby where first tab should exit lobby TV focus
    [HideInInspector]
    public bool isGamePaused; //canUse bool above has more use, so separate
    public Camera cam;
    public LayerMask layer;
    [Header("Audio")]
    public AudioMixerGroup mixer;
    public AudioClip pickUpAudioClip;
    [HideInInspector]
    public AudioSource m_audio;
    [Header("Player Interaction")]
    public PlayerMouseLook mouse_look;
    public InteractionDetection interaction;
    public FSMCaller fsmCaller;
    public Transform aimIKTarget;
    public float aim_y_offset_persistent = 0.2f;
    public float aim_y_offset = 0.4f;
    [SyncVar(hook = nameof(HookOnHandStateChanged))]
    private int mCurrentHandState = FSMCaller.NO_HAND_STATE;
    //Equipments not synced across clients for optimization, use getters
    [Header("Equipments")]
    private List<Equipment> mPlayerEquipmentCache = new List<Equipment>(); //Store this on localPlayer
    private Equipment mCurrentEquipment;
    //[SyncVar]
    private Equipment mCurrentPersistent; //Persistent handling
    private Equipment mObserveTablet; //10/30/2023 - Moved here to prevent RPC race condition null ref
    private int mCurrentSelectedEquipmentIndex = -1;
    private bool mHasPersistentItem; //Can only have one persistent item spawned each time, and to re spawn need first destroy
    private bool mIsInOSMode;
    private GameObject mPreviewInstance;
    private PlacementPreview mPreviewInstancePlacementCache;
    //Camera os mode
    private Coroutine mMoveCameraFocusCo;
    private Vector3 mTempCameraPosition;
    private Quaternion mTempCameraRotation;
    // Runtime Cache
    // Client cache for follow position and rotation, when character switches we need to update new character's sockets
    // We cache because some equipment like Phone has different holding positions depending on state, thus we can't 
    // Simply set to equipment initialization position and rotation
    private Vector3 mFollowLocalPositionCache;
    private Vector3 mFollowLocalRotationCache;
    private bool mLastCanUseValueCache = false;
    // This cache is different than FSMCaller's cache
    private int mLocalPlayerHandStateCache = FSMCaller.DROP_HAND_STATE;
    // This caches the hand state before a DROP_HAND_STATE is applied to store last in-use hand state
    private int mLocalPlayerHandLastRaisedStateCache = FSMCaller.DROP_HAND_STATE;
    //Equipment Manager must store Rewired Player reference, as in Lobby we don't have GameManager but Tablet is used
    private Rewired.Player RPlayer;
    #region Getter
    public Equipment GetCurrentEquipment() { return mCurrentEquipment; }
    public List<Equipment> GetEquipmentList() { return mPlayerEquipmentCache; }
    public Equipment GetObserveTablet()
    {
        return mObserveTablet;
    }

    public bool GetIsOSMode() { return mIsInOSMode; }

    private void Awake()
    {
        // Character change hook for late joiner could be called before OnStartClient, subscribe in Awake()
        player.OnPlayerCharacterChangedClientCallback += OnPlayerCharacterChangedCallbackClient;
    }
    #endregion
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        RPlayer = ReInput.players.GetPlayer(0);
        InitializeEquipmentReference(SceneManager.GetActiveScene().name == "Lobby");
        if (SceneManager.GetActiveScene().name != "Lobby")
        {
            //Time frame manager is only in games, not lobby
            BaseTimeframeManager.Instance.OnRefreshLocalPlayerIsPastState += OnLocalPlayerTimeframeChanged;
            BaseTimeframeManager.Instance.OnLocalPlayerLimenBreakoccured += OnLocalPlayerLimenBreakOccured;
        }
        mLastCanUseValueCache = canUse;
    }

    /// <summary>
    /// Address race condition 10/30/2023 where RPC receives reference for Server spawned GameObject could be null.
    /// Callback from Equipment's hook to add to equipment list on local player (caller)
    /// </summary>
    /// <param name="e"></param>
    public void LocalPlayerAddEquipmentCallback(Equipment e)
    {
        if (!isLocalPlayer) return;
        mPlayerEquipmentCache.Add(e);
    }

    /// <summary>
    /// This function only executes on local player (the local player that initiated the tablet spawn)
    /// </summary>
    /// <param name="e"></param>
    public void LocalPlayerAddTabletCallback(Equipment e)
    {
        if (!isLocalPlayer) return;
        mObserveTablet = e;
        mObserveTablet.GetComponent<ObserveOS>().InitializeOSOnLocalPlayerOwning();
    }

    public void InitializeEquipmentReference(bool isInLobby)
    {
        EquipmentStorage.Instance.InitializePlayerEquipments(this.connectionToClient, netId, isLocalPlayer, isInLobby);
        //StartCoroutine(TimedEquipmentInitialization());
    }


    public void ProcessPlaceableEquipment()
    {
        // Set origin of ray to 'center of screen' and direction of ray to 'camera view'
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5F, 0.5F, 0F));
        RaycastHit hit; // Variable reading information about the collider hit
        if (Physics.Raycast(ray, out hit, 4f, layer))
        {
            if ((hit.transform != this.transform && hit.transform.parent != this.transform.parent || hit.transform.parent == null && hit.transform != this.transform) && (hit.transform != mCurrentEquipment.transform))
            {
                //Spawn preview
                if (mPreviewInstance == null || mPreviewInstancePlacementCache == null)
                {
                    if (mCurrentEquipment.placement_preview != null)
                    {
                        mPreviewInstance = Instantiate(mCurrentEquipment.placement_preview, Vector3.zero, Quaternion.identity);
                        mPreviewInstance.layer = 2;
                    }
                    mPreviewInstancePlacementCache = mPreviewInstance.AddComponent<PlacementPreview>();
                    mPreviewInstancePlacementCache.mPlacementAxis = mCurrentEquipment.mPlacementAxis;
                    mPreviewInstancePlacementCache.mCanRotatePlacementPreview = mCurrentEquipment.mCanRotatePlacementPreviw;
                }

                mPreviewInstance.transform.position = hit.point;

                // Placement Axis determines the front facing of the object so when auto rotating to align with surface the specified side will face the player
                // Only create look rotation for forward type, for upward no rotation needed as surface snap should be ground only
                if (mPreviewInstancePlacementCache.mPlacementAxis == Equipment.PlacementAxis.Forward)
                    mPreviewInstance.transform.rotation = Quaternion.LookRotation(hit.normal);

                // Get offset axis
                Vector3 offsetAxis = mPreviewInstance.transform.forward;
                switch (mPreviewInstancePlacementCache.mPlacementAxis)
                {
                    case Equipment.PlacementAxis.Forward:
                        break;
                    case Equipment.PlacementAxis.Up:
                        offsetAxis = mPreviewInstance.transform.up;
                        break;
                }
                mPreviewInstance.transform.position += offsetAxis * mCurrentEquipment.placement_offset;

                // Adjust position to prevent intersection with walls/ceilings
                Bounds bounds = mPreviewInstancePlacementCache.GetCollider().bounds;
                Vector3 halfSize = bounds.extents;

                // Calculate the required adjustment to prevent intersection
                Vector3 overlapAdjustment = Vector3.zero;

                // Check each axis for overlap and calculate the adjustment
                if (hit.normal.x != 0)
                    overlapAdjustment.x = halfSize.x * hit.normal.x;
                if (hit.normal.y != 0)
                    overlapAdjustment.y = halfSize.y * hit.normal.y;
                if (hit.normal.z != 0)
                    overlapAdjustment.z = halfSize.z * hit.normal.z;

                // Apply the overlap adjustment
                mPreviewInstance.transform.position += overlapAdjustment;

                // Rotation if can be rotated, ie. RC Equipment
                if (mPreviewInstancePlacementCache.mCanRotatePlacementPreview)
                {
                    // Check for rotate input and update the rotation angle
                    if (RPlayer.GetButtonDown(LIMENDefine.ACTION_ROTATE))
                    {
                        // Apply the rotation to the preview
                        mPreviewInstance.transform.Rotate(offsetAxis, 90);
                    }
                }

                if (RPlayer.GetButtonDown(LIMENDefine.ACTION_PLACE) && mPreviewInstancePlacementCache.GetCanPlace())
                {
#if UNITY_EDITOR
                    Debug.Log("Placing item: " + mCurrentEquipment.m_name);
#endif
                    if (mCurrentEquipment.GetIsMultiStackEquipment())
                    {
                        ((MultiStackEquipment)mCurrentEquipment).CMDPlaceInstance(mPreviewInstance.transform.position, mPreviewInstance.transform.rotation);
                    }
                    else
                    {
                        mCurrentEquipment.ServerPlaceItem(mPreviewInstance.transform.position, mPreviewInstance.transform.rotation);
                    }

                    //Important! Null-check for current needed because callback for multi-stack sets current to null and could change it during frame execution
                    //Multi-stack is handled through client-side callback as the count is stored on server and we need to account for ping
                    if (mCurrentEquipment == null || !mCurrentEquipment.GetIsMultiStackEquipment())
                    {
                        //Remove the placement preview and unequip the equipment with arm dropping
                        OffloadPlacementPreview();
                        DropUnEquip();
                    }
                }
            }
        }
    }

    public bool GetIsPlayerCarryInventoryFull()
    {
        return mPlayerEquipmentCache.Count == OSAppInventory.MAX_LOADOUT_SIZE;
    }
    public void OffloadPlacementPreview()
    {
        //Offload pointing to preview and pr, destroy and set null locally since preview object is local instantiated
        if (mPreviewInstance != null)
        {
            Destroy(mPreviewInstance.gameObject);
        }
        if (mPreviewInstancePlacementCache != null)
        {
            Destroy(mPreviewInstancePlacementCache.gameObject);
        }
        mPreviewInstance = null;
        mPreviewInstancePlacementCache = null;
    }
    void ProcessOSMode()
    {

        //Reverse bool as change is applied last to prevent Update() executing before setup finalization
        if (!mIsInOSMode)
        {
            EquipTablet();
        }
        else
        {
            UnEquipTablet();
        }

    }
    // Update is called once per frame
    void Update()
    {

        if (!isLocalPlayer || isGamePaused) return;

        if (!player.is_alive)
        {
            if (mCurrentEquipment != null || mCurrentPersistent != null)
            {
                ForceUnEquip(true);
            }
            return;
        }


        //Check if tablet use is blocked, for cases like Lobby TV where we want first Tab to end lobby TV focus
        if (RPlayer.GetButtonDown(LIMENDefine.ACTION_USE_OS) && !forceBlockTabletSwitch)
        {
            ProcessOSMode();
        }

        if (!canUse)
        {
            if ((mCurrentEquipment != null && !mIsInOSMode) || mCurrentPersistent != null)
            {
                ForceUnEquip();
            }
            return;
        }

        if (!mIsInOSMode)
        {

            if (mCurrentEquipment != null && mCurrentEquipment.is_placeable)
            {
                //already checked for canUse, so no need to worry about missing camera reference
                ProcessPlaceableEquipment();
            }

            //handle dropping equipment
            if (RPlayer.GetButtonDown(LIMENDefine.ACTION_DROP))
            {
                DropEquipment();
            }

            HandleInventory(ClampScrollWheelDelta(RPlayer.GetAxisRaw(LIMENDefine.ACTION_SCROLL)));
            if (mCurrentEquipment != null && mCurrentEquipment.require_aim)
            {
                fsmCaller.aim = mCurrentEquipment.is_persistent ? aimIKTarget.position.y + aim_y_offset_persistent : aimIKTarget.position.y + aim_y_offset;
                CMDAimHand(fsmCaller.aim); //Passes y aim value for IK sync, + 0.2 to offset a bit up
            }

            if (RPlayer.GetButton(LIMENDefine.ACTION_CROUCH))
            {
                // Only set if we have not set before
                if (mLocalPlayerHandLastRaisedStateCache == FSMCaller.DROP_HAND_STATE)
                {
                    mLocalPlayerHandLastRaisedStateCache = fsmCaller.GetFSMHandStateCache();
                    SetPlayerHandState(FSMCaller.NO_HAND_STATE);
                }
            }

            else if (RPlayer.GetButtonUp(LIMENDefine.ACTION_CROUCH))
            {
                SetPlayerHandState(mLocalPlayerHandLastRaisedStateCache);
                mLocalPlayerHandLastRaisedStateCache = FSMCaller.DROP_HAND_STATE;
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
        if (mCurrentSelectedEquipmentIndex < 0 || mCurrentSelectedEquipmentIndex > mPlayerEquipmentCache.Count - 1)
        {
            UnEquip();
            return;
        }
        if (mCurrentSelectedEquipmentIndex != -1 && mPlayerEquipmentCache[mCurrentSelectedEquipmentIndex].is_persistent)
        {
            if (!mHasPersistentItem)
            {
                Equip(mCurrentSelectedEquipmentIndex);
                mHasPersistentItem = true;
            }
        }
        else
        {
            //SelectionIndex can be passed in as -1, because we use it to trigger an unequip
            Equip(mCurrentSelectedEquipmentIndex);
        }
    }

    //[TODO] For control setting, expose 0.2 as scroll wheel delta sensitivity or equipment scroll sensitivity
    //04/17/2024 -> is this sensitivity better hard coded? Since bad sensitivity can cause weird behaviors 
    //If Controller, checks "prev" or "next" Action press
    int ClampScrollWheelDelta(float delta)
    {
        if (delta < -0.2f || RPlayer.GetButtonDown("Prev")) return -1;
        if (delta > 0.2f || RPlayer.GetButtonDown("Next")) return 1;
        return 0;
    }
    public void DropEquipment()
    {
        //If both are null, can't drop anything. If current is permanent also can't drop
        if ((mCurrentEquipment == null && mCurrentPersistent == null) || (mCurrentEquipment != null && mCurrentEquipment.is_permanent)) return;
        //if (GetComponent<BuildingManager>().enabled) return; //Prevent button fighting w/ building manager
        if (mCurrentEquipment != null)
        {
            mCurrentEquipment.ServerSetCanPickUp(true);
        }
        else if (mCurrentPersistent != null)
        {
            mCurrentPersistent.ServerSetCanPickUp(true);
        }
        DropUnEquip();
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
        if (mPlayerEquipmentCache.Count < OSAppInventory.MAX_LOADOUT_SIZE)
        {
            e.OnPickedUpLocalPlayer();
            mPlayerEquipmentCache.Add(e);
            Equip(mPlayerEquipmentCache.IndexOf(e));
            //Play equip audio
            LocalPlay2DAudio(pickUpAudioClip, 0.46f, false, 0.2f);

            //Vibrate
            RPlayer.SetVibration(1, 0.5f, 0.2f);
        }
    }

    public void UpdateSelectionIndex(int g)
    {
        mCurrentSelectedEquipmentIndex += g;
        //Clamp
        if (mCurrentSelectedEquipmentIndex < -1)
        {
            mCurrentSelectedEquipmentIndex = -1;
        }
        if (mCurrentSelectedEquipmentIndex > mPlayerEquipmentCache.Count - 1)
        {
            mCurrentSelectedEquipmentIndex = mPlayerEquipmentCache.Count - 1;
        }
    }
    #region Hand Control

    /// <summary>
    /// Set the player's hand state, invokes state change on local (this network client) first then syncs with server and broadcast to other clients
    /// </summary>
    /// <param name="state"></param>
    public void SetPlayerHandState(int state)
    {
        if (state == mLocalPlayerHandStateCache) return;

        mLocalPlayerHandStateCache = state;
        // Invoke hook first on client to prevent latency, bring hand up on local player first then sync with other clients
        HookOnHandStateChanged(state, state);
        CMDSetPlayerHandState(state);
    }
    [Command(requiresAuthority = false)]
    private void CMDSetPlayerHandState(int state)
    {
        mCurrentHandState = state;
    }

    /// <summary>
    /// This is invoked on each client
    /// </summary>
    private void ClientSetHandState(bool isFromHook)
    {
        //REMOVE THIS SHIT IF NO USE
        // The local player already had state change before hook/CMD to start hand state first and then sync with clients due to latency that would make it not smooth
        if (isFromHook && isLocalPlayer) return;
    }

    /// <summary>
    /// Show hand state is now a syncvar and this will be updated via hook for client player instances
    /// </summary>
    /// <param name="oldVal"></param>
    /// <param name="newVal"></param>
    private void HookOnHandStateChanged(int oldVal, int newVal)
    {
        fsmCaller.SetHandState(newVal);
    }
    #endregion

    #region Equipment Control
    /// <summary>
    /// Called when player dies, everything gets dropped and unequipped, including the tablet (which is not dropped)
    /// </summary>
    public void DropAllEquipments()
    {
        OffloadPlacementPreview();
        foreach (Equipment e in mPlayerEquipmentCache)
        {
            if (e.is_permanent)
            {
                continue;
            }
            e.OnDroppedLocalPlayer();
            e.ServerSetCanPickUp(true); //Drop equipment
            InternalUnEquip(e);
        }
        UnEquipTablet();
        SetPlayerHandState(-1);
        mPlayerEquipmentCache.Clear();
        mCurrentEquipment = null;
        mCurrentPersistent = null;
    }

    /// <summary>
    /// 03/05/2024 - Force an update to update the equipment's position and rotation in hand, called from equipment
    /// Note: This is not the regular place to update it, but rather a force update as a result of an event change,
    /// ie. Phone switching to scan mode needs to put it to horizontally
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="rot"></param>
    [Command(requiresAuthority = false)]
    public void CMDForceUpdateCurrentFollowTransform(Vector3 pos, Vector3 rot)
    {
        RPCUpdateCurrentFollowTransform(pos, rot);
    }

    /// <summary>
    /// RPC call from server to update follow transform
    /// <param name="pos"></param>
    /// <param name="rot"></param>
    [ClientRpc]
    public void RPCUpdateCurrentFollowTransform(Vector3 pos, Vector3 rot)
    {
        InternalUpdateCurrentFollowTransform(pos, rot);
    }

    /// <summary>
    /// Internal function to update current follow transform, separated from rpc because
    /// character changed callback is client-side, but on hosting client that's also server
    /// RPC will be executed thus non-hosting clients will get the call twice, wasting network bandwidth
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="rot"></param>
    private void InternalUpdateCurrentFollowTransform(Vector3 pos, Vector3 rot)
    {
        Character ch = player.GetCharacter();
        ch.GetHandEquipmentAttachment().localPosition = pos;
        ch.GetHandEquipmentAttachment().localEulerAngles = rot;
        mFollowLocalPositionCache = pos;
        mFollowLocalRotationCache = rot;
    }
    /// <summary>
    /// The client-side callback handling for EquipmentManager when character changes
    /// </summary>
    /// <param name="characterId"></param>
    public void OnPlayerCharacterChangedCallbackClient(uint characterId)
    {
        // Re-play animation to work around Unity not updating current Animator animation skeleton correctly despite Rebind() call
        fsmCaller.RefreshShowHandOnCharacterChange();
        InternalUpdateCurrentFollowTransform(mFollowLocalPositionCache, mFollowLocalRotationCache);
    }
    public void UnEquip()
    {
        SetPlayerHandState(-1);
        if (mCurrentEquipment != null && !mCurrentEquipment.is_persistent)
        {
            if (mCurrentEquipment.is_placeable)
            {
                OffloadPlacementPreview();
            }
            ServerShow(mCurrentEquipment, false);
            InternalUnEquip(mCurrentEquipment);
        }

        // Reset the last hand raised cache so switching equipment or unequip during crouch still drops hand
        // And when player stands up correct hand state would be applied
        mLocalPlayerHandLastRaisedStateCache = FSMCaller.DROP_HAND_STATE;
    }
    public void DropUnEquip()
    {
        SetPlayerHandState(-1);
        if (mCurrentEquipment != null && !mCurrentEquipment.is_permanent)
        {
            if (mCurrentEquipment.is_placeable)
            {
                OffloadPlacementPreview();
            }

            RemoveEquipment(mCurrentEquipment);
            mCurrentEquipment.OnDroppedLocalPlayer();
            InternalUnEquip(mCurrentEquipment);
        }
        else if (mCurrentPersistent != null)
        {
            RemoveEquipment(mCurrentPersistent);
            mCurrentPersistent.OnDroppedLocalPlayer();
            InternalUnEquip(mCurrentPersistent);
        }
    }

    /// <summary>
    /// Removes an equipment from player carrying-loadout
    /// </summary>
    /// <param name="e"></param>
    public void RemoveEquipment(Equipment e)
    {
        if (e == null) return;
        for (int i = mPlayerEquipmentCache.Count - 1; i >= 0; --i)
        {
            if (mPlayerEquipmentCache[i] == e)
            {
                mPlayerEquipmentCache.RemoveAt(i);
                return;
            }
        }
    }
    /// <summary>
    /// Unequips all equipments, including persistent (flashlight) but not ObserveOS tablet
    /// During regular going to the past, we don't need to unequip tablet
    /// </summary>
    public void ForceUnEquip(bool forceUnequipTablet = false)
    {
        //Drop hand if we are not unequipping tablet
        //If we have tablet equipped, we will unequip persistent but keep tablet (not drop hand)
        if (!mIsInOSMode)
        {
            SetPlayerHandState(-1);
        }

        if (mCurrentEquipment != null)
        {
            if (forceUnequipTablet && mIsInOSMode)
            {
                UnEquipTablet();
            }
            else if (!mIsInOSMode)
            {
                if (mCurrentEquipment.is_placeable)
                {
                    OffloadPlacementPreview();
                }
                ServerShow(mCurrentEquipment, false);
                InternalUnEquip(mCurrentEquipment);
            }
        }
        if (mCurrentPersistent != null)
        {
            ServerShow(mCurrentPersistent, false);
            InternalUnEquip(mCurrentPersistent);
        }
    }
    public void UnEquipPersistent()
    {
        SetPlayerHandState(-1);
        if (mCurrentPersistent != null)
        {
            ServerShow(mCurrentPersistent, false);
            InternalUnEquip(mCurrentPersistent);
        }
    }

    public void EquipTablet()
    {
        if (mIsInOSMode) return;
        mIsInOSMode = true;
        GlobalControls.Instance.DisablePlayerControls();
        interaction.EnablePlayerUI(false);
        player.FreezeCameraAnimator();
        //player.SetAnimatorSpeed(0);
        mouse_look.enabled = false;
        UnEquip();
        mCurrentEquipment = mObserveTablet;
        mObserveTablet.GetComponent<ObserveOS>().LaunchOS(cam);
        SetPlayerHandState(mObserveTablet.HandIKType);
        RefreshBatteryDisplay(mObserveTablet);
        CMDEquip(mObserveTablet);
        mTempCameraPosition = cam.transform.localPosition;
        mTempCameraRotation = cam.transform.localRotation;
        mMoveCameraFocusCo = StartCoroutine(DelayCameraAim(mObserveTablet.transform));
        // Show head for apps that view player, ie. Player app
        player.GetCharacter().SetHeadStateClient(true);
    }
    IEnumerator DelayCameraAim(Transform t)
    {
        while (Quaternion.Angle(cam.transform.localRotation, Quaternion.Euler(AIM_ROTATION_TABLET_OVERRIDE)) > 0.01f)
        {
            cam.transform.localRotation = Quaternion.Slerp(cam.transform.localRotation, Quaternion.Euler(AIM_ROTATION_TABLET_OVERRIDE), Time.deltaTime * 6f);
            yield return null;
        }

        fsmCaller.anim.speed = 0; //Freeze animation so tablet stays still
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
        if (!mIsInOSMode) return;
        if (mMoveCameraFocusCo != null)
        {
            StopCoroutine(mMoveCameraFocusCo);
        }
        //Clear again just to make sure
        player.ClearDisplayControlHints();

        fsmCaller.anim.speed = 1; //Re-enable player animator speed
        GlobalControls.Instance.EnablePlayerControls();
        interaction.EnablePlayerUI(true);
        player.UnFreezeCameraAnimator();
        //player.SetAnimatorSpeed(1);
        cam.transform.localPosition = mTempCameraPosition;
        cam.transform.localRotation = mTempCameraRotation;
        mouse_look.enabled = true;
        mObserveTablet.GetComponent<ObserveOS>().ShutdownOS();
        UnEquip();
        Equip(mCurrentSelectedEquipmentIndex);

        // Hide player head to prevent clipping through camera
        player.GetCharacter().SetHeadStateClient(false);
        mIsInOSMode = false;
    }
    public void Equip(int index)
    {
        if (index < 0 || index > mPlayerEquipmentCache.Count - 1)
        {
            UnEquip();
            return;
        }
        UnEquip();
        mCurrentEquipment = mPlayerEquipmentCache[index];
        if (mPlayerEquipmentCache[index].is_persistent)
        {
            mCurrentPersistent = mPlayerEquipmentCache[index];
        }
        RefreshBatteryDisplay(mPlayerEquipmentCache[index]);
        SetPlayerHandState(mPlayerEquipmentCache[index].HandIKType);
        CMDEquip(mPlayerEquipmentCache[index]);
        //LocalPlay2DAudio(equip_audio, .086f, true); Commented for now, audio kind of annoying
        //CMDSpawn(index);
    }

    /// <summary>
    /// Refresh the player UI battery display
    /// </summary>
    private void RefreshBatteryDisplay(Equipment equipped)
    {
        if (equipped.is_permanent || !equipped.useBattery)
        {
            player.mBatteryDisplay.SetDisplayVisible(false);
            return;
        }

        // Only subscribe callback here, un subscribe is handle by Equipment to also handle player disconnect
        player.mBatteryDisplay.SetDisplayVisible(true);
        equipped.OnClientBatteryAmountChanged += player.mBatteryDisplay.UpdateBatteryAmount;
    }
    /// <summary>
    /// Internal function to process equipment unequip
    /// </summary>
    private void InternalUnEquip(Equipment e)
    {
        if (e == null)
        {
#if UNITY_EDITOR
            Debug.LogError("Error: Trying to unequip a null item!");
            Debug.Break();
#endif
            return;
        }
        e.OnUnEquipLocalPlayer();
        e.SetCanUse(false);
        player.ClearDisplayControlHints();
        player.mBatteryDisplay.SetDisplayVisible(false);
        //Clear current and current_persistent to null if they equal
        if (mCurrentEquipment == e)
        {
            mCurrentEquipment = null;
        }

        if (mCurrentPersistent == e)
        {
            mCurrentPersistent = null;
            mHasPersistentItem = false;
        }

        //If current_persistent still not null, meaning it was not dropped, display its hints
        if (mCurrentPersistent != null)
        {
            player.DisplayControlHints(mCurrentPersistent.GetControlHints());
        }
    }
    public void LocalPlay2DAudio(AudioClip clip, float volume, bool singlePlay, float delay)
    {
        if (!isLocalPlayer)
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
            StartCoroutine(DelayPlay2DAudio(delay, clip));
        }
    }
    IEnumerator DelayPlay2DAudio(float delay, AudioClip clip)
    {
        yield return new WaitForSeconds(delay);
        m_audio.PlayOneShot(clip);
    }
    [Command(requiresAuthority = false)]
    public void CMDEquip(Equipment e)
    {
        e.SetIsVisible(true);
        e.ServerSetCanPickUp(false);
        e.SetActiveUsingPlayerId(netId);

        // This is needed to let clients change the attachment transform to specific equipment instruction
        // Change is reflected to all client copies of the Player, not equipment, thus call from here
        RPCUpdateCurrentFollowTransform(e.position, e.rotation);
        TRPCInitializeEquipment(e);
    }


    [Command(requiresAuthority = false)]
    public void ServerShow(Equipment e, bool visible)
    {
        e.SetIsVisible(visible);
    }

    [TargetRpc]
    public void TRPCInitializeEquipment(Equipment e)
    {
        e.SetCanUse(true);
        //Display Control Hints on UI
        player.DisplayControlHints(e.GetControlHints());
    }
    #endregion

    #region Callbacks
    /// <summary>
    /// Callback used for knowing if the current equipment is exhausted, subscribed during local add equipment callback
    /// This way since multi-stack count is stored on server, the hook will invoke client-side callback that if current is the exhausted
    /// Also used for exhaust-able equipment like single-use fulu
    /// We will drop unequip current
    /// </summary>
    /// <param name="e"></param>
    public void OnEquipmentExhausted(Equipment e)
    {
        //Always remove equipment, since player may have already switched to another equipment
        //But we need to remove from player carry
        RemoveEquipment(e);
        if (e == mCurrentEquipment)
        {
            //Remove the placement preview and unequip the equipment with arm dropping
            OffloadPlacementPreview();
            DropUnEquip();
        }
    }
    void OnLocalPlayerTimeframeChanged(bool isPast)
    {
        if (isPast)
        {
            //Going to the past, we need to store the current canUse value
            mLastCanUseValueCache = canUse;
            canUse = false;
        }
        else
        {
            canUse = mLastCanUseValueCache;
        }
    }

    void OnLocalPlayerLimenBreakOccured()
    {
        canUse = mLastCanUseValueCache;
    }
    #endregion
}
