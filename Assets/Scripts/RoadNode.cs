using System.Collections.Generic;
using UnityEngine;

[System.Serializable] // Allows Unity to eventually display this data in the Inspector
public class RoadNode
{
    public Vector3 position;
    public float radius; // How big the intersection polygon needs to be

    // The critical graph link: A list of all roads touching this intersection
    public List<RoadEdge> connectedEdges = new List<RoadEdge>();

    // Constructor
    public RoadNode(Vector3 pos, float r = 5f)
    {
        position = pos;
        radius = r;
    }
}