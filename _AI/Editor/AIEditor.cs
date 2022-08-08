using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor (typeof (GhostAI))]
public class AIEditor : Editor {

	void OnSceneGUI() {
		GhostAI ai = (GhostAI)target;
		Handles.color = Color.white;
		Handles.DrawWireArc (ai.transform.position, Vector3.up, Vector3.forward, 360, ai.viewRadius);
		Vector3 viewAngleA = ai.DirFromAngle (-ai.viewAngle / 2, false);
		Vector3 viewAngleB = ai.DirFromAngle (ai.viewAngle / 2, false);

		Handles.DrawLine (ai.transform.position, ai.transform.position + viewAngleA * ai.viewRadius);
		Handles.DrawLine (ai.transform.position, ai.transform.position + viewAngleB * ai.viewRadius);

        Handles.Label(ai.transform.position + new Vector3(0, 1, 0), "Action: " + ai.action.ToString());

        if (ai.playerTarget != null)
        {
            Handles.color = Color.red;
            Handles.DrawLine(ai.transform.position, ai.playerTarget.position);
        }
		
        if (ai.attacking_item != null)
        {
            Handles.color = Color.blue;
            Handles.DrawLine(ai.transform.position, ai.attacking_item.transform.position);
        }

        if (ai.agent != null)
        {
            if (ai.agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathComplete)
            {
                Handles.color = Color.green;
            }
            else
            {
                Handles.color = Color.red;
            }
            for (int i = 0; i < ai.agent.path.corners.Length - 1; i++)
            {
                Handles.DrawLine(ai.agent.path.corners[i], ai.agent.path.corners[i + 1]);
            }
        }

    }

}
