using UnityEngine;
using PathCreation; // This lets us talk to Sebastian's spline math!

[ExecuteAlways] // This forces the script to run in the Editor so we don't have to press Play
public class RoadConnector : MonoBehaviour
{
    [Header("The Roads")]
    public PathCreator incomingRoad; // The road that is ending
    public PathCreator mainRoad;     // The continuous road it wants to plug into

    void Update()
    {
        // Safety check: Don't run if we haven't assigned the roads yet
        if (incomingRoad == null || mainRoad == null) return;
        if (incomingRoad.path == null || mainRoad.path == null) return;

        // 1. Find the physical end of the incoming road
        // (NumPoints gives us the total, so we subtract 1 to get the exact last vertex)
        Vector3 roadEndPos = incomingRoad.path.GetPoint(incomingRoad.path.NumPoints - 1);

        // 2. The Magic Math: Find the absolute closest point on the main road
        // PathCreator has a brilliant built-in calculus function for this!
        Vector3 targetConnectionPos = mainRoad.path.GetClosestPointOnPath(roadEndPos);

        // 3. Draw a laser to prove we found the exact gap
        Debug.DrawLine(roadEndPos, targetConnectionPos, Color.green);

        // Draw some poles so we can easily see the start and end points
        Debug.DrawRay(roadEndPos, Vector3.up * 3f, Color.red);
        Debug.DrawRay(targetConnectionPos, Vector3.up * 3f, Color.blue);
    }
}