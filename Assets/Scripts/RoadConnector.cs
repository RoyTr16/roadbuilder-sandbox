using UnityEngine;
using PathCreation;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RoadConnector : MonoBehaviour
{
    [Header("The Roads")]
    public PathCreator incomingRoad;
    public PathCreator mainRoad;

    [Header("Mesh Settings")]
    public float roadWidth = 6f;
    public int resolution = 20;

    private MeshFilter meshFilter;

    void OnEnable()
    {
        // Safely grab the mesh filter and give it an empty canvas
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter.sharedMesh == null)
        {
            meshFilter.sharedMesh = new Mesh();
            meshFilter.sharedMesh.name = "Intersection Bridge";
        }
    }

    void Update()
    {
        // Safety catch
        if (incomingRoad == null || mainRoad == null) return;
        if (incomingRoad.path == null || mainRoad.path == null) return;

        // --- STEP 1: THE SMART ANCHOR ---
        // 1. Get the physical coordinates of both ends of the incoming road
        Vector3 endA = incomingRoad.path.GetPoint(0);
        Vector3 endB = incomingRoad.path.GetPoint(incomingRoad.path.NumPoints - 1);

        // 2. Find where those ends would connect to the main road
        Vector3 targetA = mainRoad.path.GetClosestPointOnPath(endA);
        Vector3 targetB = mainRoad.path.GetClosestPointOnPath(endB);

        Vector3 startPos;
        Vector3 startDir;

        // 3. Compare the distances. Whichever end is shorter is our bridge anchor!
        if (Vector3.Distance(endA, targetA) < Vector3.Distance(endB, targetB))
        {
            startPos = endA;
            // Calculate outward direction: (Current Point - Next Point)
            startDir = (endA - incomingRoad.path.GetPoint(1)).normalized;
        }
        else
        {
            startPos = endB;
            // Calculate outward direction: (Current Point - Previous Point)
            startDir = (endB - incomingRoad.path.GetPoint(incomingRoad.path.NumPoints - 2)).normalized;
        }

        // 4. Flatten the laser so it doesn't shoot underground
        startDir.y = 0;
        startDir = startDir.normalized;

        // Visual Proof
        Debug.DrawRay(startPos, startDir * 5f, Color.yellow);

        // --- STEP 2: THE TARGET VECTOR ---
        // 1. Find the exact physical 3D coordinate on the main road
        Vector3 targetPos = mainRoad.path.GetClosestPointOnPath(startPos);

        // 2. Find the "Time" (0.0 to 1.0 percentage) of that exact spot
        float targetTime = mainRoad.path.GetClosestTimeOnPath(startPos);

        // 3. Get the direction the main road is flowing at that percentage
        Vector3 targetDir = mainRoad.path.GetDirection(targetTime);

        // 4. Flatten the direction so our bridge doesn't tilt or twist
        targetDir.y = 0;
        targetDir = targetDir.normalized;

        // Visual Proof:
        // A green line to show the shortest gap between the roads
        Debug.DrawLine(startPos, targetPos, Color.green);
        // A cyan laser to show which way the traffic is flowing on the main road
        Debug.DrawRay(targetPos, targetDir * 5f, Color.cyan);

        // --- STEP 3: THE MATHEMATICAL SPINE ---

        // Tangent Strength defines how far the curve shoots out before bending.
        // A good rule of thumb is half the physical distance between the roads.
        float distance = Vector3.Distance(startPos, targetPos);
        float tangentStrength = distance * 0.5f;

        // P1: Push OUTWARD along the yellow laser
        Vector3 p1 = startPos + (startDir * tangentStrength);

        // P2: Push BACKWARD against the cyan laser so it merges smoothly
        Vector3 p2 = targetPos + (-targetDir * tangentStrength);

        // Visual Proof: Draw the spine using 20 small steps
        int segments = 20;
        Vector3 previousPoint = startPos;

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 currentPoint = CalculateBezierPoint(t, startPos, p1, p2, targetPos);

            Debug.DrawLine(previousPoint, currentPoint, Color.red);
            previousPoint = currentPoint;
        }

        // --- STEP 4: POUR THE ASPHALT ---
        GenerateBridgeMesh(startPos, p1, p2, targetPos);
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

    void GenerateBridgeMesh(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        // 1. Prepare the Data Arrays
        int vertsPerRing = 2; // A Left vertex and a Right vertex
        Vector3[] verts = new Vector3[(resolution + 1) * vertsPerRing];
        Vector2[] uvs = new Vector2[verts.Length];
        int[] tris = new int[resolution * 6]; // 6 points make a rectangle (2 triangles) per step

        int vertIndex = 0;
        int triIndex = 0;

        // 2. Walk the Line
        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;

            // Calculate where we are, and where we are going
            Vector3 worldCenterPos = CalculateBezierPoint(t, p0, p1, p2, p3);

            // Look slightly ahead to find "Forward" (or slightly behind if we are at the very end)
            float tNext = (i < resolution) ? (i + 1) / (float)resolution : (i - 1) / (float)resolution;
            Vector3 worldNextPos = CalculateBezierPoint(tNext, p0, p1, p2, p3);

            Vector3 worldForward = (i < resolution) ? (worldNextPos - worldCenterPos) : (worldCenterPos - worldNextPos);
            worldForward.y = 0; // Keep it flat

            // NaN Safety Catch: Prevent the math from imploding if the vector is perfectly zero
            if (worldForward.sqrMagnitude < 0.001f) worldForward = Vector3.forward;
            worldForward = worldForward.normalized;

            // The Cross Product to find the Left edge
            Vector3 worldLeft = Vector3.Cross(worldForward, Vector3.up).normalized;

            // --- THE CRITICAL FIX: CONVERT TO LOCAL SPACE ---
            Vector3 localCenter = transform.InverseTransformPoint(worldCenterPos);
            Vector3 localLeft = transform.InverseTransformDirection(worldLeft);

            // 3. Drop the Left and Right Vertices
            verts[vertIndex + 0] = localCenter + (localLeft * roadWidth * 0.5f); // Left Edge
            verts[vertIndex + 1] = localCenter - (localLeft * roadWidth * 0.5f); // Right Edge

            // 4. Basic Texturing UVs (Stretches the asphalt texture along the bridge)
            uvs[vertIndex + 0] = new Vector2(0, t * 2f);
            uvs[vertIndex + 1] = new Vector2(1, t * 2f);

            // 5. Connect the Dots (Draw Triangles connecting back to the previous step)
            if (i > 0)
            {
                int prevLeft = vertIndex - 2;
                int prevRight = vertIndex - 1;
                int currentLeft = vertIndex + 0;
                int currentRight = vertIndex + 1;

                // First Triangle
                tris[triIndex + 0] = prevLeft;
                tris[triIndex + 1] = currentLeft;
                tris[triIndex + 2] = prevRight;

                // Second Triangle
                tris[triIndex + 3] = prevRight;
                tris[triIndex + 4] = currentLeft;
                tris[triIndex + 5] = currentRight;

                triIndex += 6;
            }

            vertIndex += 2; // Move forward 2 spots in the array for the next loop
        }

        // 6. Push the data to the physical Mesh!
        Mesh mesh = meshFilter.sharedMesh;
        mesh.Clear();
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals(); // Fixes lighting
        mesh.RecalculateTangents();
    }
}