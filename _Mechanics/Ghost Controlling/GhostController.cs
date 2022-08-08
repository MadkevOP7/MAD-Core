using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Footsteps;
public class GhostController : NetworkBehaviour
{
    private bool isLocalPlayerBool;
    [Header("Audio")]
    public AudioClip killing;
    public AudioClip player_eliminated;
    [Header("Components")]
    public Player player;
    public Character human;
    public Camera humanCam;
    public Camera ghostCam;
    [Header("Player Parts")]
    public GameObject[] p_Parts; //For disabling player control
    public PlayMakerFSM[] p_Controls;
    [Space()]
    [Header("Ghosts Container")]
    public GameObject g_slender;
    [Header("Runtime Debug")]
    public bool isKiller;
    public bool isGhost;
    public GameObject currentGhost;

    //Position Sync
    [Header("Positions")]
    public float y_diff; //Tracking y diff
    //Cameras
    private Vector3 ghostCamStartPos;
    private Vector3 ghostCamStartScale;
    private Quaternion ghostCamStartRot;
    //Restore
    private Vector3 lastGhostPos;
    private bool restoreGhostPos = false;


    //Control bools
    private bool isPinned;
    private InteractionDetection interactionDetection;
    public virtual void OnEnable() //Use enable
    {

        //Initiliaze Characters
        RememberGhostCamOriginalStats();
        human = player.currentCharacter;
    }
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        isLocalPlayerBool = true;

