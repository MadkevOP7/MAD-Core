
using System.Collections;
using UnityEngine;
using Mirror;
[RequireComponent(typeof(AudioSource))]

public class Door : NetworkBehaviour
{
    [Header("Start Settings")]
    public bool isOpened = false;

    public bool isAnimated;
    public Animator animator;
    // Inspector Settings
    public float InitialAngle = 0.0F;
    public float RotationAngle = 90.0F;
    public enum SideOfRotation { Left, Right }
    public SideOfRotation RotationSide;
    [StayPositive]
    public float Speed = 3F;
    public int TimesMoveable = 0;

    // Hinge
    private GameObject hinge;
    public enum PositionOfHinge { Left, Right }
    public PositionOfHinge HingePosition;

    // 3rd party compatibility
    public enum ScaleOfDoor { Unity3DUnits, Other }
    public ScaleOfDoor DoorScale;
    public enum PositionOfPivot { Centered, CorrectlyPositioned }
    public PositionOfPivot PivotPosition;

    // Define an initial and final rotation
    Quaternion FinalRot, InitialRot;

    int State;

    int TimesRotated = 0;
    [SyncVar(hook =nameof(HookDoor))]
    public bool RotationPending = false; // Needs to be public because Detection.cs has to access it

    // Debug Settings
    public bool VisualizeHinge = false;
    public Color HingeColor = Color.yellow;

    // An offset to take into account the original rotation of a 3rd party door
    Quaternion RotationOffset;

    //Audio
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip openAudio;
    public AudioClip closeAudio;

