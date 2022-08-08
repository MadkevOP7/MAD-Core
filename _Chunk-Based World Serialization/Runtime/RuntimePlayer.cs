using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RuntimePlayer : MonoBehaviour
{
    private RuntimeChunk last_chunk;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("RuntimeChunk"))
        {
            RuntimeChunk c = other.GetComponent<RuntimeChunk>();
            ObjectPool.Instance.UpdateCenterChunk(c, last_chunk);
            last_chunk = c;
        }
    }
}
