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

            // // Draw the Hub Polygon (Magenta)
            // if (node.polygonVertices.Count > 0)
            // {
            //     Gizmos.color = Color.magenta;
            //     for (int i = 0; i < node.polygonVertices.Count; i++)
            //     {
            //         // Draw a line from the current corner to the next corner
            //         Vector3 currentCorner = node.polygonVertices[i];
            //         Vector3 nextCorner = node.polygonVertices[(i + 1) % node.polygonVertices.Count];
            //         Gizmos.DrawLine(currentCorner, nextCorner);
            //     }
            // }
            // --- DIAGNOSTIC: DRAW THE PERIMETER PATH ---
            // if (node.diagnosticPerimeter != null && node.diagnosticPerimeter.Count > 0)
            // {
            //     for (int i = 0; i < node.diagnosticPerimeter.Count; i++)
            //     {
            //         Vector3 currentPoint = node.diagnosticPerimeter[i];
            //         Vector3 nextPoint = node.diagnosticPerimeter[(i + 1) % node.diagnosticPerimeter.Count];

            //         // Draw a sphere at every vertex
            //         Gizmos.color = Color.red;
            //         Gizmos.DrawSphere(currentPoint, 0.3f);

            //         // Draw a bright green line showing the exact path it travels to the next vertex
            //         Gizmos.color = Color.green;

            //         // We draw the line slightly elevated so it doesn't clip into the broken mesh
            //         Gizmos.DrawLine(currentPoint + Vector3.up * 0.5f, nextPoint + Vector3.up * 0.5f);
            //     }
            // }
        }

        // Draw Roads & Handles
        foreach (RoadEdge edge in edges)
        {
            if (edge.a != null && edge.b != null)
            {
                // 1. Draw the Bezier Curve Spine (Yellow)
                Gizmos.color = Color.yellow;
                int segments = 20;

                // Start exactly at the trim value!
                Vector3 previousPoint = MathUtility.CalculateBezierPoint(edge.trimStart, edge.a.position, edge.controlPoint1, edge.controlPoint2, edge.b.position);

                for (int i = 1; i <= segments; i++)
                {
                    // Calculate the percentage strictly between trimStart and trimEnd
                    float lerpFactor = i / (float)segments;
                    float t = Mathf.Lerp(edge.trimStart, edge.trimEnd, lerpFactor);

                    Vector3 currentPoint = MathUtility.CalculateBezierPoint(t, edge.a.position, edge.controlPoint1, edge.controlPoint2, edge.b.position);
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
            // --- DIAGNOSTIC: BEZIER X-RAY ---
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(edge.controlPoint1, 0.5f);
            Gizmos.DrawSphere(edge.controlPoint2, 0.5f);

            // Draw lines connecting the handles to their owner nodes
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(edge.a.position, edge.controlPoint1);
            Gizmos.DrawLine(edge.b.position, edge.controlPoint2);
        }
    }

    // --- DIAGNOSTIC: STATE DUMP ---
    public void DumpNetworkState()
    {
        Debug.LogWarning("=== ROAD NETWORK STATE DUMP ===");
        Debug.Log($"Total Nodes: {nodes.Count} | Total Edges: {edges.Count}");

        for (int i = 0; i < nodes.Count; i++)
        {
            RoadNode n = nodes[i];
            Debug.Log($"[Node {i}] Pos: {n.position} | Edges Attached: {n.connectedEdges.Count}");

            for (int e = 0; e < n.connectedEdges.Count; e++)
            {
                RoadEdge edge = n.connectedEdges[e];
                string role = (edge.a == n) ? "Start (Control 1)" : "End (Control 2)";
                Vector3 handlePos = (edge.a == n) ? edge.controlPoint1 : edge.controlPoint2;

                Debug.Log($"   -> Edge {e} is {role}. Handle at: {handlePos}");
            }
        }
        Debug.LogWarning("===============================");
    }

    // --- THE FABRICATOR (MESH GENERATION) ---
    public Material defaultRoadMaterial; // Drag a material here in the Inspector!

    public void BuildPhysicalRoads()
    {
        // 1. Demolish the old physical city before building the new one
        // (We iterate backwards when destroying children in Unity to avoid index shifting)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        // 2. Loop through our pure data and build the physical traces
        foreach (RoadEdge edge in edges)
        {
            if (edge.a == null || edge.b == null) continue;

            // Create an empty GameObject to hold this specific road's mesh
            GameObject roadGO = new GameObject("Physical_Road_Edge");
            roadGO.transform.SetParent(this.transform); // Organize it under the Motherboard

            MeshFilter filter = roadGO.AddComponent<MeshFilter>();
            MeshRenderer renderer = roadGO.AddComponent<MeshRenderer>();

            // Assign a default material if one is provided
            if (defaultRoadMaterial != null) renderer.material = defaultRoadMaterial;

            // 3. Generate the Custom Mesh
            filter.sharedMesh = GenerateSingleRoadMesh(edge);
        }

        // 4. Loop through the Nodes and build the Hubs
        foreach (RoadNode node in nodes)
        {
            if (node.connectedEdges.Count > 2)
            {
                Mesh hubMesh = GenerateIntersectionMesh(node);
                if (hubMesh == null) continue;

                GameObject hubGO = new GameObject("Physical_Intersection_Hub");
                hubGO.transform.SetParent(this.transform);

                MeshFilter filter = hubGO.AddComponent<MeshFilter>();
                MeshRenderer renderer = hubGO.AddComponent<MeshRenderer>();

                if (defaultRoadMaterial != null) renderer.material = defaultRoadMaterial;

                filter.sharedMesh = hubMesh;
            }
        }
    }

    Mesh GenerateSingleRoadMesh(RoadEdge edge)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Procedural_Asphalt";

        int resolution = 20; // How smooth the curve is
        int vertexCount = (resolution + 1) * 2; // Left and Right vertex for every step

        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[resolution * 6]; // 2 triangles (6 points) per segment

        int vertIndex = 0;
        int triIndex = 0;

        for (int step = 0; step <= resolution; step++)
        {
            // Calculate where we are strictly within the trimmed boundaries!
            float lerpFactor = step / (float)resolution;
            float currentT = Mathf.Lerp(edge.trimStart, edge.trimEnd, lerpFactor);

            // Calculate Current Point
            Vector3 currentPoint = MathUtility.CalculateBezierPoint(currentT, edge.a.position, edge.controlPoint1, edge.controlPoint2, edge.b.position);

            // Hack the Tangent (Look slightly ahead to find the Forward direction)
            // We clamp to 1.0f so we don't accidentally look past the end of the curve
            float nextT = Mathf.Min(currentT + 0.001f, 1.0f);
            Vector3 nextPoint = MathUtility.CalculateBezierPoint(nextT, edge.a.position, edge.controlPoint1, edge.controlPoint2, edge.b.position);

            // If we are at the very end of the line, look slightly backwards instead
            if (currentT >= 0.999f)
            {
                float prevT = currentT - 0.001f;
                Vector3 prevPoint = MathUtility.CalculateBezierPoint(prevT, edge.a.position, edge.controlPoint1, edge.controlPoint2, edge.b.position);
                nextPoint = currentPoint + (currentPoint - prevPoint);
            }

            Vector3 forward = (nextPoint - currentPoint).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            // Push vertices left and right by half the width
            float halfWidth = edge.width / 2f;
            vertices[vertIndex] = currentPoint - (right * halfWidth);     // Left Vertex
            vertices[vertIndex + 1] = currentPoint + (right * halfWidth); // Right Vertex

            // Standard UV mapping (0 to 1 along the road, 0 to 1 across the width)
            uvs[vertIndex] = new Vector2(0, lerpFactor);
            uvs[vertIndex + 1] = new Vector2(1, lerpFactor);

            // Stitch the Triangles together (Skipping the very last step since there is no "next" step to connect to)
            if (step < resolution)
            {
                triangles[triIndex + 0] = vertIndex;           // Bottom Left
                triangles[triIndex + 1] = vertIndex + 2;       // Top Left
                triangles[triIndex + 2] = vertIndex + 1;       // Bottom Right

                triangles[triIndex + 3] = vertIndex + 1;       // Bottom Right
                triangles[triIndex + 4] = vertIndex + 2;       // Top Left
                triangles[triIndex + 5] = vertIndex + 3;       // Top Right
                triIndex += 6;
            }

            vertIndex += 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals(); // Tells Unity how light should bounce off the asphalt

        return mesh;
    }

    Mesh GenerateIntersectionMesh(RoadNode node)
    {
        if (node.polygonVertices.Count < 2) return null; // Need at least a corner to draw a mesh

        Mesh mesh = new Mesh();
        mesh.name = "Procedural_Fillet_Hub";

        // We will collect all the points of our outer perimeter here
        List<Vector3> perimeterVertices = new List<Vector3>();

        for (int i = 0; i < node.connectedEdges.Count; i++)
        {
            RoadEdge currentRoad = node.connectedEdges[i];
            RoadEdge nextRoad = node.connectedEdges[(i + 1) % node.connectedEdges.Count];

            // 1. GET CURRENT ROAD'S VERTICES
            // Figure out which way is "Outward" from the intersection
            float tCurr = (currentRoad.a == node) ? currentRoad.trimStart : currentRoad.trimEnd;
            float offsetCurr = (currentRoad.a == node) ? 0.01f : -0.01f;
            float tCurrOutward = Mathf.Clamp(tCurr + offsetCurr, 0f, 1f);

            Vector3 posCurr = MathUtility.CalculateBezierPoint(tCurr, currentRoad.a.position, currentRoad.controlPoint1, currentRoad.controlPoint2, currentRoad.b.position);
            Vector3 posCurrOutward = MathUtility.CalculateBezierPoint(tCurrOutward, currentRoad.a.position, currentRoad.controlPoint1, currentRoad.controlPoint2, currentRoad.b.position);

            Vector3 outCurr = (posCurrOutward - posCurr).normalized;
            Vector3 rightCurr = Vector3.Cross(Vector3.up, outCurr).normalized;

            Vector3 currentRoadRightVertex = posCurr + (rightCurr * (currentRoad.width / 2f));
            Vector3 currentRoadLeftVertex = posCurr - (rightCurr * (currentRoad.width / 2f));

            // 2. GET NEXT ROAD'S LEFT VERTEX
            float tNext = (nextRoad.a == node) ? nextRoad.trimStart : nextRoad.trimEnd;
            float offsetNext = (nextRoad.a == node) ? 0.01f : -0.01f;
            float tNextOutward = Mathf.Clamp(tNext + offsetNext, 0f, 1f);

            Vector3 posNext = MathUtility.CalculateBezierPoint(tNext, nextRoad.a.position, nextRoad.controlPoint1, nextRoad.controlPoint2, nextRoad.b.position);
            Vector3 posNextOutward = MathUtility.CalculateBezierPoint(tNextOutward, nextRoad.a.position, nextRoad.controlPoint1, nextRoad.controlPoint2, nextRoad.b.position);

            Vector3 outNext = (posNextOutward - posNext).normalized;
            Vector3 rightNext = Vector3.Cross(Vector3.up, outNext).normalized;

            Vector3 nextRoadLeftVertex = posNext - (rightNext * (nextRoad.width / 2f));

            // 3. BUILD THE CONTINUOUS PERIMETER LOOP
            // Add the straight line across the end of the current road
            perimeterVertices.Add(currentRoadLeftVertex);
            perimeterVertices.Add(currentRoadRightVertex);

            // 4. SWEEP THE CURVE TO THE NEXT ROAD
            // Find the exact middle if we were to draw a straight line between the roads
            Vector3 straightLineMidpoint = Vector3.Lerp(currentRoadRightVertex, nextRoadLeftVertex, 0.5f);

            // The Tension Slider (0.0 = straight line, 1.0 = deep V-shape)
            float curveTightness = 0.08f;

            // Pull the magnet back so it doesn't dive too deep into the center
            Vector3 controlMagnet = Vector3.Lerp(straightLineMidpoint, node.position, curveTightness);

            int curveResolution = 5;

            for (int step = 1; step < curveResolution; step++)
            {
                float t = step / (float)curveResolution;

                // The Double-Lerp Quadratic Bezier Cheat Code
                Vector3 l1 = Vector3.Lerp(currentRoadRightVertex, controlMagnet, t);
                Vector3 l2 = Vector3.Lerp(controlMagnet, nextRoadLeftVertex, t);
                Vector3 curvePoint = Vector3.Lerp(l1, l2, t);

                perimeterVertices.Add(curvePoint);
            }
        }

        // --- NEW DIAGNOSTIC INTERCEPT ---
        // Save the exact perimeter sequence back to the node before we build the mesh
        node.diagnosticPerimeter = new List<Vector3>(perimeterVertices);

        // 5. TRIANGULATE THE FAN
        // Now that we have a perfect circle of vertices, we connect them all to the center point
        int totalVertices = perimeterVertices.Count + 1; // Perimeter + 1 Center
        Vector3[] vertices = new Vector3[totalVertices];
        Vector2[] uvs = new Vector2[totalVertices];
        int[] triangles = new int[perimeterVertices.Count * 3];

        // Set Center Point
        vertices[0] = node.position;
        uvs[0] = new Vector2(node.position.x, node.position.z) * 0.1f; // Basic UV

        // Set Perimeter Points & Triangles
        for (int i = 0; i < perimeterVertices.Count; i++)
        {
            vertices[i + 1] = perimeterVertices[i];
            uvs[i + 1] = new Vector2(vertices[i + 1].x, vertices[i + 1].z) * 0.1f;

            int currentCorner = i + 1;
            int nextCorner = (i + 1) % perimeterVertices.Count + 1;

            triangles[i * 3 + 0] = 0;             // Center
            triangles[i * 3 + 1] = currentCorner; // Corner A
            triangles[i * 3 + 2] = nextCorner;    // Corner B
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        return mesh;
    }

    // --- THE MASTER REBUILDER ---
    // The Editor tool will call this every time you click the mouse
    public void RebuildNetwork()
    {
        // 1. Process all Nodes based on their type
        foreach (RoadNode node in nodes)
        {
            if (node.connectedEdges.Count > 2)
            {
                // It's a true intersection! Run the complex geometry math.
                node.CalculateIntersectionPolygon();
                node.AlignRoadsToPolygon();
                node.TrimIntersectingRoads();
            }
            else
            {
                // It's a Spline! Smooth the curves and ensure full rendering.
                node.SmoothSplines();

                foreach (RoadEdge edge in node.connectedEdges)
                {
                    if (edge.a == node) edge.trimStart = 0f;
                    if (edge.b == node) edge.trimEnd = 1f;
                }
                node.polygonVertices.Clear(); // Erase any old magenta intersection data
            }
        }

        // 2. Build Physical Geometry
        BuildPhysicalRoads();
    }
}