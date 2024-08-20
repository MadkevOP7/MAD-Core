//© 2022 by MADKEV Studio, all rights reserved
//Handling each individual equipment, local class, handle client sync through EquipmentManager
//Note: I believe this approach is better, as we aren't sending network data for transform sync, rather having each client side follow the set target transform
//Known issue for late joiners, we must re sync position for dropped equipment
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System;
public class Equipment : NetworkBehaviour
{
    #region Defines
    static Vector3 UNEQUIPPED_POSITION = new Vector3(-9999, -9999, -9999);
    public const float RC_EQUIPMENT_UP_COLLISION_SIDE_DOT_PRODUCT_THRESHOLD = 0.86f;
    const float DEFAULT_BATTERY_DEPLETION_DURATION = 8.6f;
    public enum PlacementAxis
    {
        Forward,
        Up
    }
    #endregion

    #region Invisible Public & Privates
    [HideInInspector]
    [SyncVar(hook = nameof(HookAddEquipmentToOriginPlayerEquipmentList))]
    public GameObject originPlayer;

    // The reference to the current player, set from hook of the p_follow
    protected Player mCurrentHoldingPlayer;

    [HideInInspector]
    public int id = -1; //For EquipmentStorage and save/load

    [HideInInspector]
    [SyncVar(hook = nameof(HookPlacementPos))]
    public Vector3 placement_pos;
    [HideInInspector]
    [SyncVar(hook = nameof(HookPlacementRot))]
    public Quaternion placement_rot;
    [Header("Runtime")]
    [HideInInspector]
    [SyncVar(hook = nameof(HookVisible))]
    public bool visible;
    [HideInInspector]
    public bool binded; //for determining when the first player has binded, then if the player becomes null equipment drops (player disconnect)
    [HideInInspector]
    [SyncVar(hook = nameof(HookActiveUserPlayerIDChanged))]
    public uint mActiveUsingPlayerId; //On the server, change this for updating owner
    [HideInInspector]
    public Transform follow;
    [HideInInspector]
    public Vector3 disconnect_drop_pos; //Last follow pos
    [SyncVar(hook = nameof(HookOnCanPickUpChanged))]
    private bool canPickUp = true; //Start with true so first equip can trigger hook
    [HideInInspector]
    public bool isCreatedFromSave = false; //On the server only, do not sync
    protected AudioSource mInternalAudioSource;
    #endregion

    [Header("Equipment Info")]
    public Sprite m_image;
    public string m_name = "Default Equipment";
    public AudioClip m_dropAudio;
    public bool require_aim;
    public bool allowDropArm = true; //Hold object down like running with phone, allow for smaller objects and false for larger ones
    public bool is_persistent; //For flashlight which doesn't auto unEqip
    public bool is_placeable; //For equipments that can be placed, (cameras, sensors, etc)
    public bool mCanRotatePlacementPreviw = false;
    [Tooltip("Placement Axis determines the front facing of the object so when auto rotating to align with surface the specified side will face the player")]
    public PlacementAxis mPlacementAxis = PlacementAxis.Forward;
    public bool is_permanent; //For tablet, which players can't drop
    public GameObject placement_preview; //For placing previews without network identity
    public bool constraint_preview_direction = true; //Have object always face forward, such as motion detectors always face away from wall, etc
    public float placement_offset = 2f; //Placement deviation from wall, etc Moves in the direction of facing (v3.forward)
    public int HandIKType = 0;
    public bool useBattery = true;

    // EVENTS
    // Invoked from hook
    public event Action<bool> OnClientCanPickUpStateChanged;
    public event Action<uint> OnClientBatteryAmountChanged;
    // Invoked on each client when equipment is equipped or unequipped. When unequipped the equipment "disappears“ hence visibility change
    public event Action<bool> OnClientEquippedVisibilityChanged;
    //0 = default phone hand IK raise, 1 = Motion Detector grab -1 = none
    [Header("Initialization Storage")]
    public Vector3 position;
    public Vector3 rotation;

