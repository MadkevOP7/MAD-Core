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
    [Header("Runtime")]
    public GameObject preview;
    public PlacementPreview pr;
    float y_snap;
    float x_snap;
    float z_snap;
    Vector3 yOffset;
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

                    if ((hit.transform != this.transform && hit.transform.parent != this.transform.parent || hit.transform.parent == null && hit.transform != this.transform) && (preview == null || hit.transform != preview.transform))
                    {
                        //Spawn preview
                        if (preview == null || pr == null)
                        {
                            BuildItem build;
                            if (!GlobalBuild.Instance.dictionary.TryGetValue(inventory.selected.m_name, out build) && inventory.selected != null)
                            {
                                Debug.LogError("Building Manager: Item with name " + inventory.selected.m_name + " is not found in dictionary!");
                                return;
                            }
                            preview = Instantiate(build.gameObject, Vector3.zero, Quaternion.identity);
                            yOffset = new Vector3(0, build.yOffset, 0);
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

                            pr.detectionMode = PlacementPreview.DetectionType.None;
                            pr.selectedItem = inventory.selected;
                        }

                        preview.transform.position = hit.point + yOffset;

                        //Calculate per object face snapping deviation
                        if (current_selection != null)
                        {

                            if (GetHitFaceAxis(hit) == current_selection.front_allignment_axis)
                            {
                                preview.transform.position = (hit.normal * 0.25f) + hit.transform.position;
                            }
                            else
                            {
                                float a = Mathf.Abs(preview.transform.rotation.y - hit.transform.rotation.y);
                                if (GetHitFaceAxis(hit) == BuildItem.Axis.y || (a > -0.1 && a < 0.1))
                                {
                                    preview.transform.position = (hit.transform.position + hit.normal);
                                }
                                else
                                {
                                    //Sync y
                                    preview.transform.position = new Vector3(preview.transform.position.x, hit.transform.position.y, preview.transform.position.z) + hit.normal * 0.125f;
                                }
                            }

                        }
                        //Snap input key z
                        if (Input.GetKey(KeyCode.Z))
                        {
                            if(pr.rotationState == PlacementPreview.RotationState.rotated)
                            {
                                preview.transform.localPosition = new Vector3(preview.transform.localPosition.x, y_snap, z_snap);
                            }
                            else
                            {
                                preview.transform.localPosition = new Vector3(x_snap, y_snap, preview.transform.localPosition.z);

                            }
                        }
                        else
                        {
                            y_snap = preview.transform.localPosition.y;
                            x_snap = preview.transform.localPosition.x;
                            z_snap = preview.transform.localPosition.z;
                        }
                 


                        if (Input.GetKeyDown(KeyCode.R))
                        {
                            if (pr.rotationState == PlacementPreview.RotationState.defaultState)
                            {
                                preview.transform.Rotate(0, 90, 0);
                                pr.rotationState = PlacementPreview.RotationState.rotated;
                            }
                            else
                            {
                                preview.transform.Rotate(0, -90, 0);
                                pr.rotationState = PlacementPreview.RotationState.defaultState;
                            }
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

                    if (current_selection == null || current_selection.isPickupMode) return;
                    //[TODO] Implement lobby mode (allow_world_editing) to allow this only if lobby owner allows direct world edit
                    if (current_selection != null && !current_selection.isPickupMode)
                    {
                        current_selection.ActivateOutline(Color.red);
                    }

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
