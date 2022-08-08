//Handle Character Hand interaction IK
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FSMCaller : MonoBehaviour
{
    public Transform ik_target;
    public float aim;
    public Vector3 start_pos;
    public Quaternion start_rot; //LocalPos
    public PlayMakerFSM phoneFSM; //LocalRot
    [Header("Hand Animation")]
    public Animator anim;
    public bool is_aiming;
    public void ShowHand(int visible)
    {
        if (visible == 0) //Phone fsm
        {
            phoneFSM.enabled = true;
            anim.SetLayerWeight(1, 0); //Disable hand extra animation layer
            is_aiming = false;
        }
        else if (visible == 1) //Motion Detector fsm
        {
            phoneFSM.enabled = true; //Ups hand
            anim.SetLayerWeight(1, 1); //Enable hand extra animation layer
            anim.Play("hand_motion_detector", 1, 0f); //Play state
            is_aiming = false;
        }
        else if (visible == 2) //EMF Reader fsm, aims at target
        {
            phoneFSM.enabled = true;
            anim.SetLayerWeight(1, 1); //Enable hand extra animation layer
            anim.Play("hand_emf_reader", 1, 0f); //Play state
            is_aiming = true;
        }
        //Add else if here for different fsm states


        //Drop Arm without removing layer hand
        else if (visible == -2)
        {
            phoneFSM.enabled = false;
            is_aiming = false;
        }
        else
        {
            phoneFSM.enabled = false;
            anim.SetLayerWeight(1, 0); //Disable hand extra animation layer
            is_aiming = false; //All aim objects cannot allow handdrop
        }
    }

    private void Start()
    {
        //start_pos = ik_target.localPosition;
        //start_rot = ik_target.localRotation;
    }

    public void Update()
    {
        if (is_aiming)
        {
            //Vector3 f = 
            float new_y = IntegrateAim(transform.InverseTransformPoint(new Vector3(ik_target.position.x, aim, ik_target.position.z)).y);
            ik_target.localPosition = Vector3.Lerp(ik_target.localPosition, new Vector3(ik_target.localPosition.x, new_y, ik_target.localPosition.z), 3 * Time.deltaTime);
        }
        else
        {
            ik_target.localPosition = start_pos;
            ik_target.localRotation = start_rot;
        }
    }

    public float IntegrateAim(float a)
    {
       
        if (a > 0.05f && a <= 0.2f)
        {
            return 1.08f;
        }
        if(a>0.2f && a <= 1.2f)
        {
            return 1.11f;
        }
        if (a > 1.2f && a <= 2f)
        {
            return 1.21f;
        }
        if(a>2 && a <= 2.6f)
        {
            return 1.3f;
        }
        if (a > 2.6f)
        {
            return 1.4f;
        }
        return a;
    }
}