    [Command(requiresAuthority = false)]
    public void Interact()
    {
        if (isAnimated)
        {
            
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("idle") && !animator.IsInTransition(0))
            {     
                //can play
                animator.Play(isOpened == true ? "close" : "open");
                RPCPlayAnimation(isOpened == true ? "close" : "open");
                isOpened = !isOpened;
            }
            else if(animator.GetCurrentAnimatorStateInfo(0).IsName("open")&& animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1)
            {
                //Debug Test for isOpen boolean is same as animation state
                //Door should be closed here
                if (!isOpened)
                {
                    Debug.LogWarning("Door boolean has incorrect state synced with Animator animation, should be open but is closed");
                }
                //can play
                animator.Play(isOpened == true ? "close" : "open");
                RPCPlayAnimation(isOpened == true ? "close" : "open");
                isOpened = !isOpened;
            }
            else if (animator.GetCurrentAnimatorStateInfo(0).IsName("close") && animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1)
            {
                //Debug Test for isOpen boolean is same as animation state
                //Door should be closed here
                if (!isOpened)
                {
                    Debug.LogWarning("Door boolean has incorrect state synced with Animator animation, should be closed but is open");
                }
                //can play
                animator.Play(isOpened == true ? "close" : "open");
                RPCPlayAnimation(isOpened == true ? "close" : "open");
                isOpened = !isOpened;
            }
        }
        else if (!RotationPending)
        {
            RotationPending = true;
        }
    }
    [Command(requiresAuthority = false)]
    public void CMDFinished()
    {
        RotationPending = false;
    }
    [ClientRpc(includeOwner = false)]
    public void RPCPlayAnimation(string name)
    { 
        animator.Play(name);
        if (name == "open"&&openAudio!=null)
        {
            audioSource.clip = openAudio;
            audioSource.Play();
        }
        else if (name == "close"&&closeAudio!=null)
        {
            audioSource.clip = closeAudio;
            audioSource.Play();
        }
    }
    public void HookDoor(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            StartCoroutine(Move());
        }
    }
    private void Update()
    {
      
    }
    void Start()
    {
        audioSource = this.GetComponent<AudioSource>();
        if (isAnimated)
        {
            transform.parent = null;
            return;
        }
        RotationOffset = transform.rotation;

        if (DoorScale != ScaleOfDoor.Unity3DUnits || PivotPosition != PositionOfPivot.Centered) return;

        // Create a hinge
        hinge = new GameObject("hinge");

        // Calculate sine/cosine of initial angle (needed for hinge positioning)
        float CosDeg = Mathf.Cos((transform.eulerAngles.y * Mathf.PI) / 180);
        float SinDeg = Mathf.Sin((transform.eulerAngles.y * Mathf.PI) / 180);

        // Read transform (position/rotation/scale) of the door
        float PosDoorX = transform.position.x;
        float PosDoorY = transform.position.y;
        float PosDoorZ = transform.position.z;

        float RotDoorX = transform.localEulerAngles.x;
        float RotDoorZ = transform.localEulerAngles.z;

        float ScaleDoorX = transform.localScale.x;
        float ScaleDoorZ = transform.localScale.z;

        // Create a placeholder/temporary object of the hinge's position/rotation
        Vector3 HingePosCopy = hinge.transform.position;
        Vector3 HingeRotCopy = hinge.transform.localEulerAngles;

        if (HingePosition == PositionOfHinge.Left)
        {
            if (transform.localScale.x > transform.localScale.z)
            {
                HingePosCopy.x = (PosDoorX - (ScaleDoorX / 2 * CosDeg));
                HingePosCopy.z = (PosDoorZ + (ScaleDoorX / 2 * SinDeg));
                HingePosCopy.y = PosDoorY;

                HingeRotCopy.x = RotDoorX;
                HingeRotCopy.y = -InitialAngle;
                HingeRotCopy.z = RotDoorZ;
            }

            else
            {
                HingePosCopy.x = (PosDoorX + (ScaleDoorZ / 2 * SinDeg));
                HingePosCopy.z = (PosDoorZ + (ScaleDoorZ / 2 * CosDeg));
                HingePosCopy.y = PosDoorY;

                HingeRotCopy.x = RotDoorX;
                HingeRotCopy.y = -InitialAngle;
                HingeRotCopy.z = RotDoorZ;
            }
        }

        if (HingePosition == PositionOfHinge.Right)
        {
            if (transform.localScale.x > transform.localScale.z)
            {
                HingePosCopy.x = (PosDoorX + (ScaleDoorX / 2 * CosDeg));
                HingePosCopy.z = (PosDoorZ - (ScaleDoorX / 2 * SinDeg));
                HingePosCopy.y = PosDoorY;

                HingeRotCopy.x = RotDoorX;
                HingeRotCopy.y = -InitialAngle;
                HingeRotCopy.z = RotDoorZ;
            }

            else
            {
                HingePosCopy.x = (PosDoorX - (ScaleDoorZ / 2 * SinDeg));
                HingePosCopy.z = (PosDoorZ - (ScaleDoorZ / 2 * CosDeg));
                HingePosCopy.y = PosDoorY;

                HingeRotCopy.x = RotDoorX;
                HingeRotCopy.y = -InitialAngle;
                HingeRotCopy.z = RotDoorZ;
            }
        }

        // Hinge Positioning
        hinge.transform.position = HingePosCopy;
        transform.parent = hinge.transform;
        hinge.transform.localEulerAngles = HingeRotCopy;

        // Debugging
        if (VisualizeHinge == true)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = HingePosCopy;
            cube.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            cube.GetComponent<Renderer>().material.color = HingeColor;
        }
    }

    // Move Function
    public IEnumerator Move()
    {
        if (true)
        {
            RotationPending = true;
            AnimationCurve rotationcurve = AnimationCurve.EaseInOut(0, 0, 1f, 1f);
            float TimeProgression = 0f;

            Transform t = transform;

            if (RotationSide == SideOfRotation.Left)
            {
                InitialRot = Quaternion.Euler(0, -InitialAngle, 0);
                FinalRot = Quaternion.Euler(0, -InitialAngle - RotationAngle, 0);
            }

            if (RotationSide == SideOfRotation.Right)
            {
                InitialRot = Quaternion.Euler(0, -InitialAngle, 0);
                FinalRot = Quaternion.Euler(0, -InitialAngle + RotationAngle, 0);
            }

            if (DoorScale == ScaleOfDoor.Unity3DUnits && PivotPosition == PositionOfPivot.Centered)
            {
                t = hinge.transform;
                RotationOffset = Quaternion.identity;
            }

            if ((TimesRotated < TimesMoveable || TimesMoveable == 0))
            {
                if (!isOpened)
                {
                    //Play open audio
                    if (true)
                    {
                        if (openAudio != null)
                        {
                            audioSource.clip = openAudio;
                            audioSource.Play();
                        }
                    }
                }
                else if (isOpened)
                {
                    //Play close audio
                    if (true)
                    {
                        if (closeAudio != null)
                        {
                            audioSource.clip = closeAudio;
                            audioSource.Play();
                        }
                    }
                }
                // Change state from 1 to 0 and back (= alternate between FinalRot and InitialRot)
                if (t.rotation == (State == 0 ? FinalRot * RotationOffset : InitialRot * RotationOffset)) State ^= 1;

                // Set 'FinalRotation' to 'FinalRot' when moving and to 'InitialRot' when moving back
                Quaternion FinalRotation = ((State == 0) ? FinalRot * RotationOffset : InitialRot * RotationOffset);

                // Make the door/window rotate until it is fully opened/closed
                while (TimeProgression <= (1 / Speed))
                {
                    TimeProgression += Time.deltaTime;
                    float RotationProgression = Mathf.Clamp01(TimeProgression / (1 / Speed));
                    float RotationCurveValue = rotationcurve.Evaluate(RotationProgression);

                    t.rotation = Quaternion.Lerp(t.rotation, FinalRotation, RotationCurveValue);

                    yield return null;
                }

                if (TimesMoveable == 0) TimesRotated = 0;
                else TimesRotated++;

                isOpened = !isOpened;
            }

            CMDFinished();
        }
        
    }
}
