using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interactable : MonoBehaviour
{
    [SerializeField]
    public bool selected = false;

    public void Interact()
    {
        selected = true;
    }

    public void UnSelect()
    {
        selected = false;
    }
}
