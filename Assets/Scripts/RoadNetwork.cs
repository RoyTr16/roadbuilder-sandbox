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

            // --- PHASE 1 VISUAL TEST: JunctionEdge Ring ---
            if (node.junctionEdges != null && node.junctionEdges.Count > 0)
            {
                for (int i = 0; i < node.junctionEdges.Count; i++)
                {
                    JunctionEdge je = node.junctionEdges[i];

                    // Blue sphere = Left vertex
                    Gizmos.color = Color.blue;
                    Gizmos.DrawSphere(je.leftVertex, 0.4f);

                    // Red sphere = Right vertex
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(je.rightVertex, 0.4f);

                    // White sphere = Road center (where road stops)
                    Gizmos.color = Color.white;
                    Gizmos.DrawSphere(je.roadCenter, 0.25f);

                    // Yellow ray = Outward direction
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(node.position, je.roadCenter);

                    // Green line = road-end edge (left to right)
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(je.leftVertex, je.rightVertex);
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

                Vector3 previousPoint = MathUtility.CalculateBezierPoint(edge.trimStart, edge.a.position, edge.controlPoint1, edge.controlPoint2, edge.b.position);

                for (int i = 1; i <= segments; i++)
                {
                    float lerpFactor = i / (float)segments;
                    float t = Mathf.Lerp(edge.trimStart, edge.trimEnd, lerpFactor);

                    Vector3 currentPoint = MathUtility.CalculateBezierPoint(t, edge.a.position, edge.controlPoint1, edge.controlPoint2, edge.b.position);
                    Gizmos.DrawLine(previousPoint, currentPoint);
                    previousPoint = currentPoint;
                }

                // 2. Draw the Control Handles (Green)
                Gizmos.color = Color.green;
                Gizmos.DrawLine(edge.a.position, edge.controlPoint1);
                Gizmos.DrawSphere(edge.controlPoint1, 0.5f);

                Gizmos.DrawLine(edge.b.position, edge.controlPoint2);
                Gizmos.DrawSphere(edge.controlPoint2, 0.5f);
            }
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

        Debug.Log("<color=cyan>[3. Road Meshes] Generating asphalt meshes using current trims.</color>");

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

        Debug.Log("<color=magenta>[4. Hub Meshes] Generating Intersection Hubs.</color>");

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
        if (node.junctionEdges.Count < 2) return null;

        int curveResolution = 8;
        float curveStrength = 0.2f;
        float uvScale = 0.2f;
        List<Vector3> perimeterVertices = new List<Vector3>();

        for (int i = 0; i < node.junctionEdges.Count; i++)
        {
            JunctionEdge currentEdge = node.junctionEdges[i];
            JunctionEdge nextEdge = node.junctionEdges[(i + 1) % node.junctionEdges.Count];

            // 1. Add the exact road-end cap vertices (left → right) — no math, pure data
            perimeterVertices.Add(currentEdge.leftVertex);
            perimeterVertices.Add(currentEdge.rightVertex);

            // 2. Bridge the gap: currentEdge.rightVertex → nextEdge.leftVertex
            float gapDist = Vector3.Distance(currentEdge.rightVertex, nextEdge.leftVertex);
            Vector3 midpoint = Vector3.Lerp(currentEdge.rightVertex, nextEdge.leftVertex, 0.5f);

            // Dynamic fillet: angle between outward directions determines concave vs convex
            float angle = Vector3.Angle(currentEdge.direction, nextEdge.direction);
            Vector3 controlPoint;

            if (angle < 135f)
            {
                controlPoint = Vector3.Lerp(midpoint, node.position, curveStrength);
            }
            else
            {
                Vector3 outwardDir = (midpoint - node.position).normalized;
                controlPoint = midpoint + (outwardDir * gapDist * 0.25f);
            }

            // Clamp: control point must not exceed intersection radius from center
            Vector3 centerToCP = controlPoint - node.position;
            if (centerToCP.magnitude > node.radius)
            {
                controlPoint = node.position + (centerToCP.normalized * node.radius);
            }

            // Quadratic Bezier curve samples — include interior points only
            // Endpoints are the exact junction vertices (rightVertex above, leftVertex next iteration)
            for (int step = 1; step < curveResolution; step++)
            {
                float t = step / (float)curveResolution;
                Vector3 l1 = Vector3.Lerp(currentEdge.rightVertex, controlPoint, t);
                Vector3 l2 = Vector3.Lerp(controlPoint, nextEdge.leftVertex, t);
                perimeterVertices.Add(Vector3.Lerp(l1, l2, t));
            }

            // Watertight seal: add the exact endpoint so there is zero gap
            // before the next iteration adds nextEdge.leftVertex as a cap start
            perimeterVertices.Add(nextEdge.leftVertex);
        }

        // 3. Build the triangle fan mesh
        Mesh mesh = new Mesh();
        mesh.name = "Procedural_Intersection_Hub";

        int perimCount = perimeterVertices.Count;
        Vector3[] vertices = new Vector3[perimCount + 1];
        Vector2[] uvs = new Vector2[perimCount + 1];
        int[] triangles = new int[perimCount * 3];

        // Index 0 = dead center
        vertices[0] = node.position;

        for (int i = 0; i < perimCount; i++)
        {
            vertices[i + 1] = perimeterVertices[i];

            int current = i + 1;
            int next = (i + 1) % perimCount + 1;

            triangles[i * 3 + 0] = 0;       // Center
            triangles[i * 3 + 1] = current;  // Current perimeter vertex
            triangles[i * 3 + 2] = next;     // Next perimeter vertex
        }

        // Planar projection UVs from world X/Z
        for (int i = 0; i < vertices.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x, vertices[i].z) * uvScale;
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
        Debug.Log("<color=yellow>--- REBUILD NETWORK STARTED ---</color>");

        // Pass 1: Smooth all spline control points first (dead ends + pass-throughs)
        foreach (RoadNode node in nodes)
        {
            node.SmoothSplines();

            // Reset trims for non-intersection nodes
            if (node.connectedEdges.Count <= 2)
            {
                foreach (RoadEdge edge in node.connectedEdges)
                {
                    if (edge.a == node) edge.trimStart = 0f;
                    if (edge.b == node) edge.trimEnd = 1f;
                }
                node.junctionEdges.Clear();
            }
        }

        // Pass 2: Calculate junction edges for all intersections (reads finalized control points)
        foreach (RoadNode node in nodes)
        {
            if (node.connectedEdges.Count >= 3)
            {
                node.CalculateJunctionEdges(node.radius);
            }
        }

        // Pass 3 & 4: Build physical road meshes then intersection hub meshes
        BuildPhysicalRoads();
    }
}