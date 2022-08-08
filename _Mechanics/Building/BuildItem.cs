//© 2022 by MADKEV Studio, all rights reserved
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Mirror;
using UnityEngine.AI;
using Random = UnityEngine.Random;

[RequireComponent(typeof(NavMeshObstacle))]
[Serializable]
public class BuildItem : NetworkBehaviour
{
    [Header("Settings")]
    public NavMeshObstacle obstacle;
    public Sprite image;
    public AudioSource m_audio;
    public AudioClip dropped_sound;
    [HideInInspector]
    public Outline outline;
    [SerializeField]
    public string m_name; //For all woods, the name must contain Wood as Inventory uses common contained to sort into groups

    [SerializeField]
    public enum Axis
    {
        x,
        y,
        z,
        none
    }
    [SerializeField]
    public Axis front_allignment_axis;

    [SerializeField]
    public enum Type
    {
        OFBlockVertical, //one-fourth a block
        OFBlockFlat,
        block
    }
    [SerializeField]
    public Type type;

    //===============================
    //Non serailized references
    [NonSerialized]
    MeshDeformer deformer;
    //===============================
    //Runtime Data===================
    [Header("Runtime")]
    [SerializeField]
    public int health = 100; //Lives on server but not syncvar
    [SerializeField]
    public bool isPickupMode;
    [NonSerialized]
    bool isInVisualOperation;

    private void Awake()
    {
        gameObject.layer = 18;
        obstacle = GetComponent<NavMeshObstacle>();
        BoxCollider c = GetComponent<BoxCollider>();
        obstacle.center = c.center;
        obstacle.size = c.size;
    }
    private void OnEnable()
    {
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
        }
        outline.enabled = false;
        if (isPickupMode)
        {
            gameObject.tag = "Interactable";
        }
        else
        {
            ProcessObstacleCarving();
        }
    }

    #region AI Functions
    public float Distance(Vector3 a, Vector3 b)
    {
        float xDiff = a.x - b.x;
        float zDiff = a.z - b.z;
        return Mathf.Sqrt((xDiff * xDiff) + (zDiff * zDiff));
    }

    //Check by raycast, don't raycast down to check duplicate carving as reconstruction from save may not build in order
    void ProcessObstacleCarving()
    {
        if (isPickupMode) return;
        RaycastHit[] hits = Physics.RaycastAll(transform.position + new Vector3(0, 1, 0), -transform.up, 3f);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject.CompareTag("Terrain"))
            {
                obstacle.carving = true;
                return;
            }
        }
    }
    #endregion

    #region Outline
    public void ActivateOutline(Color col)
    {
        outline.OutlineColor = col;
        outline.enabled = true;
    }

    public void DeActivateOutline()
    {
        if (outline == null) return;
        outline.enabled = false;
    }
    #endregion

    #region Runtime Behaviors Public
    [Command(requiresAuthority = false)]
    public void Damage(int damage)
    {
        health -= damage;
        if (health < 0)
        {
            health = 0;
        }
        if (health == 0)
        {
            float x = Random.Range(-0.68f, 0.68f);
            float z = Random.Range(-0.68f, 0.68f);
            SetAsDestroyed(x, z);
            return;
        }
        RefreshObjectHealth(health);
    }

    //Not command, so owner of this gets the item added to their inventory
    public void PickUp()
    {
        GlobalInventory.Instance.AddItem(m_name, 1, type);
        ServerDestroy();
    }

    [Command(requiresAuthority = false)]
    void ServerDestroy()
    {
        GlobalBuild.Instance.Remove(netId);
        NetworkServer.Destroy(this.gameObject);
    }
    #endregion

    #region Core Private
    [ClientRpc]
    void RefreshObjectHealth(int health)
    {
        this.health = health;
        if (health > 0)
        {
            StartCoroutine(ProcessVisual());
        }
    }

    [ClientRpc]
    void SetAsDestroyed(float x, float z)
    {
        StopAllCoroutines();
        if (deformer != null)
        {
            Destroy(deformer);
        }
        isPickupMode = true;
        gameObject.tag = "Interactable";
        obstacle.carving = false;
        obstacle.enabled = false;

        Rigidbody rigid = gameObject.AddComponent<Rigidbody>();
        rigid = gameObject.GetComponent<Rigidbody>(); //Prevent disconnect exploit
        rigid.useGravity = true;
        rigid.isKinematic = false;
        rigid.detectCollisions = true;
        rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        transform.localScale /= 4;
        rigid.AddForce(Vector3.up * 5, ForceMode.Impulse);
        rigid.AddForce(new Vector3(x, 0, z) * 1, ForceMode.Impulse);
        //Set to null in built list to speed things up

        DeActivateOutline();
    }

    [ClientRpc]
    public void SetDestroyedFromSave()
    {
        //called by server when loading in save file data
        if (deformer != null)
        {
            Destroy(deformer);
        }
        transform.localScale /= 4;
        isPickupMode = true;
        gameObject.tag = "Interactable";
        Rigidbody rigid = gameObject.GetComponent<Rigidbody>(); //Prevent disconnect exploit
        if (rigid)
        {
            Destroy(rigid);
        }
        DeActivateOutline();
    }
    IEnumerator ProcessVisual()
    {
        if (isInVisualOperation)
        {
            if (deformer != null)
            {
                deformer.power = 10;
            }
            yield break;
        }
        isInVisualOperation = true;

        if (deformer == null)
        {
            deformer = gameObject.AddComponent<MeshDeformer>();
        }
        deformer.power = 10;
        deformer.relaxMesh = true;
        deformer.enabled = true;
        while (deformer.power > 0)
        {
            deformer.power--;
            yield return new WaitForSeconds(0.5f);
        }
        Destroy(deformer);
        deformer = null;
        isInVisualOperation = false;
    }
    #endregion

    #region Core Audio
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude > 0.68f && collision.gameObject.layer != 7)
        {
            Play3DAudio(dropped_sound, 0.268f, 6);
        }
    }

    public void Play3DAudio(AudioClip clip, float volume, float max_dist)
    {
        if (m_audio == null)
        {
            m_audio = gameObject.AddComponent<AudioSource>();
        }
        m_audio.spatialBlend = 1;
        m_audio.maxDistance = max_dist;
        m_audio.PlayOneShot(clip, volume);
    }
    #endregion
}
