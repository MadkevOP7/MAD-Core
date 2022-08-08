using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//Use as a bridge between each ghost and Player.cs
//Tracks GhostController, and boolean can_attack
public class GhostHelper : MonoBehaviour
{
    [Header("References")]
    
    public bool isOwner; //For detecting who can control
    public GhostController ghostControl;
    public Vector3 lastHumanPos;
    public Vector3 lastGhostPos;
    public Quaternion lastHumanRot;
    public Quaternion lastHumanCamRot; //local
    public GhostController player;
    public float ghostMouseX;
    public float ghostMouseY;
    public Transform hand;
    public Transform head;
    public Transform pinHinge; //Used for ragdoll lockhead and pin
    public bool canSwitchState = true; //Used for determine if can switch back to human, can't during kill
}