    [Header("Runtime")]
    [SyncVar(hook = nameof(HookOnEquipmentTurnedOnStateChanged))]
    protected bool mIsTurnedOn = false;
    protected List<ControlHintInstruction> mControlHints = new List<ControlHintInstruction>();
    protected bool mIsPlaced; //For placeable equipments, local
    protected BoxCollider mBoxCollider;
    protected Rigidbody mRigidBody;
    [SyncVar(hook = nameof(HookOnBatteryAmountChanged))]
    private uint mBatteryAmount = 100;
    private bool canUse; //Holds LocalPlayer can USe
    //Cache for time frame change, we cache originally inactive components
    //When player changes to past, need to enable things and disable originally disabled components
    private List<Renderer> originallyInactiveRendererCache = new List<Renderer>();
    private List<AudioSource> originallyInactiveAudioSourceCache = new List<AudioSource>();

    public virtual void Awake()
    {
        // Get and cache references
        mInternalAudioSource = gameObject.AddComponent<AudioSource>();
        mBoxCollider = GetComponent<BoxCollider>();
        if (mBoxCollider == null)
        {
            mBoxCollider = gameObject.AddComponent<BoxCollider>();
        }

        mRigidBody = GetComponent<Rigidbody>();
        if (mRigidBody == null)
        {
            mRigidBody = gameObject.AddComponent<Rigidbody>();
        }

        mRigidBody.useGravity = false;
    }
    public virtual void Update() { }

    #region Virtuals
    /// <summary>
    /// Creates the hint instructions list
    /// </summary>
    public virtual void InitControlsHints()
    {
        //Don't add for tablet
        if (!is_permanent)
        {
            //Add drop option and placeable hint for placeable equipment
            mControlHints.Add(new ControlHintInstruction(LIMENDefine.ACTION_DROP, LIMENDefine.ACTION_DROP_LEAN_KEY, false));
        }
        if (is_placeable)
        {
            mControlHints.Add(new ControlHintInstruction(LIMENDefine.ACTION_PLACE, LIMENDefine.ACTION_PLACE_LEAN_LEY, false));
            if (mCanRotatePlacementPreviw)
                mControlHints.Add(new ControlHintInstruction(LIMENDefine.ACTION_ROTATE, LIMENDefine.ACTION_ROTATE_LEAN_KEY, false));
        }
    }
    public virtual bool GetIsMultiStackEquipment()
    {
        return false;
    }

    public virtual bool GetIsRCEquipment() { return false; }
    public virtual int GetCount()
    {
        return 1;
    }

    // LocalPlayer means only invoked on Local player (called from Equipment Manager), Client means invoked on all client instances (called from Hook)
    public virtual void OnPickedUpLocalPlayer() { }
    public virtual void OnUnEquipLocalPlayer() { }
    public virtual void OnDroppedLocalPlayer() { }
    public virtual void OnEquipmentPlacedClient() { }
    public virtual void OnRefreshServer() { }

    /// <summary>
    /// Callback on all clients, should not be used to handle server refresh logic or local player refresh logic, use their corresponding callbacks instead
    /// </summary>
    public virtual void OnRefreshClient() { }
    public virtual void OnRefreshLocalPlayer() { }
    public virtual void OnPickedUpClient() { }
    #endregion

    #region Core
    public virtual void LateUpdate()
    {
        if (isServer)
        {
            InternalCheckDisconnect();
        }

        //Local player view syncs in Update
        if (visible && !canPickUp)
        {
            Sync();
        }

        //This needs to be set on all clients
        InternalUpdateDisconnectPosition();
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
        // Only fire when equipment is in dropped state and is not RC Equipment as RC would cause false positives because of WheelCollision
        // Thus, for RC only fire if collision side is not bottom
        if (canPickUp && (!GetIsRCEquipment() || Vector3.Dot(collision.GetContact(0).normal, Vector3.up) <= RC_EQUIPMENT_UP_COLLISION_SIDE_DOT_PRODUCT_THRESHOLD))
        {
            //Hits ground
            //7 is player layer: LayerMask.nametolayer optimization
            if (collision.relativeVelocity.magnitude > 0.68f && collision.gameObject.layer != LIMENDefine.LAYER_PLAYER_INT)
            {
                Play3DAudio(m_dropAudio, 0.268f);
            }

            //Sync transform data
            if (isServer)
            {
                ServerSetTransformData(transform.position, transform.eulerAngles);
            }
        }
    }

    public void Play3DAudio(AudioClip clip, float volume)
    {
        mInternalAudioSource.spatialBlend = 1;
        mInternalAudioSource.maxDistance = 3;
        mInternalAudioSource.PlayOneShot(clip, volume);
    }

