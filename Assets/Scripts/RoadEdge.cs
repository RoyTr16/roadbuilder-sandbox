using UnityEngine;

[System.Serializable]
public class RoadEdge
{
    [SerializeReference] public RoadNode a;
    [SerializeReference] public RoadNode b;
    public float width;

    // The Invisible Magnets (P1 and P2)
    public Vector3 controlPoint1;
    public Vector3 controlPoint2;

    // The Non-Destructive Trim Values (0.0 to 1.0)
    public float trimStart = 0f;
    public float trimEnd = 1f;

    public RoadEdge(RoadNode nodeA, RoadNode nodeB, float w = 6f)
    {
        a = nodeA;
        b = nodeB;
        width = w;

        // Auto-wire the nodes
        if (!a.connectedEdges.Contains(this)) a.connectedEdges.Add(this);
        if (!b.connectedEdges.Contains(this)) b.connectedEdges.Add(this);

        // Initialize the control points to form a perfectly straight line
        Vector3 direction = b.position - a.position;
        controlPoint1 = a.position + (direction * 0.33f); // 1/3rd of the way there
        controlPoint2 = b.position - (direction * 0.33f); // 1/3rd of the way back
    }

    // Updates the control point for a specific node to force an arrival angle
    public void SetControlPointForNode(RoadNode node, Vector3 newControlPoint)
    {
        if (node == a) controlPoint1 = newControlPoint;
        else if (node == b) controlPoint2 = newControlPoint;
    }
}