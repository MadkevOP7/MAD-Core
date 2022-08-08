using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class InstantiateHelper : MonoBehaviour
{
    public GameObject CreateInstatiation(GameObject go)
    {
        GameObject temp = Instantiate(go);
        Destroy(temp.GetComponent<NetworkIdentity>());
        Destroy(temp.GetComponent<Equipment>());
        return temp;
    }
}
