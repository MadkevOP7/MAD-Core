
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Mirror;
public class InteractionDetection : NetworkBehaviour
{
    public bool isLocalPlayerBool;
    public Camera cam;
    public bool isGhostMode;
    // Raycast Settings
    public float Reach = 4.0F;
    //[HideInInspector]
    public bool InReach;
    
   
    public GameObject selected;
    public bl_UCrosshairInfo crosshair;
    public GameObject c_normal;
    public GameObject c_interact;
    public float fadeDelay = 0.01f;

    public HostManagerUI hmUI;
    public bool crosshair_enabled;

    [Header("FSM Controls")]
    public PlayMakerFSM[] input_disable_fsm;

    //Cache
    private Outline h;
    EquipmentManager equipmentManager;
    Player player;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        isLocalPlayerBool = true;
        EnablePlayerUI();
        equipmentManager = GetComponent<EquipmentManager>();
        player = GetComponent<Player>();
    }

    [Command]
    void CallDoor(Door d)
    {
        d.Interact();
    }
    private void OnDisable()
    {
        //Disable UI crosshair on disable
        crosshair.gameObject.SetActive(false);
    }
    void Update()
    {
        
        if (!isLocalPlayerBool)
        {
            this.enabled = false;
            return;
        }
       
        if (InReach)
        {
            if (crosshair_enabled)
            {
                c_normal.SetActive(false);
                c_interact.SetActive(true);
            }

        }
        else if (!InReach&&crosshair_enabled)
        {
            c_interact.SetActive(false);
            c_normal.SetActive(true);
            

        }
        // Set origin of ray to 'center of screen' and direction of ray to 'cameraview'
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5F, 0.5F, 0F));

        RaycastHit hit; // Variable reading information about the collider hit

        // Cast ray from center of the screen towards where the player is looking
        if (Physics.Raycast(ray, out hit, Reach))
        {
         
            if (hit.collider.tag == "Interactable")
            {
                InReach = true;
                crosshair.SetDefaultColors();
                if (selected == null || selected != hit.collider.gameObject)
                {
                
                    selected = hit.collider.gameObject;
                }
                selected?.GetComponent<Interactable>()?.Interact();

                if (Input.GetKeyDown(KeyCode.E))
                {
                    //Door
                    if (selected.GetComponent<Door>() != null)
                    {

                        //CallDoor(selected.GetComponent<Door>());
                        selected.GetComponent<Door>().Interact();
                        return;
                    }
                    else if (selected.GetComponentInParent<Door>() != null)
                    {
                        //CallDoor(selected.GetComponent<Door>());
                        selected.GetComponentInParent<Door>().Interact();
                        return;
                    }
                    //pickup
                    BuildItem b = selected.GetComponent<BuildItem>();
                    if (b != null && b.isPickupMode)
                    {
                        b.GetComponent<BuildItem>().PickUp();
                    }

                }
      
            }
            else if (hit.collider.tag == "HostMachine"&&!isGhostMode)
            {
                InReach = true;
                crosshair.SetDefaultColors();

                //Shift to EquipmentManager
                if (selected == null || selected != hit.collider.gameObject)
                {

                    selected = hit.collider.gameObject;
                }

                
                //Enable phone showhand
                /*this.GetComponent<Phone>().interactable = true;
                this.GetComponent<Phone>().host_machine = hit.collider.transform.GetComponent<HostMachine>();
                this.GetComponent<Phone>().RefreshSelectedHM();
                */

                //Above commented for shift to EquipmentManager

                //Handle Outline (Different for host/player by destory or check) Only host can see outline
                //Disable for outline glitch, outline only visble when object is non-visble to killer
                if (hit.collider.gameObject.GetComponent<Outline>() != null&&hit.collider.gameObject.GetComponent<Outline>().enabled)
                {
                    h = hit.collider.gameObject.GetComponent<Outline>();
                    h.enabled = false;
                }
                if (player.isKiller)
                {
                    hmUI.DisplayProgressBar(hit.collider.gameObject.GetComponent<HostMachine>().machine_Health);
                }
                else if(player.isKiller == false)
                {
                    hmUI.DisplayProgressBar(100-(hit.collider.gameObject.GetComponent<HostMachine>().machine_Health));

                }
            }
            else if (hit.collider.tag == "Equipment")
            {
                InReach = true;

                if (!equipmentManager.canUse)
                {
                    crosshair.SetColor(Color.red);
                    return;
                }

                crosshair.SetDefaultColors();
                if (selected == null || selected != hit.collider.gameObject)
                {

                    selected = hit.collider.gameObject;
                }
                //Equipment
                if (Input.GetKeyDown(KeyCode.E))
                {
                    equipmentManager.PickUpEquipment(hit.collider.gameObject.GetComponent<Equipment>());
                }

            }
            else if (hit.collider.tag == "PlacedObject")
            {
                InReach = true;
                crosshair.SetDefaultColors();          
            }
            else
            {
                InReach = false;
                if (selected != null)
                {
                    selected?.GetComponent<Interactable>()?.UnSelect();
                }
                selected = null;
                //Handle Outline (Different for host/player by destory or check) Only host can see outline
                if (h != null)
                {
                    h.enabled = true;
                }
                hmUI.HideProgressBar();

            }
        }

        else
        {
            InReach = false;
            if (selected != null)
            {
                selected?.GetComponent<Interactable>()?.UnSelect();
            }
            selected = null;
            //Handle Outline (Different for host/player by destory or check) Only host can see outline
            if (h != null)
            {
                h.enabled = true;
            }
            hmUI.HideProgressBar();

        }
    }
  
  
    //Player Control States
    public void DisablePlayerControl() // May not work, check before use
    {
        foreach (var component in input_disable_fsm)
        {
            component.enabled = false;
        }
    }

    public void EnablePlayerControl() // May not work, check before use
    {
        foreach (var component in input_disable_fsm)
        {
            component.enabled = true;
        }
    }

    public void DisablePlayerUI()
    {
        crosshair_enabled = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.Confined;
        c_interact.SetActive(false);
        c_normal.SetActive(false);
    }

    public void EnablePlayerUI()
    {
        crosshair_enabled = true;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        c_interact.SetActive(true);
        c_normal.SetActive(true);
    }
}
