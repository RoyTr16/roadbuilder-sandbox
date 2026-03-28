using UnityEngine;

[System.Serializable]
public class RoadEdge
{
    public RoadNode a;
    public RoadNode b;
    public float width;

    // Constructor
    public RoadEdge(RoadNode nodeA, RoadNode nodeB, float w = 6f)
    {
        a = nodeA;
        b = nodeB;
        width = w;

        // Auto-wiring: When a road is created, tell the nodes they are connected!
        if (!a.connectedEdges.Contains(this)) a.connectedEdges.Add(this);
        if (!b.connectedEdges.Contains(this)) b.connectedEdges.Add(this);
    }
}