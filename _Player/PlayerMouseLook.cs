using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class PlayerMouseLook : NetworkBehaviour
{
    [Header("View")]
    public Camera cam;
    public float minX = -360f;
    public float maxX = 360f;
    public float minY = -60f;
    public float maxY = 80f;
    public float sensitivity = 2;
    public float rotationX;
    public float rotationY;
    public bool isLocalPlayerBool;
    #region Mouselook
    //Initilize rotationX and rotationY
    void InitializeRotations()
    {
        rotationX = transform.localEulerAngles.y;
        rotationY = -cam.transform.localEulerAngles.x;
    }
    // Clamp function that respects IsNone and 360 wrapping
    public float ClampAngle(float angle, float min, float max)
    {
        if (angle < 0f) angle = 360 + angle;

        var from = min;
        var to = max;

        if (angle > 180f) return Mathf.Max(angle, 360 + from);
        return Mathf.Min(angle, to);
    }

    public float GetXRotation()
    {
        rotationX += Input.GetAxis("Mouse X") * sensitivity;
        rotationX = ClampAngle(rotationX, minX, maxX) % 360;
        return rotationX;
    }
    public float GetYRotation(float invert = 1)
    {
        rotationY += Input.GetAxis("Mouse Y") * sensitivity * invert;
        rotationY = ClampAngle(rotationY, minY, maxY) % 360;
        return rotationY;
    }
    public void MouseLook()
    {
        transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, GetXRotation(), 0);
        cam.transform.localEulerAngles = new Vector3(GetYRotation(-1), cam.transform.localEulerAngles.y, 0);
    }

    #endregion
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        InitializeRotations();
        isLocalPlayerBool = true;
    }
    private void Update()
    {
        if (!isLocalPlayerBool)
        {
            return;
        }
        MouseLook();
    }
    private void Start()
    {
        
    }
}
