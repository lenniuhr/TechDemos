# Tech Demos

This repository contains snippets of more technical game development projects within the Unity Engine.

## Terrain Generator

This terrain generator generates terrain meshes and colliders with a Marching Cubes algorithm and density functions (layered 3D noises). The density can be placed around the world with DensityBox objects. In a chunk-based algorithm, the density at each point in the world is calculated by considering all overlapping DensityBoxes. Then a Marching Cubes algorithm is calculated on top of it.

<img src="https://github.com/lenniuhr/TechDemos/blob/main/Images/TerrainGeneratorGizmos.png" width=49.6% height=49.6%> <img src="https://github.com/lenniuhr/TechDemos/blob/main/Images/TerrainGenerator.png" width=49.6% height=49.6%>

## Grass Generator

This grass generator generates hundreds of thousands of grass blades on Unity Terrain objects and mesh objects within a selected layer. The algorithm has the following parts:
1. In the editor, a grass map (similar to a terrain splatmap) is calculated and determines the density of grass on the terrain. This can be extended to add hand painted grass maps to control where grass is growing.
2. In the editor, an algorithm analyses the topology of the mesh objects, on which grass is spawned. It creates a list of evenly sized triangles.
3. In runtime, grass chunks near the player are activated. At activation, seeding points for the grass are calculated on the terrain and the grass triangles.
4. At every frame, grass blades are spawned with a proper wind animation and displayed via RenderPrimitives.

<img src="https://github.com/lenniuhr/TechDemos/blob/main/Images/GrassGeneratorGizmos.png" width=49.6% height=49.6%> <img src="https://github.com/lenniuhr/TechDemos/blob/main/Images/GrassGenerator.png" width=49.6% height=49.6%>
