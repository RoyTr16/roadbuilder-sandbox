using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class RoadNetwork : MonoBehaviour
{
    // The Master Netlist
    [SerializeReference] public List<RoadNode> nodes = new List<RoadNode>();
    [SerializeReference] public List<RoadEdge> edges = new List<RoadEdge>();

    // --- OUR PROGRAMMATIC API ---

    public RoadNode CreateNode(Vector3 position)
    {
        RoadNode newNode = new RoadNode(position);
        nodes.Add(newNode);
        return newNode;
    }

    public RoadEdge ConnectNodes(RoadNode a, RoadNode b)
    {
        RoadEdge newEdge = new RoadEdge(a, b);
        edges.Add(newEdge);
        return newEdge;
    }

    public void ClearNetwork()
    {
        nodes.Clear();
        edges.Clear();
    }

    // --- VISUALIZATION (THE LOGIC ANALYZER) ---
    void OnDrawGizmos()
    {
        // Draw Intersections
        foreach (RoadNode node in nodes)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(node.position, node.radius);

            // Draw the Hub Polygon (Magenta)
            if (node.polygonVertices.Count > 0)
            {
                Gizmos.color = Color.magenta;
                for (int i = 0; i < node.polygonVertices.Count; i++)
                {
                    // Draw a line from the current corner to the next corner
                    Vector3 currentCorner = node.polygonVertices[i];
                    Vector3 nextCorner = node.polygonVertices[(i + 1) % node.polygonVertices.Count];
                    Gizmos.DrawLine(currentCorner, nextCorner);
                }
            }
        }

        // Draw Roads & Handles
        foreach (RoadEdge edge in edges)
        {
            if (edge.a != null && edge.b != null)
            {
                // 1. Draw the Bezier Curve Spine (Yellow)
                Gizmos.color = Color.yellow;
                int segments = 20;
                Vector3 previousPoint = edge.a.position;

                for (int i = 1; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    Vector3 currentPoint = CalculateBezierPoint(t, edge.a.position, edge.controlPoint1, edge.controlPoint2, edge.b.position);
                    Gizmos.DrawLine(previousPoint, currentPoint);
                    previousPoint = currentPoint;
                }

                // 2. Draw the Control Handles (Green) so we can see the math
                Gizmos.color = Color.green;
                Gizmos.DrawLine(edge.a.position, edge.controlPoint1);
                Gizmos.DrawSphere(edge.controlPoint1, 1f); // P1 Handle

                Gizmos.DrawLine(edge.b.position, edge.controlPoint2);
                Gizmos.DrawSphere(edge.controlPoint2, 1f); // P2 Handle
            }
        }
    }

    // Pure cubic Bezier math
    Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 p = uuu * p0;
        p += 3 * uu * t * p1;
        p += 3 * u * tt * p2;
        p += ttt * p3;

        return p;
    }

    // --- THE TESTBENCH ---
    [ContextMenu("Generate Test Network")]
    public void GenerateTestNetwork()
    {
        ClearNetwork(); // Flush the old data

        // 1. Define the physical pins (Nodes)
        RoadNode centerHub = CreateNode(new Vector3(0, 0, 0));

        RoadNode northEnd = CreateNode(new Vector3(0, 0, 50));
        RoadNode southWestEnd = CreateNode(new Vector3(-40, 0, -40));
        RoadNode southEastEnd = CreateNode(new Vector3(40, 0, -40));

        // 2. Route the wires (Edges)
        ConnectNodes(centerHub, northEnd);
        ConnectNodes(centerHub, southWestEnd);
        ConnectNodes(centerHub, southEastEnd);

        // 3. Command the center hub to calculate its custom shape
        centerHub.CalculateIntersectionPolygon();

        // 4. ALIGN THE ROADS to the new shape
        centerHub.AlignRoadsToPolygon();

        Debug.Log("Test Network Generated! Check the Scene View.");

    }
}