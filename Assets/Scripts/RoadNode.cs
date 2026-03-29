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

        for (int i = 0; i < connectedEdges.Count; i++)
        {
            RoadEdge edge = connectedEdges[i];
            bool isNodeA = (edge.a == this);

            Debug.Log($"<color=green>[2. Junctions] Node@{position} edge {i}: Reading CP1={edge.controlPoint1} CP2={edge.controlPoint2}</color>");

            // 1. Binary search for the exact t where distance from node == intersectionRadius
            float tLow = 0f;
            float tHigh = 1f;
            float t = 0.5f;

            for (int j = 0; j < 15; j++)
            {
                t = (tLow + tHigh) / 2f;
                Vector3 pt = MathUtility.CalculateBezierPoint(t, edge.a.position, edge.controlPoint1, edge.controlPoint2, edge.b.position);
                float dist = Vector3.Distance(position, pt);

                if (isNodeA)
                {
                    if (dist < intersectionRadius) tLow = t; else tHigh = t;
                }
                else
                {
                    if (dist < intersectionRadius) tHigh = t; else tLow = t;
                }
            }

            // 2. Apply trim immediately so road mesh matches hub
            if (isNodeA) edge.trimStart = t;
            else edge.trimEnd = t;

            // 3. Evaluate spline at t for the road center
            Vector3 roadCenter = MathUtility.CalculateBezierPoint(t, edge.a.position, edge.controlPoint1, edge.controlPoint2, edge.b.position);

            // 4. Tangent via finite difference (always step toward higher t)
            float epsilon = 0.001f;
            float tNext = Mathf.Min(t + epsilon, 1f);
            float tPrev = Mathf.Max(t - epsilon, 0f);
            Vector3 pNext = MathUtility.CalculateBezierPoint(tNext, edge.a.position, edge.controlPoint1, edge.controlPoint2, edge.b.position);
            Vector3 pPrev = MathUtility.CalculateBezierPoint(tPrev, edge.a.position, edge.controlPoint1, edge.controlPoint2, edge.b.position);
            Vector3 tangent = (pNext - pPrev).normalized;

            // 5. CRITICAL: Outward tangent always points AWAY from the intersection
            Vector3 outwardTangent = isNodeA ? tangent.normalized : -tangent.normalized;

            // 6. Perpendicular from outward tangent: Cross(up, outward) = right
            Vector3 right = Vector3.Cross(Vector3.up, outwardTangent).normalized;
            float halfWidth = edge.width / 2f;

            Vector3 leftVertex = roadCenter - right * halfWidth;
            Vector3 rightVertex = roadCenter + right * halfWidth;

            float angle = Mathf.Atan2(outwardTangent.z, outwardTangent.x);

            junctionEdges.Add(new JunctionEdge
            {
                edgeRef = edge,
                roadCenter = roadCenter,
                leftVertex = leftVertex,
                rightVertex = rightVertex,
                direction = outwardTangent,
                angle = angle
            });
        }

        // Sort circularly (descending angle = clockwise when viewed top-down)
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
        else if (connectedEdges.Count >= 3)
        {
            // INTERSECTION: Find the "most straight-through" pair by minimizing
            // the angle between dirI and -dirJ (most collinear = smallest deviation)
            RoadEdge bestA = null;
            RoadEdge bestB = null;
            float minDeviation = float.MaxValue;

            for (int i = 0; i < connectedEdges.Count; i++)
            {
                for (int j = i + 1; j < connectedEdges.Count; j++)
                {
                    RoadNode ni = (connectedEdges[i].a == this) ? connectedEdges[i].b : connectedEdges[i].a;
                    RoadNode nj = (connectedEdges[j].a == this) ? connectedEdges[j].b : connectedEdges[j].a;

                    Vector3 dirI = (ni.position - this.position).normalized;
                    Vector3 dirJ = (nj.position - this.position).normalized;

                    // Angle between dirI and the NEGATED dirJ: 0° = perfect straight-through
                    float deviation = Vector3.Angle(dirI, -dirJ);

                    if (deviation < minDeviation)
                    {
                        minDeviation = deviation;
                        bestA = connectedEdges[i];
                        bestB = connectedEdges[j];
                    }
                }
            }

            // Apply logic based on whether there's a clear main road
            if (minDeviation < 45f && bestA != null && bestB != null)
            {
                // Clear main road: apply pass-through tangent continuity
                RoadNode neighborA = (bestA.a == this) ? bestA.b : bestA.a;
                RoadNode neighborB = (bestB.a == this) ? bestB.b : bestB.a;

                Vector3 masterTangent = (neighborA.position - neighborB.position).normalized;

                float distA = Vector3.Distance(this.position, neighborA.position);
                float distB = Vector3.Distance(this.position, neighborB.position);

                if (bestA.a == this) bestA.controlPoint1 = this.position + masterTangent * (distA * 0.33f);
                else bestA.controlPoint2 = this.position + masterTangent * (distA * 0.33f);

                if (bestB.a == this) bestB.controlPoint1 = this.position - masterTangent * (distB * 0.33f);
                else bestB.controlPoint2 = this.position - masterTangent * (distB * 0.33f);

                // Side streets: point directly at their neighbor
                foreach (RoadEdge edge in connectedEdges)
                {
                    if (edge == bestA || edge == bestB) continue;

                    RoadNode neighbor = (edge.a == this) ? edge.b : edge.a;
                    float dist = Vector3.Distance(this.position, neighbor.position);
                    Vector3 newCP = this.position + ((neighbor.position - this.position).normalized * dist * 0.33f);

                    if (edge.a == this) edge.controlPoint1 = newCP;
                    else edge.controlPoint2 = newCP;
                }
            }
            else
            {
                // Y-Junction / no clear main road: point all handles symmetrically outward
                foreach (RoadEdge edge in connectedEdges)
                {
                    RoadNode neighbor = (edge.a == this) ? edge.b : edge.a;
                    float dist = Vector3.Distance(this.position, neighbor.position);
                    Vector3 dir = (neighbor.position - this.position).normalized;
                    Vector3 newCP = this.position + (dir * dist * 0.25f);

                    if (edge.a == this) edge.controlPoint1 = newCP;
                    else edge.controlPoint2 = newCP;
                }
            }
        }

        if (connectedEdges.Count > 0)
            Debug.Log($"<color=blue>[1. Splines] Smoothed node@{position}. CP1={connectedEdges[0].controlPoint1}</color>");
    }
}