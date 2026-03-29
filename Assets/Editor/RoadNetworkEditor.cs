using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RoadNetwork))]
public class RoadNetworkEditor : Editor
{
    RoadNetwork network;
    RoadNode selectedNode = null;
    bool isDrawingMode = false;

    void OnEnable()
    {
        network = (RoadNetwork)target;
    }

    // This redesigns the Inspector window for your Road Network
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(15);

        // The Big Draw Button
        GUI.backgroundColor = isDrawingMode ? Color.red : Color.green;
        if (GUILayout.Button(isDrawingMode ? "STOP DRAWING" : "START DRAWING ROADS", GUILayout.Height(40)))
        {
            isDrawingMode = !isDrawingMode;
            selectedNode = null;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(5);

        // The Nuke Button
        if (GUILayout.Button("Clear Network"))
        {
            network.nodes.Clear();
            network.edges.Clear();
            network.RebuildNetwork();
            selectedNode = null;
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("DEBUG: DUMP STATE TO CONSOLE", GUILayout.Height(30)))
        {
            network.DumpNetworkState();
        }
        GUI.backgroundColor = Color.white;
    }

    // This runs constantly in the Scene View
    void OnSceneGUI()
    {
        // --- INTERACTIVE BEZIER CONTROL POINT HANDLES (always active) ---
        foreach (RoadNode node in network.nodes)
        {
            if (node.connectedEdges.Count < 3) continue;

            foreach (RoadEdge edge in node.connectedEdges)
            {
                bool isNodeA = (edge.a == node);
                Vector3 currentCP = isNodeA ? edge.controlPoint1 : edge.controlPoint2;

                float handleSize = HandleUtility.GetHandleSize(currentCP) * 0.15f;
                Handles.color = Color.magenta;

                EditorGUI.BeginChangeCheck();
                Vector3 newCP = Handles.FreeMoveHandle(currentCP, handleSize, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(network, "Move Control Point");
                    if (isNodeA) edge.controlPoint1 = newCP;
                    else edge.controlPoint2 = newCP;
                    network.RebuildNetwork();
                }
            }
        }

        if (!isDrawingMode) return;

        Event e = Event.current;

        // Hijack the mouse so we don't accidentally click other GameObjects
        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlID);

        // Shoot a raycast from the mouse to the flat ground plane (Y = 0)
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 mousePos = ray.GetPoint(enter);

            // Draw a helpful dotted line if we are dragging a road
            if (selectedNode != null)
            {
                Handles.color = Color.yellow;
                Handles.DrawDottedLine(selectedNode.position, mousePos, 4f);
            }

            // --- LEFT CLICK: PLACE NODE ---
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                // 1. Magnetic Snapping: Check if we clicked near an existing node
                RoadNode clickedNode = null;
                float snapRadius = 3f; // How aggressive the magnet is

                foreach (RoadNode n in network.nodes)
                {
                    // If the mouse is close to a node, snap to it!
                    if (Vector3.Distance(mousePos, n.position) < snapRadius)
                    {
                        clickedNode = n;
                        break;
                    }
                }

                // 2. If we didn't snap to anything, create a brand new node
                if (clickedNode == null)
                {
                    clickedNode = new RoadNode(mousePos, 5f);
                    network.nodes.Add(clickedNode);
                }

                // 3. Connect the roads
                if (selectedNode != null && selectedNode != clickedNode)
                {
                    RoadEdge newEdge = new RoadEdge(selectedNode, clickedNode, 6f);

                    newEdge.controlPoint1 = Vector3.Lerp(selectedNode.position, clickedNode.position, 0.33f);
                    newEdge.controlPoint2 = Vector3.Lerp(selectedNode.position, clickedNode.position, 0.66f);

                    network.edges.Add(newEdge);
                }

                // The node we just clicked/created becomes the active pencil tip
                selectedNode = clickedNode;
                network.RebuildNetwork();
                e.Use();
            }

            // --- RIGHT CLICK: DESELECT ---
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                selectedNode = null; // Drops the pencil
                e.Use();
            }
        }

        // Force the Scene View to update so the dotted line follows the mouse
        SceneView.RepaintAll();
    }
}