        //Restore position from last Ghost Pos
        if (restoreGhostPos)
        {
            transform.position = new Vector3(lastGhostPos.x, lastGhostPos.y-y_diff + 0.2f, lastGhostPos.z);
            isGhost = false;
            restoreGhostPos = false;
        }
        if (GetComponent<InteractionDetection>() != null)
        {
            interactionDetection = GetComponent<InteractionDetection>();
        }
    }
    #region Player Attacks
    public void RememberGhostCamOriginalStats()
    {
        ghostCamStartPos = ghostCam.transform.localPosition;
        ghostCamStartRot = ghostCam.transform.localRotation;
        ghostCamStartScale = ghostCam.transform.localScale;
    }
    public void ResetGhostCamStatsToOriginal() //Reset ghostcam
    {
        ghostCam.transform.localPosition = humanCam.transform.localPosition;
        ghostCam.transform.localRotation = humanCam.transform.localRotation;
        ghostCam.transform.localScale = ghostCamStartScale;
    }
    public virtual void AttackPlayer(GameObject p, int hit)
    {
        AudioSource killerAudio = gameObject.AddComponent<AudioSource>();
        killerAudio.spatialBlend = 0;
        p.GetComponent<Player>().OnSanityHit(hit);
        killerAudio.PlayOneShot(killing, 1f);
        if (p.GetComponent<Player>().sanity - hit <= 0)
        {
            killerAudio.PlayOneShot(player_eliminated, 1f);
        }

        

    }

    //TODO implement near player static effect: when phantasmic entity is near player, player's screen starts glitching and static whitenoise

    public virtual void OnSeeingPlayer(GameObject p) //Raycast/in view from update detects if player is in view
    {
        //TODO
    }

    #endregion
    #region Server/Client Side Sync

    [Command]
    public void CMDRequestNetDisablePlayer()
    {
        RPCDisablePlayer();
    }
    [ClientRpc]
    public void RPCDisablePlayer()
    {
        DisableClientPlayer();
    }

    [Command(requiresAuthority = false)]
    public void CMDRequestNetEnablePlayer(NetworkConnectionToClient conn)
    {
        NetworkServer.ReplacePlayerForConnection(conn, this.gameObject, true);
        RPCEnablePlayer();
    }

    [Command(requiresAuthority =false)]
    public void ServerDestroy(GameObject g)
    {
        NetworkServer.Destroy(g);
    }
    [ClientRpc]
    public void RPCEnablePlayer()
    {
        EnableClientPlayer();
    }
    [Command]
    public void CMDSpawnGhost(int type, GameObject player, float yDiff)
    {
        GameObject temp = null; //Temp storage

        //Ghost types for difference
        if (type == 0)
        {
            temp = Instantiate(g_slender, new Vector3(transform.position.x, transform.position.y + yDiff, transform.position.z), transform.localRotation);
        }

        //Server finalize spawn, check if null return ghostassignment error
        if (temp != null)
        {
            NetworkServer.Spawn(temp);
            NetworkServer.ReplacePlayerForConnection(GetComponent<NetworkIdentity>().connectionToClient, temp, true);
            GiveGhostOwnership(temp);
        }
        else
        {
            Debug.LogError("Error: CMDSpawnGhost has null ghost type referenced");
        }
        
    }
    [TargetRpc]
    public void GiveGhostOwnership(GameObject g)
    {
        
        g.GetComponent<GhostHelper>().lastHumanPos = transform.position;
        g.GetComponent<GhostHelper>().lastHumanRot = transform.rotation;
        g.GetComponent<GhostHelper>().lastHumanCamRot = humanCam.transform.localRotation;
        g.GetComponent<GhostHelper>().player = this;
        g.GetComponent<GhostHelper>().isOwner = true;
        currentGhost = g;
        g.GetComponent<GhostHelper>().ghostControl = this;
    }
    #endregion
    #region Local Functions
    public void DisableClientPlayer()
    {
        foreach (GameObject g in p_Parts)
        {
            g?.SetActive(false);
        }
        human?.gameObject.SetActive(false);
        GetComponent<CharacterFootsteps>().enabled = false;
        GetComponent<CharacterController>().enabled = false;
        GetComponent<CharacterController>().SimpleMove(Vector3.zero);
    }

    public void EnableClientPlayer()
    {
        foreach (GameObject g in p_Parts)
        {
            g.SetActive(true);
        }
        human.gameObject.SetActive(true);
        GetComponent<CharacterFootsteps>().enabled = true;
        GetComponent<CharacterController>().enabled = true;
        GetComponent<CharacterController>().SimpleMove(Vector3.zero);

    }

    //Local
    public void DisablePlayerControls()
    {
       
        foreach(PlayMakerFSM f in p_Controls)
        {
            f.enabled = false;
        }
       

    }
    //Local
    public void EnablePlayerControls()
    {
        
        foreach (PlayMakerFSM f in p_Controls)
        {
            f.enabled = true;
        }
    }
    public virtual void SwitchToGhost(int type)//0-slender
    {
        if (!isGhost&&isKiller)
        {
            isGhost = true;
            GetComponent<EquipmentManager>().ForceUnEqip();
            GetComponent<EquipmentManager>().canUse = false;
            if (type == 0)
            {
                y_diff = -1f; //Calculate y difference for start pos (player.y - ghost.y)
                CMDSpawnGhost(0, this.gameObject, y_diff);
            }
            GetComponent<InteractionDetection>().isGhostMode = true;
            DisablePlayerControls();
            CMDRequestNetDisablePlayer();
            GetComponent<CapsuleCollider>().enabled = false;
            GetComponent<InteractionDetection>().cam = ghostCam;
            GetComponent<CameraManager>().GhostSwitchGlitchCamera();
            ghostCam.gameObject.SetActive(true);
            humanCam.gameObject.SetActive(false);
        }
        
    }

    public void SyncPlayerPositionFromGhost()
    {
        
        lastGhostPos = currentGhost.GetComponent<GhostHelper>().lastGhostPos;
        restoreGhostPos = true;
    }

    public void SyncMouseLookFromGhost()
    {
        GetComponent<PlayerMouseLook>().rotationX = currentGhost.GetComponent<GhostHelper>().ghostMouseX;
        GetComponent<PlayerMouseLook>().rotationY = currentGhost.GetComponent<GhostHelper>().ghostMouseY;
    }
    public virtual void SwitchToHuman() //Switch to humans
    {
        if (isGhost)
        {
            
            GetComponent<InteractionDetection>().isGhostMode = false;
            EnablePlayerControls();
            SyncPlayerPositionFromGhost();
            CMDRequestNetEnablePlayer(currentGhost.GetComponent<NetworkIdentity>().connectionToClient);
            GetComponent<CapsuleCollider>().enabled = true;
            GetComponent<InteractionDetection>().cam = humanCam;
            //Unparent bring ghostcam back before destroy ghost
            ghostCam.transform.SetParent(this.transform, true);

            SyncMouseLookFromGhost();
            GetComponent<CameraManager>().GhostSwitchGlitchCamera();
            humanCam.gameObject.SetActive(true);
            ghostCam.gameObject.SetActive(false);
            ServerDestroy(currentGhost);

            ResetGhostCamStatsToOriginal();
            GetComponent<CharacterController>().SimpleMove(Vector3.zero);
            GetComponent<EquipmentManager>().canUse = true;
        }
    }

    #endregion
    #region Camera Effects

    #endregion
    #region Ragdoll Pin and Pick

    //Call this from clienRPC in specific ghost script
    
    public void PinRagdollHead(Transform pin, Transform target)
    {
        if (!isPinned)
        {
            isPinned = true;
            //Transform p = NetworkClient.spawned[pin].GetComponent<GhostHelper>().pinHinge;
            //Transform t = NetworkClient.spawned[target].GetComponent<Player>().head;
            FixedJoint f = target.gameObject.AddComponent<FixedJoint>();
            Rigidbody rb = pin.gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            f.connectedBody = rb;
            StartCoroutine(TimedUnpin(2f, pin, target));
        }
        
    }

    IEnumerator TimedUnpin(float duration, Transform pin, Transform target)
    {
        yield return new WaitForSeconds(duration);
        UnpinRagdollHead(pin, target);
    }
    public void UnpinRagdollHead(Transform pin, Transform target)
    {
        if (isPinned)
        {
            isPinned = false;
            if (target.GetComponent<FixedJoint>() != null)
            {
                Destroy(target.GetComponent<FixedJoint>());
            }

            if (pin.GetComponent<Rigidbody>() != null)
            {
                Destroy(pin.GetComponent<Rigidbody>());
            }
        }

       
    }
 
   
    #endregion
    public void Update()
    {
        if (!isLocalPlayerBool||!isKiller)
        {
            return;
        }
        if (interactionDetection==null&&GetComponent<InteractionDetection>() != null)
        {
            interactionDetection = GetComponent<InteractionDetection>();
        }
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (!isGhost)
            {
                if(interactionDetection != null && interactionDetection.selected!=null&& interactionDetection.selected.tag == "HostMachine"&&interactionDetection.selected.GetComponent<HostMachine>().isAlive)//When interacting with host machine to switch to ghost
                {
                    SwitchToGhost(0); //Test switch to slender
                }
            }
            else
            {
                if (currentGhost.GetComponent<GhostHelper>().canSwitchState)
                {
                    SwitchToHuman();
                }
            }
            
        }
    }
}
