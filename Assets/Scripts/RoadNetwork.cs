using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class RoadNetwork : MonoBehaviour
{
    // The Master Netlist
    public List<RoadNode> nodes = new List<RoadNode>();
    public List<RoadEdge> edges = new List<RoadEdge>();

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
    // This allows us to see our pure data in the Scene view without generating meshes
    void OnDrawGizmos()
    {
        // Draw the Intersections (Cyan Spheres)
        Gizmos.color = Color.cyan;
        foreach (RoadNode node in nodes)
        {
            Gizmos.DrawWireSphere(node.position, node.radius);
        }

        // Draw the Roads (Yellow Lines)
        Gizmos.color = Color.yellow;
        foreach (RoadEdge edge in edges)
        {
            if (edge.a != null && edge.b != null)
            {
                // We draw straight lines for now to verify the graph connections
                Gizmos.DrawLine(edge.a.position, edge.b.position);
            }
        }
    }
}