using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
public class BuildingManager : MonoBehaviour
{

    [Header("Settings")]
    public Camera cam;

    //Filter out equipment layer, player, and other non placeable (Only Default Layer and BuildItem layer should work)
    public LayerMask layer;

    //Selection
    [Header("Runtime")]
    //State control enum==========
    public action Action;
    public enum action
    {
        place,
        destroy
    }
    //============================
    public GlobalInventory inventory;
    //public GlobalItem current; //Current item to be placed (holding) Use inventory.selected
    public BuildItem current_selection; //Raycast hit item
    [Header("Placeable Debug")]
    public GameObject preview;
    public PlacementPreview pr;

    private void Start()
    {
        inventory = GlobalInventory.Instance;
        cam = GetComponent<Equipment>().player_cam;
    }
    private void Update()
    {
        Process();
    }

    private void OnDisable()
    {
        CleanupPlacementPreview();
        CleanupSelection();
    }

    #region Clean up

    public void CleanupSelection()
    {
        if (current_selection != null && !current_selection.isPickupMode)
        {
            current_selection.DeActivateOutline();
        }
        current_selection = null;
    }

    public void CleanupPlacementPreview()
    {
        //Clean up placement preview
        if (preview != null || pr != null)
        {
            Destroy(preview.gameObject);
            preview = null;
            pr = null;
        }
    }
    #endregion
    public void Process()
    {
        if (inventory.selected == null && Action == action.place)
        {
            return;
        }
        // Set origin of ray to 'center of screen' and direction of ray to 'cameraview'
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5F, 0.5F, 0F));
        RaycastHit hit; // Variable reading information about the collider hit
        if (Physics.Raycast(ray, out hit, 4f, layer))
        {
            if (hit.transform.CompareTag("PlacedObject"))
            {
                BuildItem cur = hit.transform.GetComponent<BuildItem>();
                if (cur != current_selection)
                {
                    CleanupSelection();
                    current_selection = cur;
                }
            }
            else
            {
                CleanupSelection();
            }
            switch (Action)
            {
                #region Placement Update
                case action.place:
                    ////Switch mode
                    //if (Input.GetKeyDown(KeyCode.Q))
                    //{
                    //    Action = action.destroy;
                    //    CleanupPlacementPreview();
                    //    return;
                    //}
                    //CleanupSelection();
                    if ((hit.transform != this.transform && hit.transform.parent != this.transform.parent || hit.transform.parent == null && hit.transform != this.transform) && (preview == null || hit.transform != preview.transform))
                    {
                        //Spawn preview
                        if (preview == null || pr == null)
                        {
                            BuildItem build;
                            if (!GlobalBuild.Instance.dictionary.TryGetValue(inventory.selected.m_name, out build) && inventory.selected!=null)
                            {
                                Debug.LogError("Building Manager: Item with name " + inventory.selected.m_name + " is not found in dictionary!");
                                return;
                            }
                            preview = Instantiate(build.gameObject, Vector3.zero, Quaternion.identity);
                            Destroy(preview.GetComponent<BuildItem>());

                            preview.layer = 2;
                            pr = preview.AddComponent<PlacementPreview>();
                            preview.transform.rotation = Quaternion.identity;
                            //Cleanup components for preview obj
                            Collider col = preview?.GetComponent<Collider>();
                            if (col != null) { col.isTrigger = true; }
                            NavMeshObstacle obs = preview.GetComponent<NavMeshObstacle>();
                            if (obs != null)
                            {
                                obs.enabled = false;
                            }
                            pr.detectionMode = PlacementPreview.DetectionType.PhysicsOverlap;
                            pr.selectedItem = inventory.selected;
                        }

                        //All place objects are confined to 90 degree rotation like Minecraft

                        var currentPos = hit.point;

                        //Calculate per object face snapping deviation
                        if (current_selection != null)
                        {

                            if (GetHitFaceAxis(hit) == current_selection.front_allignment_axis)
                            {

                                preview.transform.position = (hit.normal * 0.25f) + hit.transform.position;
                            }
                            else
                            {
                                preview.transform.position = (hit.transform.position + hit.normal);
                            }

                        }
                        else
                        {

                            //Snap position to grid
                            preview.transform.position = new Vector3(Mathf.RoundToInt(currentPos.x),
                                                        Mathf.RoundToInt(currentPos.y),
                                                        Mathf.RoundToInt(currentPos.z));
                        }


                        if (Input.GetKeyDown(KeyCode.R))
                        {
                            preview.transform.Rotate(0, 90, 0);
                        }
                        if (Input.GetKeyDown(KeyCode.F) && pr.canPlace)
                        {
                            //Placement code
                            GlobalBuild.Instance.Build(inventory.selected, preview.transform.position, preview.transform.eulerAngles);
                        }
                    }
                    break;
                #endregion
                #region Destroy Update
                case action.destroy:
                    //Switch mode
                    //if (Input.GetKeyDown(KeyCode.Q))
                    //{
                    //    CleanupSelection();
                    //    Action = action.place;
                    //    return;
                    //}
                    //CleanupPlacementPreview();
                    if (current_selection == null || current_selection.isPickupMode) return;
                    //[TODO] Implement lobby mode (allow_world_editing) to allow this only if lobby owner allows direct world edit
                    if (current_selection != null && !current_selection.isPickupMode)
                    {
                        current_selection.ActivateOutline(Color.red);
                    }

                    BuildItem b = hit.transform.GetComponent<BuildItem>();
                    if (b == null) return;

                    if (Input.GetKeyDown(KeyCode.F))
                    {
                        current_selection.Damage(100);
                    }
                    break;
                    #endregion
            }

        }
    }

    #region Math & Calculations
    public Vector3 RoundVector3(Vector3 vec)
    {
        return new Vector3(Mathf.Round(vec.x), Mathf.Round(vec.y), Mathf.Round(vec.z));
    }
    public Vector3 GetLocalHitNormal(RaycastHit hit)
    {
        return hit.transform.InverseTransformDirection(hit.normal);
    }
    public BuildItem.Axis GetHitFaceAxis(RaycastHit hit)
    {
        Vector3 vec = hit.transform.InverseTransformDirection(hit.normal);
        if (Mathf.RoundToInt(vec.x) != 0)
        {

            return BuildItem.Axis.x;
        }
        if (Mathf.RoundToInt(vec.y) != 0)
        {
            return BuildItem.Axis.y;
        }
        if (Mathf.RoundToInt(vec.z) != 0)
        {
            return BuildItem.Axis.z;
        }

        return BuildItem.Axis.none;
    }
    #endregion
}
