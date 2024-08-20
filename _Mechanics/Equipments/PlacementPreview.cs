

using System.Collections.Generic;
using UnityEngine;

public class PlacementPreview : MonoBehaviour
{
    // The public variables here act as intermediate storage as EquipmentManager holds reference only to current placement preview for placeable equipment
    public Equipment.PlacementAxis mPlacementAxis;
    public bool mCanRotatePlacementPreview;
    private Outline mOutline;
    private Collider mCollider;
    private bool mCanPlace = false;
    public Collider GetCollider() => mCollider;

    public bool GetCanPlace() { return mCanPlace; }
    private void FixedUpdate()
    {
        UpdateCanPlace();
    }

    private void UpdateCanPlace()
    {
        foreach (Equipment e in GameManager.GetEquipmentRuntimeCacheClient())
        {
            if (e.GetColliderNonRaceConditionSafe() && e.GetColliderNonRaceConditionSafe().bounds.Intersects(mCollider.bounds))
            {
                SetCanPlace(false);
                return;
            }
        }

        SetCanPlace(true);
    }

    private void Start()
    {
        if (!mOutline.enabled)
        {
            mOutline.enabled = true;
        }

        mOutline.OutlineColor = Color.green;
    }

    private void Awake()
    {
        mOutline = GetComponent<Outline>();
        if (mOutline == null)
        {
            mOutline = gameObject.AddComponent<Outline>();
        }
        mCollider = GetComponent<Collider>();
    }

    public void SetCanPlace(bool canPlace)
    {
        if (canPlace != mCanPlace)
        {
            mCanPlace = canPlace;
            UpdateOutline(mCanPlace);
        }
    }

    private void UpdateOutline(bool canPlace)
    {
        mOutline.OutlineColor = canPlace ? Color.green : Color.red;
    }
}
