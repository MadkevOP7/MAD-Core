//Handle Character Hand interaction IK
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FSMCaller : MonoBehaviour
{
    #region Const Define
    public const int NO_HAND_STATE = -999;
    public const int DROP_HAND_STATE = -2;
    #endregion
    public Player mPlayer;
    public Transform ik_target;
    public float aim;
    public PlayMakerFSM phoneFSM; //LocalRot
    [Header("Hand Animation")]
    public Animator anim;
    public bool is_aiming;

    public int GetFSMHandStateCache() { return mFSMHandStateCache; }
    // Runtime cache
    // After replacing character, we need to call FSMShowHand() again to re-play the animation
    // For some reason it doesn't update skeleton correctly despite Rebind() call and thus causes offset issues
    private int mFSMHandStateCache = NO_HAND_STATE;
    public void RefreshShowHandOnCharacterChange()
    {
        SetHandState(mFSMHandStateCache);
    }
    public void SetHandState(int state)
    {
        mFSMHandStateCache = state;
        if (state == 0) //Phone fsm
        {
            phoneFSM.enabled = true;
            anim.SetLayerWeight(1, 1); //Enable hand extra animation layer
            anim.Play(LIMENDefine.ANIMATION_HAND_DEFAULT, 1, 0f); //Play default state to remove other anim
            is_aiming = false;
        }
        else if (state == 1) //Motion Detector fsm
        {
            phoneFSM.enabled = true; //Ups hand
            anim.SetLayerWeight(1, 1); //Enable hand extra animation layer
            anim.Play("hand_motion_detector", 1, 0f); //Play state
            is_aiming = false;
        }
        else if (state == 2) //EMF Reader fsm, aims at target
        {
            phoneFSM.enabled = true;
            anim.SetLayerWeight(1, 1); //Enable hand extra animation layer
            anim.Play("hand_emf_reader", 1, 0f); //Play state
            is_aiming = true;
        }
        else if (state == 3) //Phone but aims at target (camera/scanner)
        {
            phoneFSM.enabled = true;
            anim.SetLayerWeight(1, 0); //Disable hand extra animation layer
            is_aiming = true;
        }
        else if (state == 4) //Charms
        {
            phoneFSM.enabled = true; //Ups hand
            anim.SetLayerWeight(1, 1); //Enable hand extra animation layer
            anim.Play("hand_charm", 1, 0f); //Play state
            is_aiming = false;
        }
        else if (state == 5) //Geiger Counter
        {
            phoneFSM.enabled = true; //Ups hand
            anim.SetLayerWeight(1, 1); //Enable hand extra animation layer
            anim.Play("hand_geiger", 1, 0f); //Play state
            is_aiming = false;
        }
        else if (state == 6) //Listening Gun
        {
            phoneFSM.enabled = true; //Ups hand
            anim.SetLayerWeight(1, 1); //Enable hand extra animation layer
            anim.Play("hand_listening_gun", 1, 0f); //Play state
            is_aiming = false;
        }

        //Add else if here for different fsm states


        //Drop Arm without removing layer hand
        else if (state == DROP_HAND_STATE)
        {
            phoneFSM.enabled = false;
            is_aiming = false;
        }
        else
        {
            phoneFSM.enabled = false;
            anim.SetLayerWeight(1, 0); //Disable hand extra animation layer
            is_aiming = false; //All aim objects cannot allow hand drop
        }
    }
    public void Update()
    {
        if (is_aiming)
        {
            float new_y = IntegrateAim(transform.InverseTransformPoint(new Vector3(ik_target.position.x, aim, ik_target.position.z)).y);
            ik_target.localPosition = Vector3.Lerp(ik_target.localPosition, new Vector3(ik_target.localPosition.x, new_y, ik_target.localPosition.z), 3 * Time.deltaTime);
        }
        else
        {
            if (mPlayer.GetCharacter())
            {
                ik_target.localPosition = mPlayer.GetCharacter().GetTabletIKPosition();
                ik_target.localEulerAngles = mPlayer.GetCharacter().GetTabletIKRotation();
            }
        }
    }

    public float IntegrateAim(float a)
    {
        if (a > 0.05f && a <= 0.2f)
        {
            return 1.08f;
        }
        if (a > 0.2f && a <= 1.2f)
        {
            return 1.11f;
        }
        if (a > 1.2f && a <= 2f)
        {
            return 1.21f;
        }
        if (a > 2 && a <= 2.6f)
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
