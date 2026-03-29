using System.Collections.Generic;
using UnityEngine;


// Clean data for one road entering an intersection hub
[System.Serializable]
public struct JunctionEdge
{
    public RoadEdge edgeRef;      // The road this data belongs to
    public Vector3 roadCenter;    // Where the road stops (pulled back by intersectionRadius)
    public Vector3 leftVertex;    // Left edge of the road at that stop point
    public Vector3 rightVertex;   // Right edge of the road at that stop point
    public Vector3 direction;     // Pointing OUTWARD from the node center
    public float angle;           // For circular sorting (Atan2)
}


[System.Serializable] // Allows Unity to eventually display this data in the Inspector
public class RoadNode
{
    public Vector3 position;
    public float radius; // How far roads pull back from center

    // The critical graph link: A list of all roads touching this intersection
    [SerializeReference] public List<RoadEdge> connectedEdges = new List<RoadEdge>();

    // The new Triangle Fan data — populated by CalculateJunctionEdges()
    public List<JunctionEdge> junctionEdges = new List<JunctionEdge>();

    // Constructor
    public RoadNode(Vector3 pos, float r = 5f)
    {
        position = pos;
        radius = r;
    }

    // Calculates the clean junction data for every road entering this node.
    // Call this instead of the old polygon pipeline.
    public void CalculateJunctionEdges(float intersectionRadius)
    {
        junctionEdges.Clear();

        if (connectedEdges.Count < 2) return;

        // 1. Build a JunctionEdge for each connected road
        for (int i = 0; i < connectedEdges.Count; i++)
        {
            RoadEdge edge = connectedEdges[i];

            // Direction pointing OUTWARD from this node toward the other end
            RoadNode neighbor = (edge.a == this) ? edge.b : edge.a;
            Vector3 dir = (neighbor.position - position).normalized;

            // Pull the road center back from the node center
            Vector3 roadCenter = position + dir * intersectionRadius;

            // Perpendicular (left-hand rule: Cross(up, forward) = right)
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            float halfWidth = edge.width / 2f;

            Vector3 leftVertex = roadCenter - right * halfWidth;
            Vector3 rightVertex = roadCenter + right * halfWidth;

            float angle = Mathf.Atan2(dir.z, dir.x);

            junctionEdges.Add(new JunctionEdge
            {
                edgeRef = edge,
                roadCenter = roadCenter,
                leftVertex = leftVertex,
                rightVertex = rightVertex,
                direction = dir,
                angle = angle
            });
        }

        // 2. Sort circularly (descending angle = clockwise when viewed top-down)
        junctionEdges.Sort((a, b) => b.angle.CompareTo(a.angle));
    }

    // Handles smoothing for Dead Ends (1 road) and Pass-Throughs (2 roads)
    public void SmoothSplines()
    {
        if (connectedEdges.Count == 1)
        {
            // DEAD END: Just point the control handle straight toward the other node
            RoadEdge edge = connectedEdges[0];
            RoadNode neighbor = (edge.a == this) ? edge.b : edge.a;

            Vector3 dir = (neighbor.position - this.position).normalized;
            float dist = Vector3.Distance(this.position, neighbor.position);

            // Set the control point 1/3rd of the way down the line
            if (edge.a == this) edge.controlPoint1 = this.position + dir * (dist * 0.33f);
            else edge.controlPoint2 = this.position + dir * (dist * 0.33f);
        }
        else if (connectedEdges.Count == 2)
        {
            // PASS-THROUGH: Enforce Tangent Continuity (Lock handles at 180 degrees)
            RoadEdge edge1 = connectedEdges[0];
            RoadEdge edge2 = connectedEdges[1];

            RoadNode neighbor1 = (edge1.a == this) ? edge1.b : edge1.a;
            RoadNode neighbor2 = (edge2.a == this) ? edge2.b : edge2.a;

            // The master tangent runs parallel to the invisible line connecting the two neighbors
            Vector3 masterTangent = (neighbor1.position - neighbor2.position).normalized;

            float dist1 = Vector3.Distance(this.position, neighbor1.position);
            float dist2 = Vector3.Distance(this.position, neighbor2.position);

            // Push edge 1's handle along the master tangent
            if (edge1.a == this) edge1.controlPoint1 = this.position + masterTangent * (dist1 * 0.33f);
            else edge1.controlPoint2 = this.position + masterTangent * (dist1 * 0.33f);

            // Push edge 2's handle in the EXACT OPPOSITE direction (-masterTangent)
            if (edge2.a == this) edge2.controlPoint1 = this.position - masterTangent * (dist2 * 0.33f);
            else edge2.controlPoint2 = this.position - masterTangent * (dist2 * 0.33f);
        }
    }
}