using UnityEngine;

namespace LenniUhr.Grass
{
    public struct MeshTriangle
    {
        public MeshVertex a;
        public MeshVertex b;
        public MeshVertex c;

        public MeshTriangle(MeshVertex a, MeshVertex b, MeshVertex c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }

        public MeshVertex[] Vertices()
        {
            return new MeshVertex[3] { a, b, c };
        }
    }

    public struct MeshVertex
    {
        public Vector3 position;
        public Vector3 normal;

        public MeshVertex(Vector3 position, Vector3 normal)
        {
            this.position = position;
            this.normal = normal;
        }
    }
}


