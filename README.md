# Tech Demos

This repository contains snippets of more technical game development projects within the Unity Engine.

## Terrain Generator

This terrain generator generates terrain meshes and colliders with a Marching Cubes algorithm and density functions (layered 3D noises). The density can be placed around the world with DensityBox objects. In a chunk-based algorithm, the density at each point in the world is calculated by considering all overlapping DensityBoxes. Then a Marching Cubes algorithm is calculated on top of it.
The most important files:
1. **TerrainGenerator.cs**: Starts the terrain generation process. Based on all DensityBox objects, this class creates all overlapping terrain chunks and generates the meshes by executing different comnpute shaders.
2. **TerrainChunk.cs**: Contain the terrain mesh objects for rendering and collision. Can be automatically updated when DensityBoxes in the world are changed.
3. **DensityBox.cs**: These objects determine where density is placed in the world. Different DensityBox classes exist for different layered noise density functions.

<img src="https://github.com/lenniuhr/TechDemos/blob/main/Images/TerrainGeneratorGizmos.png" width=49.6% height=49.6%> <img src="https://github.com/lenniuhr/TechDemos/blob/main/Images/TerrainGenerator.png" width=49.6% height=49.6%>

## Grass Generator

This grass generator generates hundreds of thousands of grass blades on Unity Terrain objects and mesh objects within a selected layer. The algorithm has the following parts:
1. In the editor, a grass map (similar to a terrain splatmap) is calculated and determines the density of grass on the terrain. This can be extended to add hand painted grass maps to control where grass is growing.
2. In the editor, an algorithm analyses the topology of the mesh objects, on which grass is spawned. It creates a list of evenly sized triangles.
3. In runtime, grass chunks near the player are activated. At activation, seeding points for the grass are calculated on the terrain and the grass triangles.
4. At every frame, grass blades are spawned with a proper wind animation and displayed via RenderPrimitives.

The most important files:
1. **GrassMap.cs**: The component can be added to any Unity terrain object to enable grass spawning. It also generates a placement mesh for mesh objects within a selected layer, that lie within the bounds of the terrain.
2. **GrassGenerator.cs**: Based on the information from the GrassMaps, the generator executes compute shaders to spawn grass on the Unity Terrain and on placement meshes. This is done chunk-based and around a given target, e.g. the player character.
3. **GrassChunk.cs**: The GrassChunks are objects that are managed by the GrassGenerator and are used to create and render grass blades each frame. 

<img src="https://github.com/lenniuhr/TechDemos/blob/main/Images/GrassGeneratorGizmos.png" width=49.6% height=49.6%> <img src="https://github.com/lenniuhr/TechDemos/blob/main/Images/GrassGenerator.png" width=49.6% height=49.6%>
