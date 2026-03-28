using System.Collections.Generic;
using UnityEngine;


[System.Serializable] // Allows Unity to eventually display this data in the Inspector
public class RoadNode
{
    public Vector3 position;
    public float radius; // How big the intersection polygon needs to be

    // The critical graph link: A list of all roads touching this intersection
    [SerializeReference] public List<RoadEdge> connectedEdges = new List<RoadEdge>();
    public List<Vector3> polygonVertices = new List<Vector3>();

    // Constructor
    public RoadNode(Vector3 pos, float r = 5f)
    {
        position = pos;
        radius = r;
    }

    // This function sorts the connected edges clockwise so we can draw a clean polygon
    public void SortEdgesClockwise()
    {
        // We use a bit of LINQ (Language Integrated Query) to easily sort the list
        connectedEdges.Sort((edge1, edge2) =>
        {
            // Calculate the vector pointing AWAY from the intersection for Edge 1
            // (We check which node is 'us' and look towards the other node)
            Vector3 dir1 = (edge1.a == this ? edge1.controlPoint1 : edge1.controlPoint2) - position;
            dir1 = dir1.normalized;
            // Get the full 360 angle using Atan2
            float angle1 = Mathf.Atan2(dir1.z, dir1.x);

            // Do the exact same thing for Edge 2
            Vector3 dir2 = (edge2.a == this ? edge2.controlPoint1 : edge2.controlPoint2) - position;
            dir2 = dir2.normalized;
            float angle2 = Mathf.Atan2(dir2.z, dir2.x);

            // Compare the angles to sort them from smallest to largest
            return angle1.CompareTo(angle2);
        });
    }

    // Generates the custom 2D footprint of the intersection
    public void CalculateIntersectionPolygon()
    {
        polygonVertices.Clear();

        // A Hub requires at least 2 roads (which forms a bent quad).
        // 1 road is just a dead end.
        if (connectedEdges.Count < 2) return;

        // Step 1: Ensure the roads are sorted in a circle so lines don't crisscross
        SortEdgesClockwise();

        // Step 2: Loop around the circle and calculate the bisecting corners
        for (int i = 0; i < connectedEdges.Count; i++)
        {
            // Get the Current road, and the Previous road
            // (Using the % modulo operator wraps the index back around to the end of the list safely)
            RoadEdge currEdge = connectedEdges[i];
            RoadEdge prevEdge = connectedEdges[(i - 1 + connectedEdges.Count) % connectedEdges.Count];

            // Calculate directions pointing AWAY from the intersection
            Vector3 dirCurr = (currEdge.a == this ? currEdge.controlPoint1 : currEdge.controlPoint2) - position;
            dirCurr = dirCurr.normalized;

            Vector3 dirPrev = (prevEdge.a == this ? prevEdge.controlPoint1 : prevEdge.controlPoint2) - position;
            dirPrev = dirPrev.normalized;

            // THE CHEAT CODE: Vector Addition perfectly bisects the angle
            Vector3 bisector = dirCurr + dirPrev;

            // SAFETY CATCH: If roads are exactly 180 degrees opposite, they cancel out to (0,0,0).
            if (bisector.sqrMagnitude < 0.001f)
            {
                // If they are a straight line, use the Cross Product to find the 90-degree corner
                bisector = Vector3.Cross(Vector3.up, dirCurr);
            }

            bisector = bisector.normalized;

            // Push the corner point out by the radius of the intersection
            Vector3 corner = position + (bisector * radius);
            polygonVertices.Add(corner);
        }
    }

    // Aligns the Bezier control points so the roads arrive 90-degrees to the polygon edges
    public void AlignRoadsToPolygon()
    {
        if (polygonVertices.Count < 2) return;

        // Loop through our sorted roads
        for (int i = 0; i < connectedEdges.Count; i++)
        {
            RoadEdge edge = connectedEdges[i];

            // 1. Identify the magenta line segment facing this road
            Vector3 cornerA = polygonVertices[i];
            Vector3 cornerB = polygonVertices[(i + 1) % polygonVertices.Count];

            // 2. Find the exact middle of that magenta line
            Vector3 midPoint = (cornerA + cornerB) / 2f;

            // 3. Find the direction of the magenta line (from A to B)
            Vector3 edgeDirection = (cornerB - cornerA).normalized;

            // 4. Calculate the Normal (perpendicular) pointing OUTWARD
            // In Unity, crossing the UP vector with a clockwise direction gives an outward normal
            Vector3 outwardNormal = Vector3.Cross(Vector3.up, edgeDirection).normalized;

            // 5. Place the Bezier Control Point exactly along that normal
            // We push it out by half the road's length (just a standard Bezier tangent strength)
            float tangentStrength = Vector3.Distance(edge.a.position, edge.b.position) * 0.33f;
            Vector3 newControlPoint = midPoint + (outwardNormal * tangentStrength);

            // 6. Update the edge's data
            edge.SetControlPointForNode(this, newControlPoint);
        }
    }
}