    void InternalUpdateDisconnectPosition()
    {
        if (follow != null)
        {
            disconnect_drop_pos = follow.transform.position;
        }
    }

    //Since player is available on all clients, including the server. The check should only be done on server
    [Server]
    void InternalCheckDisconnect()
    {
        //If equipment owner disconnects, drop the equipment
        if (mCurrentHoldingPlayer == null && binded)
        {
            binded = false;
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
    }

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

    [Command(requiresAuthority = false)]
    public void ServerSetRotation(Vector3 rotation)
    {
        RPCSetRotation(rotation);
    }

    [ClientRpc]
    void RPCSetPosition(Vector3 position)
    {
        transform.position = position;
    }

    [ClientRpc]
    void RPCSetRotation(Vector3 rotation)
    {
        transform.eulerAngles = rotation;
    }
    #endregion
    /// <summary>
    /// Called whenever we need to refresh follow transform. Use case would be when player changes character while holding an equipment.
    /// The follow transform would be null but we can update again from here
    /// </summary>
    private void UpdateFollowTarget()
    {
        if (HandIKType == -1)
        {
            follow = mCurrentHoldingPlayer.GetCharacter().GetShoulderMount();
        }
        else
        {
            follow = mCurrentHoldingPlayer.GetCharacter().GetHandEquipmentAttachment();
        }
    }
    protected void HookOnEquipmentTurnedOnStateChanged(bool oldVal, bool newVal)
    {
        OnEquipmentOnOffStateChangedClient(newVal);
    }
    public virtual void HookActiveUserPlayerIDChanged(uint oldVal, uint newVal)
    {
        mCurrentHoldingPlayer = NetworkClient.spawned[mActiveUsingPlayerId].GetComponent<Player>();
        UpdateFollowTarget();
        if (isServer)
            OnRefreshServer();
        OnRefreshClient();
        if (mCurrentHoldingPlayer.isLocalPlayer)
            OnRefreshLocalPlayer();
        binded = true;
    }

    public virtual void HookOnBatteryAmountChanged(uint oldVal, uint newVal)
    {
        OnClientBatteryAmountChanged?.Invoke(newVal);
        if (oldVal > 0 && newVal == 0)
            OnBatteryDepleted();

        else if (oldVal == 0 && newVal > 0)
            OnBatteryDepletionRestored();
    }
    public void Sync()
    {
        if (!visible)
        {
            return;
        }

        if (!mIsPlaced)
        {
            if (!follow && mCurrentHoldingPlayer)
            {
                UpdateFollowTarget();
            }

            // We still need to make sure here that follow isn't null as the mCurrentHoldingPlayer could've been null as well
            if (follow)
            {
                transform.position = follow.position;
                transform.rotation = follow.rotation;
            }
        }
    }
    public void HookVisible(bool oldVal, bool newVal)
    {
        LocalSetEquippedVisibility(newVal);
        OnClientEquippedVisibilityChanged?.Invoke(newVal);
    }
    #endregion

    #region Initialization
    /// <summary>
    /// Address race condition 10/30/2023 where RPC receives reference for Server spawned GameObject could be null
    /// </summary>
    public void HookAddEquipmentToOriginPlayerEquipmentList(GameObject oldVal, GameObject newVal)
    {
        if (null == newVal) return;

        //Only fire this hook on local player since it's for original owning player, thus that local player only
        if (false == newVal.GetComponent<EquipmentManager>().isLocalPlayer) return;

        //For Tablet, callback to start launch OS and set the observe_tablet reference
        //For regular equipment, add to player_equipment list
        if (m_name == LIMENDefine.NAME_OBSERVE_TABLET)
        {
            newVal.GetComponent<EquipmentManager>().LocalPlayerAddTabletCallback(this);
            //For tablet, we subscribe time frame client side refresh so when owning player state changed, ie is in past, this equipment will be visible to other players in the past
            newVal.GetComponent<Player>().OnLocalPlayerTimeframeChangedClientCallback += OnOwningPlayerTimeframeChangedPlayerPastStateDependentClientSideCallback;
            return;
        }

        newVal.GetComponent<EquipmentManager>().LocalPlayerAddEquipmentCallback(this);
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
        mIsPlaced = true;
        UpdateRotationClient(placement_rot);
    }

    public void HookPlacementRot(Quaternion oldVal, Quaternion newVal)
    {
        UpdateRotationClient(newVal);
    }


    //Prevent hook rotation not called again if position is the same
    void UpdateRotationClient(Quaternion rot)
    {
        transform.rotation = rot;
        mIsPlaced = true;
        ServerSetCanPickUp(true);
    }

    public void HookOnCanPickUpChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            // Clear battery events because equipment is dropped and owning player may have disconnected thus cannot unsubscribe on their end
            OnClientBatteryAmountChanged = null;
            if (mIsPlaced)
            {
                SetEquipmentStatus(true, true, true, false, true, LIMENDefine.TAG_EQUIPMENT);
                mRigidBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                OnEquipmentPlacedClient();
            }
            else
            {
                SetEquipmentStatus(true, false, true, true, false, LIMENDefine.TAG_EQUIPMENT);
                mRigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // Ensure dynamic collision detection
                transform.position = disconnect_drop_pos;
                ApplyDropPhysics();
            }
        }
        else
        {
            HandlePickUpClient();
        }

