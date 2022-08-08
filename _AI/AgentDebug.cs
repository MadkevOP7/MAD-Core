using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
public class AgentDebug : MonoBehaviour
{
    [Header("Agent Debug")]
    public Vector3 agent_target_position;
    private NavMeshAgent agent;
    public Transform player;
    // Start is called before the first frame update
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        InvokeRepeating("Calc", 0, 1.0f);

    }

    // Update is called once per frame
    void Update()
    {

    }
    public void Calc()
    {
            if (agent == null && GetComponent<NavMeshAgent>() != null)
            {
                agent = GetComponent<NavMeshAgent>();
            }
            agent.SetDestination(player.transform.position);
            agent_target_position = agent.destination;
        
    }
}
