using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class ObjectPlacement : NetworkBehaviour
{

    [Header("Settings")]
    public Material preview_material;
    public int m_spawnLimit = 1;
    [SyncVar]
    public int spawnLimit; //Use for tracking how many times this can be placed, such as only x amount of salt can be spawned . -1 = infinite
    public Camera cam;
    public float Reach = 4f;
    [Header("Objects")]
    public GameObject obj;

    [Header("Debug")]
    public bool is_enabled;
    private Vector3 point;

    [Header("Runtime")]
    public GameObject preview;
    public List<GameObject> spawned = new List<GameObject>();
    private Equipment eq;
    private void Awake()
    {
        NetworkClient.RegisterPrefab(obj);
    }

    private void Start()
    {
        SetSpawnLimit(m_spawnLimit);
        eq = GetComponent<Equipment>();
    }
    [Command(requiresAuthority = false)]
    public void SetSpawnLimit(int n)
    {
        spawnLimit = n;
    }
    // Update is called once per frame
    void Update()
    {
        if (cam == null && eq.player_cam != null)
        {
            cam = eq.player_cam;
            is_enabled = true; //TEMP
        }
        if (!is_enabled||cam==null)
        {
            return;
        }
        
        // Set origin of ray to 'center of screen' and direction of ray to 'cameraview'
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5F, 0.5F, 0F));
        RaycastHit hit; // Variable reading information about the collider hit
        if (Physics.Raycast(ray, out hit, Reach))
        {
            point = hit.point;
        }

        if (preview == null)
        {
            preview = Instantiate(obj);
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = preview_material;
            }
          
        }
        else
        {
            preview.transform.position = point;
        }
        if (Input.GetKeyDown(KeyCode.F))
        {

        }
    }
}