        OnClientCanPickUpStateChanged?.Invoke(newVal);
    }
    private void HandlePickUpClient()
    {
        // Reset scale and physics for pick up
        // transform.localScale /= 2;
        SetEquipmentStatus(false, true, false, false, true, LIMENDefine.TAG_EQUIPMENT);
        mIsPlaced = false;
        OnPickedUpClient();
    }

    private void SetEquipmentStatus(bool colliderEnabled, bool isTrigger, bool detectCollisions, bool useGravity, bool isKinematic, string tag)
    {
        mBoxCollider.enabled = colliderEnabled;
        mBoxCollider.isTrigger = isTrigger;
        mRigidBody.detectCollisions = detectCollisions;
        mRigidBody.useGravity = useGravity;
        mRigidBody.isKinematic = isKinematic;
        gameObject.tag = tag;
        canUse = false;
    }

    private void ApplyDropPhysics()
    {
        if (isCreatedFromSave)
            return;

        Vector3 forwardDir = mCurrentHoldingPlayer != null ? mCurrentHoldingPlayer.transform.forward : transform.forward;
        Vector3 upwardDir = mCurrentHoldingPlayer != null ? mCurrentHoldingPlayer.transform.up : transform.up;
        mRigidBody.AddForce(forwardDir * 4, ForceMode.Impulse);
        mRigidBody.AddForce(upwardDir * 1, ForceMode.Impulse);
    }

    [Command(requiresAuthority = false)]
    public void ServerDestroy()
    {
        NetworkServer.Destroy(gameObject);
    }

    [Command(requiresAuthority = false)]
    public void ServerSetCanPickUp(bool state)
    {
        canPickUp = state;
        visible = true;
        Debug.Log(m_name + " has been set as pickup able: " + canPickUp);
    }

    public bool IsPickupable()
    {
        return canPickUp;
    }

    #region Late Join Force Resync
    public override void OnStartClient()
    {
        //Sets current GO to Interactable layer, and all child with layer default to Interactable
        //Reason: For time frame filtering where present can't see past equipment and past can't see present
        SetThisAndAllImmediateChildLayer(LayerMask.NameToLayer(LIMENDefine.LAYER_EQUIPMENT), 0);
        InitControlsHints();
        //Time frame manager is only in games, not Lobby
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != LIMENDefine.SCENE_LOBBY)
        {
            GameManager.AddEquipmentToRuntimeCacheClient(this);
            BaseTimeframeManager.Instance.OnRefreshLocalPlayerIsPastState += OnLocalPlayerTimeframeChanged;
            BaseTimeframeManager.Instance.OnLocalPlayerLimenBreakoccured += OnLocalPlayerLimenBreakOccured;
        }
        if (!isServer)
        {
            CMDForceResyncTransform();
        }
    }

    /// <summary>
    /// For late joiners to request server re sync position and rotation
    /// </summary>
    [Command(requiresAuthority = false)]
    void CMDForceResyncTransform(NetworkConnectionToClient sender = null)
    {
        RPCResyncTransform(sender, transform.position, transform.eulerAngles);
    }


    /// <summary>
    /// Syncs transform to late joiner that requested the re sync
    /// </summary>
    /// <param name="target"></param>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    [TargetRpc]
    void RPCResyncTransform(NetworkConnectionToClient target, Vector3 position, Vector3 rotation)
    {
        transform.position = position;
        transform.eulerAngles = rotation;
    }
    #endregion
    #endregion

    #region Callbacks
    /// <summary>
    /// Client callback to respond to the equipment being turned On/Off, override to implement response
    /// </summary>
    public virtual void OnEquipmentOnOffStateChangedClient(bool state)
    {

    }
    /// <summary>
    /// Callback when battery is exhausted to 0, override to handle custom effects when the equipment is dead/not usable
    /// </summary>
    public virtual void OnBatteryDepleted()
    {

    }

    /// <summary>
    /// Callback when the battery is previously depleted (at 0), and has now been restored with charge of at least 1 or more.
    /// Override to handle turning equipment back on
    /// </summary>
    public virtual void OnBatteryDepletionRestored()
    {

    }
    /// <summary>
    /// This callback should only be used for tablet, as the tablet is both visible in past and present, but dependent on the owning player and local player state
    /// Ie. If this tablet is from a player in the past, we can only see it if we are in the past, vise versa for present
    /// Other equipment simply go invisible in the past, thus we don't need to worry about whether their owning player is in past or present
    /// The reason for this callback is to prevent race condition, as isPlayerInPast syncvar updated on the server, but this is client side, thus
    /// the instance we refresh visibility, the syncvar may have not yet been reflected on the client, thus a callback after that update is good
    /// <para>The owner of this equipment state has changed and this is the client side callback for dat</para>
    /// </summary>
    public virtual void OnOwningPlayerTimeframeChangedPlayerPastStateDependentClientSideCallback(bool isPast)
    {
        //Here we simply change the layer for Tablet, like a syncvar hook but we are riding the hook from owning player to save network bandwidth yay!
        //This just validates that we are only doing this for the tablet & not the local client one! (we are changed if we are an equipment in another player's world!)
        if (is_permanent)
        {
            //Put tablet in past layer, present players camera cannot see this layer, past player
            //camera cull mask can see this
            //This is because while other equipment just not visible in the past, the tablet is visible to other past players if owner is in the past
            int pastLayer = LayerMask.NameToLayer(LIMENDefine.LAYER_EQUIPMENT_PAST);
            int presentLayer = LayerMask.NameToLayer(LIMENDefine.LAYER_EQUIPMENT);
            if (isPast)
            {
                SetThisAndAllImmediateChildLayer(pastLayer, presentLayer);

            }
            else
            {
                SetThisAndAllImmediateChildLayer(presentLayer, pastLayer);
            }
        }
    }

    /// <summary>
    /// This callback is used for all equipment except the tablet, as these will just go invisible for past players, and visible for present players
    /// </summary>
    /// <param name="isPast"></param>
    public virtual void OnLocalPlayerTimeframeChanged(bool isPast)
    {
        //Permanent (tablet) handling is moved to owning player callback as the layer for it depends on if the owning player (player using it) is in the present or past, not the local player
        //Local player in Player.cs has camera cull mask handling already, and for permanent the owning player callback handles layer change
        if (!is_permanent)
        {
            //Non-tablet equipment will change visibility - no equipment visible in past
            LocalSetCurrentTimeframeVisibility(!isPast);
        }
    }

    /// <summary>
    /// Warning: This is strictly used for time frame changes (past/present) change
    /// Changes by visibility are local, non-networked
    /// Changes include enable/disable Renderer, AudioSource, and Colliders
    /// Note: This function is local, runs on client
    /// </summary>
    /// <param name="visible"></param>
    public virtual void LocalSetCurrentTimeframeVisibility(bool visible)
    {
        if (!visible)
        {
            //We are going to be disabling components, clear cache as we will need recompute
            originallyInactiveRendererCache.Clear();
            originallyInactiveAudioSourceCache.Clear();
        }

        //GetComponent<T>(bool includeInactive) -> the include inactive is for searching on inactive GameObjects
        //Disabled components can still be found. We currently include only on active GameObjects
        //As disabled GameObjects won't have effect anyways
        //Keep this for line render (other possible)
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (!visible && !renderer.enabled)
            {
                //If we are going to set things to disabled, and this component is already disabled, add to originallyDisabled cache
                originallyInactiveRendererCache.Add(renderer);
            }

            renderer.enabled = visible;
        }

        foreach (AudioSource audio in GetComponentsInChildren<AudioSource>())
        {
            if (!visible && !audio.enabled)
            {
                //If we are going to set things to disabled, and this component is already disabled, add to originallyDisabled cache
                originallyInactiveAudioSourceCache.Add(audio);
            }

            audio.enabled = visible;
        }

        if (visible)
        {
            //If we just set everything to visible, we need to re-disable originally inactive components
            foreach (Renderer renderer in originallyInactiveRendererCache)
            {
                renderer.enabled = false;
            }

            foreach (AudioSource audio in originallyInactiveAudioSourceCache)
            {
                audio.enabled = false;
            }
        }
    }

    /// <summary>
    /// Apply changes for when an equipment is equipped and unequipped changing its visibility
    /// ie. We can turn off light sources for performance
    /// </summary>
    /// <param name="visible"></param>
    public virtual void LocalSetEquippedVisibility(bool visible)
    {
        if (!visible)
        {
            transform.position = UNEQUIPPED_POSITION;
            canUse = false;
        }
    }

    public virtual void OnLocalPlayerLimenBreakOccured()
    {
        //Permanent (tablet) handling is moved to owning player callback as the layer for it depends on if the owning player (player using it) is in the present or past, not the local player
        //Local player in Player.cs has camera cull mask handling already, and for permanent the owning player callback handles layer change
        if (!is_permanent)
        {
            //Non-tablet equipment will change visibility - no equipment visible in past, all visible in present
            LocalSetCurrentTimeframeVisibility(true);
        }
    }
    #endregion

    #region Helper

    /// <summary>
    /// Sets the GO to setToLayer, and all immediate children to setToLayer layer, if they (children) are currently in the layer of filterLayer or default
    /// Current (parent) GO layer is set regardless
    /// </summary>
    private void SetThisAndAllImmediateChildLayer(int setToLayer, int filterLayer)
    {
        gameObject.layer = setToLayer;
        foreach (Transform t in transform)
        {
            if (t.gameObject.layer == filterLayer || t.gameObject.layer == 0)
            {
                t.gameObject.layer = setToLayer;
            }
        }
    }
    #endregion

    #region Getter/Setter
    public uint GetBatteryAmount() { return mBatteryAmount; }
    public bool GetHasBatteryLeft() { return mBatteryAmount > 0; }
    public bool GetIsPlaced() { return mIsPlaced; }

    /// <summary>
    /// This returns the cached collider set in Awake(), but could cause race condition if trying to get during the creation instance it would still be null
    /// </summary>
    /// <returns></returns>
    public Collider GetColliderNonRaceConditionSafe() { return mBoxCollider; }
    public List<ControlHintInstruction> GetControlHints() { return mControlHints; }
    public bool GetCanPickUp() { return canPickUp; }
    public void SetCanUse(bool val) { canUse = val; }

    /// <summary>
    /// Returns true if this equipment can be used on the client, also checks for battery if the equipment uses it
    /// </summary>
    /// <returns></returns>
    public bool GetCanUse()
    {
        return canUse && (!useBattery || GetHasBatteryLeft());
    }
    #endregion

    /// <summary>
    /// Turns the equipment On/Off
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CMDToggleEquipmentOnOffState()
    {
        ServerSetEquipmentTurnedOnState(!mIsTurnedOn);
    }

    [Server]
    protected void ServerSetEquipmentTurnedOnState(bool state)
    {
        mIsTurnedOn = state;
    }
    /// <summary>
    /// [Server Only] Drains the battery
    /// </summary>
    [Server]
    public void ServerDepleteBattery(uint amount)
    {
        if (amount >= mBatteryAmount)
        {
            mBatteryAmount = 0; // Ensure battery doesn't go negative
        }
        else
        {
            mBatteryAmount -= amount;
        }
    }

    /// <summary>
    /// [Server Only] Adds charge back to battery
    /// </summary>
    /// <param name="amount"></param>
    [Server]
    public void ServerChargeBattery(uint amount)
    {
        mBatteryAmount = Math.Min(mBatteryAmount + amount, 100);
    }
}
