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

    // Trims the Bezier curves exactly where they hit the polygon boundary
    public void TrimIntersectingRoads()
    {
        if (polygonVertices.Count < 2) return;

        int resolution = 20; // Resolution of our collision check

        for (int i = 0; i < connectedEdges.Count; i++)
        {
            RoadEdge edge = connectedEdges[i];

            // 1. Get the 2D magenta polygon edge
            Vector3 cornerA3D = polygonVertices[i];
            Vector3 cornerB3D = polygonVertices[(i + 1) % polygonVertices.Count];
            Vector2 polyA = new Vector2(cornerA3D.x, cornerA3D.z);
            Vector2 polyB = new Vector2(cornerB3D.x, cornerB3D.z);

            // 2. Walk the Bezier curve
            Vector3 previousPoint3D = edge.a == this ? edge.a.position : edge.b.position;
            float previousT = edge.a == this ? 0f : 1f;

            for (int step = 1; step <= resolution; step++)
            {
                float currentT = edge.a == this ? (step / (float)resolution) : (1f - (step / (float)resolution));
                Vector3 currentPoint3D = MathUtility.CalculateBezierPoint(currentT, edge.a.position, edge.controlPoint1, edge.controlPoint2, edge.b.position);

                Vector2 curveStart = new Vector2(previousPoint3D.x, previousPoint3D.z);
                Vector2 curveEnd = new Vector2(currentPoint3D.x, currentPoint3D.z);

                // 3. The Collision Cut
                if (LineSegmentsIntersect2D(curveStart, curveEnd, polyA, polyB, out float fraction))
                {
                    float exactT = previousT + ((currentT - previousT) * fraction);

                    if (edge.a == this) edge.trimStart = exactT;
                    else edge.trimEnd = exactT;

                    break;
                }

                previousPoint3D = currentPoint3D;
                previousT = currentT;
            }
        }
    }

    // Pure 2D Line Intersection Math (Ignoring Y axis)
    bool LineSegmentsIntersect2D(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out float fractionP1P2)
    {
        fractionP1P2 = 0f;

        Vector2 p1p2 = p2 - p1;
        Vector2 p3p4 = p4 - p3;
        Vector2 p1p3 = p3 - p1;

        // 2D Cross product to find if the lines are parallel
        float determinant = (p1p2.x * p3p4.y) - (p1p2.y * p3p4.x);

        if (Mathf.Abs(determinant) < 0.0001f) return false; // Lines are parallel

        // Calculate the exact fractional collision points for both lines
        float tA = ((p1p3.x * p3p4.y) - (p1p3.y * p3p4.x)) / determinant;
        float tB = ((p1p3.x * p1p2.y) - (p1p3.y * p1p2.x)) / determinant;

        // If both fractions are between 0 and 1, the segments physically cross!
        if (tA >= 0f && tA <= 1f && tB >= 0f && tB <= 1f)
        {
            fractionP1P2 = tA;
            return true;
        }

        return false;
    }
}