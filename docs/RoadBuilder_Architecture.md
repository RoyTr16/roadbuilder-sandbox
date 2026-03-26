# Procedural Road Builder: Sandbox Architecture

## The Goal
To build a standalone, math-driven procedural road generator from scratch. This system will serve as the proven, fully understood foundation before integrating external data (OpenStreetMap) or generating a full city like Kefar Sava.

## Core Philosophy
1. **Understand the Math:** No black-box mesh generators. We write the vertex and triangle arrays ourselves.
2. **Splines for Data, Code for Geometry:** We use Unity's Spline Package strictly to draw the invisible paths. We use custom C# scripts to generate the physical asphalt.
3. **Procedural Intersections:** We will mathematically bridge the gaps between roads to allow for complex, real-world junction shapes (e.g., *כיכר המשקפיים*).

---

## Phase 1: The Spline Foundation
**Objective:** Establish the invisible mathematical skeleton and prove we can extract data from it.
* **Action:** Install the Unity Splines package and draw a manual curve in the Editor.
* **Implementation:** Write a basic script that samples the spline at evenly spaced intervals and retrieves the exact `Vector3` position and `Vector3` forward tangent.
* **Success Criteria:** We can draw a curve in the Editor, and our script can plot debug spheres evenly along that path.

## Phase 2: The Mesh Extrusion (Sebastian Lague Method)
**Objective:** Convert the invisible spline data into a physical, 3D asphalt mesh.
* **Action:** Implement pure procedural generation using vertices and triangles.
* **Implementation:** * Walk along the sampled points from Phase 1.
    * Calculate the perpendicular `Left` and `Right` vectors for each point based on a `roadWidth` variable.
    * Plot the vertices (`Vector3[]`).
    * Define the clockwise draw order for the triangles (`int[]`).
* **Success Criteria:** A visible, continuous gray mesh is generated along the spline that updates in real-time as the spline is edited.

## Phase 3: True-Scale UV Mapping
**Objective:** Apply an asphalt material without the texture stretching or squashing on longer/shorter roads.
* **Action:** Map 2D texture coordinates (`Vector2[] UVs`) to the 3D vertices.
* **Implementation:** * Track the `accumulatedDistance` as the script walks down the spline.
    * Set the forward `V` coordinate of the UV mapping by dividing the accumulated distance by the real-world scale of the texture.
* **Success Criteria:** A tiled asphalt texture looks perfectly scaled whether the road is 10 meters long or 100 meters long.

## Phase 4: Procedural Intersections (GameDevGuide Method)
**Objective:** Seamlessly connect two independent road splines.
* **Action:** Detect where roads meet and generate a custom bridge mesh.
* **Implementation:**
    * Leave a gap between the ends of the intersecting roads.
    * Calculate the center point of the junction.
    * Retrieve the start/end vertices and tangents of the connecting roads.
    * Generate a new procedural mesh that bridges the gaps with smooth, Bezier-curved edges.
* **Success Criteria:** Two separate spline roads can merge into a T-Junction or Roundabout seamlessly, accommodating irregular, non-90-degree angles.

---
**Rule of Engagement:** We do not move to the next Phase until the current Phase is visually verified and mathematically understood.