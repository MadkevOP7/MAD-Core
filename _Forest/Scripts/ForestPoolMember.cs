//© 2022 by MADKEV Studio, all rights reserved

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
public class ForestPoolMember : MonoBehaviour
{
    public int treeID = -1;
    public CapsuleCollider c_collider;
    public NavMeshObstacle obstacle;
    public void InitializeMember(Matrix4x4 matrix, int treeID, ForestRuntimeData runtimeData)
    {
        gameObject.SetActive(true);
        this.treeID = treeID;
        transform.position = matrix.GetColumn(3);
        transform.localScale = new Vector3(
                            matrix.GetColumn(0).magnitude,
                            matrix.GetColumn(1).magnitude,
                            matrix.GetColumn(2).magnitude
                            );
        transform.rotation = Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
        c_collider.center = runtimeData.center;
        c_collider.height = runtimeData.height;
        c_collider.radius = runtimeData.radius;
        obstacle.center = runtimeData.center;
        obstacle.height = runtimeData.height;
        obstacle.radius = runtimeData.radius;
    }

    public void DeAllocateMember()
    {
        treeID = -1;
        gameObject.SetActive(false);
    }

    public void DestroyMember()
    {
        Destroy(gameObject);
    }
    public bool IsAllocated()
    {
        return treeID != -1;
    }

    public void Damage(int damage)
    {
        ForestManager.Instance.OnDamageTree(treeID, damage);
    }
}
