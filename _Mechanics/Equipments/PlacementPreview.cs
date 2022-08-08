using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlacementPreview : MonoBehaviour
{
    public static float min_overlap_distance = 0.086f;
    public enum DetectionType
    {
        Trigger, //For equipments
        Collision,
        PhysicsOverlap //For building blocks, sometimes trigger don't work...
    }

    public DetectionType detectionMode;
    private Outline outline;
    public bool canPlace;
    public List<Collider> col = new List<Collider>();
    public List<Collider> building_col = new List<Collider>();

    //Building System
    public GlobalItem selectedItem;
    private void OnTriggerEnter(Collider other)
    {
        if (detectionMode == DetectionType.Trigger)
        {

            if (other.gameObject.tag == "Equipment")
            {
                col.Add(other);
            }

            if (other.gameObject.tag == "PlacedObject")
            {
                building_col.Add(other);
            }
        }
        //if(detectionMode == DetectionType.PhysicsOverlap)
        //{
        //    if (other.gameObject.tag == "Equipment")
        //    {
        //        col.Add(other);
        //    }
        //}
    }
    private void OnTriggerExit(Collider other)
    {
        if (detectionMode == DetectionType.Trigger)
        {
            if (other.gameObject.tag == "Equipment")
            {
                col.Remove(other);
            }

            if (other.gameObject.tag == "PlacedObject")
            {
                building_col.Remove(other);
            }
        }

        //if (detectionMode == DetectionType.PhysicsOverlap)
        //{
        //    if (other.gameObject.tag == "Equipment")
        //    {
        //        col.Remove(other);
        //    }
        //}
    }

    void OnCollisionEnter(Collision other)
    {
        if (detectionMode == DetectionType.Collision)
        {
            if (other.gameObject.tag == "Equipment")
            {
                col.Add(other.collider);
            }

            if (other.gameObject.tag == "PlacedObject")
            {
                building_col.Add(other.collider);
            }
        }

    }
    void OnCollisionExit(Collision other)
    {
        if (detectionMode == DetectionType.Collision)
        {
            if (other.gameObject.tag == "Equipment")
            {
                col.Remove(other.collider);
            }

            if (other.gameObject.tag == "PlacedObject")
            {
                building_col.Remove(other.collider);
            }
        }

    }

    private void Update()
    {
        ValidateBuilding();
    }

    private void FixedUpdate()
    {
        FixedValidateBuilding();
    }
    private void Start()
    {

        outline = GetComponent<Outline>(); //keep 2 to work, figure out later lol
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
        }
        outline = GetComponent<Outline>();
        if (!outline.enabled)
        {
            outline.enabled = true;
        }
        outline.OutlineColor = Color.green;
        Rigidbody rigid = GetComponent<Rigidbody>();
        if (rigid == null)
        {
            rigid = gameObject.AddComponent<Rigidbody>();
        }
        rigid.useGravity = false;
        rigid.detectCollisions = true;
    }
    public void FixedValidateBuilding()
    {
        if (detectionMode == DetectionType.PhysicsOverlap)
        {
            Collider[] hitColliders = Physics.OverlapSphere(gameObject.transform.position, min_overlap_distance);
            foreach (Collider c in hitColliders)
            {
                if (c.gameObject == this.gameObject) continue;
                if (c.CompareTag("PlacedObject") || c.CompareTag("Equipment"))
                {
                    if (IsOverlapDistance(c))
                    {
                        SetCanPlace(false);
                        return;
                    }
                }
            }

            //if (col.Count == 0)
            //{
            //    SetCanPlace(true);
            //    return;
            //}
            //else
            //{
            //    //Refresh null cols
            //    foreach (Collider c in col)
            //    {
            //        if (c == null)
            //        {
            //            col.Remove(c);
            //        }
            //    }
            //    if (col.Count > 0)
            //    {
            //        SetCanPlace(false);
            //    }
                
            //}
            SetCanPlace(true);
            return;
        }
    }
    public void ValidateBuilding()
    {
        if (selectedItem != null && selectedItem.count == 0)
        {
            SetCanPlace(false);
            selectedItem = null;
        }
        if (detectionMode == DetectionType.PhysicsOverlap)
        {
            return;
        }

        if (col.Count == 0 && building_col.Count == 0)
        {
            SetCanPlace(true);
            return;
        }
        else
        {
            //Refresh null cols
            foreach (Collider c in col)
            {
                if (c == null)
                {
                    col.Remove(c);
                }
            }
            if (col.Count > 0)
            {
                SetCanPlace(false);
            }

            //Refresh null cols
            foreach (Collider c in building_col)
            {
                if (c == null)
                {
                    building_col.Remove(c);
                }
                else if (!IsOverlapDistance(c))
                {
                    building_col.Remove(c);
                }
            }
            if (building_col.Count > 0)
            {
                SetCanPlace(false);
            }
        }
    }

    public bool IsOverlapDistance(Collider c)
    {
        float x = c.transform.position.x;
        float y = c.transform.position.y;
        float z = c.transform.position.z;

        if (Mathf.Abs(x - transform.position.x) < min_overlap_distance && Mathf.Abs(y - transform.position.y) < min_overlap_distance && Mathf.Abs(z - transform.position.z) < min_overlap_distance)
        {
            return true;
        }
        return false;
    }
    public void SetCanPlace(bool can_place)
    {
        if (can_place != canPlace)
        {
            //Need updating Outline
            canPlace = can_place;
            UpdateOutline(canPlace);
        }
    }

    public void UpdateOutline(bool canPlace)
    {

        if (canPlace)
        {
            outline.OutlineColor = Color.green;
        }
        else
        {
            outline.OutlineColor = Color.red;
        }
    }


}
