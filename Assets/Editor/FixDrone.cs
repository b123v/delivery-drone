using UnityEngine;
using UnityEditor;
using Unity.MLAgents.Policies;

public class FixDrone 
{
    [MenuItem("Tools/Fix Drone ML Agents")]
    public static void Fix() 
    {
        var drone = GameObject.Find("DroneSketchfab");
        if (drone != null) 
        {
            var bp = drone.GetComponent<BehaviorParameters>();
            if (bp != null) 
            {
                bp.BrainParameters.VectorObservationSize = 19;
                bp.BrainParameters.ActionSpec = new Unity.MLAgents.Actuators.ActionSpec(4, new[] { 2 });
                
                // Force Heuristic Only so the user can control it
                bp.BehaviorType = BehaviorType.HeuristicOnly;
                
                EditorUtility.SetDirty(bp);
                Debug.Log("Fixed Drone ML-Agents ActionSpec and VectorObservationSize!");
            }
            else 
            {
                Debug.LogError("BehaviorParameters not found on DroneSketchfab!");
            }
        }
        else 
        {
            Debug.LogError("DroneSketchfab not found!");
        }
    }
}
