using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace LenniUhr.Grass
{
    public class MeshUtils
    {
        public static void SubdivideTriangles(ref List<MeshTriangle> tris, float targetArea)
        {
            List<MeshTriangle> subdividedVertices = new List<MeshTriangle>();

            foreach (MeshTriangle triangle in tris)
            {
                MeshVertex a = triangle.a;
                MeshVertex b = triangle.b;
                MeshVertex c = triangle.c;

                float area = Vector3.Magnitude(Vector3.Cross(b.position - a.position, c.position - a.position)) * 0.5f;
                float ratio = area / targetArea;

                if (ratio < 1.5f)
                {
                    subdividedVertices.Add(new MeshTriangle(a, b, c));
                }
                else
                {
                    float edgeAB = Vector3.Distance(a.position, b.position);
                    float edgeBC = Vector3.Distance(b.position, c.position);
                    float edgeCA = Vector3.Distance(c.position, a.position);

                    if (edgeAB > edgeBC && edgeAB > edgeCA)
                    {
                        // D between A and B -> CAD, CDB
                        MeshVertex d = new MeshVertex((a.position + b.position) * 0.5f, Utils.InterpolateNormal(a.normal, b.normal, 0.5f));
                        subdividedVertices.Add(new MeshTriangle(c, a, d));
                        subdividedVertices.Add(new MeshTriangle(c, d, b));
                    }
                    else if (edgeBC > edgeCA)
                    {
                        // D between B and C -> ABD, ADC
                        MeshVertex d = new MeshVertex((b.position + c.position) * 0.5f, Utils.InterpolateNormal(b.normal, c.normal, 0.5f));
                        subdividedVertices.Add(new MeshTriangle(a, b, d));
                        subdividedVertices.Add(new MeshTriangle(a, d, c));
                    }
                    else
                    {
                        // D between C and A -> BCD, BDA
                        MeshVertex d = new MeshVertex((c.position + a.position) * 0.5f, Utils.InterpolateNormal(c.normal, a.normal, 0.5f));
                        subdividedVertices.Add(new MeshTriangle(b, c, d));
                        subdividedVertices.Add(new MeshTriangle(b, d, a));
                    }
                }
            }
            tris = subdividedVertices;
        }

        public static void ClampNormals(ref List<MeshTriangle> tris, float maxAngle)
        {
            List<MeshTriangle> filteredTris = new List<MeshTriangle>();

            foreach (MeshTriangle triangle in tris)
            {
                MeshVertex a = triangle.a;
                MeshVertex b = triangle.b;
                MeshVertex c = triangle.c;

                float angleA = Vector3.Angle(a.normal, Vector3.up);
                float angleB = Vector3.Angle(b.normal, Vector3.up);
                float angleC = Vector3.Angle(c.normal, Vector3.up);

                int steep = 0;
                if (angleA > maxAngle)
                    steep++;
                if (angleB > maxAngle)
                    steep++;
                if (angleC > maxAngle)
                    steep++;

                if (steep == 0)
                {
                    filteredTris.Add(triangle);
                }
                else if (steep == 1)
                {
                    MeshVertex vertA;
                    MeshVertex vertB;
                    MeshVertex vertInC;
                    if (angleA > maxAngle)
                    {
                        vertA = b;
                        vertB = c;
                        vertInC = a;
                    }
                    else if (angleB > maxAngle)
                    {
                        vertA = c;
                        vertB = a;
                        vertInC = b;
                    }
                    else
                    {
                        vertA = a;
                        vertB = b;
                        vertInC = c;
                    }

                    angleA = Vector3.Angle(vertA.normal, Vector3.up);
                    angleB = Vector3.Angle(vertB.normal, Vector3.up);
                    angleC = Vector3.Angle(vertInC.normal, Vector3.up);

                    float lerpAC = Mathf.InverseLerp(angleA, angleC, maxAngle);
                    MeshVertex vertAC = new MeshVertex(Vector3.Lerp(vertA.position, vertInC.position, lerpAC), Utils.InterpolateNormal(vertA.normal, vertInC.normal, lerpAC));

                    float lerpBC = Mathf.InverseLerp(angleB, angleC, maxAngle);
                    MeshVertex vertBC = new MeshVertex(Vector3.Lerp(vertB.position, vertInC.position, lerpBC), Utils.InterpolateNormal(vertB.normal, vertInC.normal, lerpBC));

                    MeshTriangle triA;
                    MeshTriangle triB;
                    GetBestTriangles(vertA, vertB, vertAC, vertBC, out triA, out triB);

                    filteredTris.Add(triA);
                    filteredTris.Add(triB);
                }
                else if (steep == 2)
                {
                    MeshVertex vertA;
                    MeshVertex vertInB;
                    MeshVertex vertInC;
                    if (angleA <= maxAngle)
                    {
                        vertA = a;
                        vertInB = b;
                        vertInC = c;
                    }
                    else if (angleB <= maxAngle)
                    {
                        vertA = b;
                        vertInB = c;
                        vertInC = a;
                    }
                    else
                    {
                        vertA = c;
                        vertInB = a;
                        vertInC = b;
                    }

                    angleA = Vector3.Angle(vertA.normal, Vector3.up);
                    angleB = Vector3.Angle(vertInB.normal, Vector3.up);
                    angleC = Vector3.Angle(vertInC.normal, Vector3.up);

                    float lerpAB = Mathf.InverseLerp(angleA, angleB, maxAngle);
                    vertInB = new MeshVertex(Vector3.Lerp(vertA.position, vertInB.position, lerpAB), Utils.InterpolateNormal(vertA.normal, vertInB.normal, lerpAB));

                    float lerpAC = Mathf.InverseLerp(angleA, angleC, maxAngle);
                    vertInC = new MeshVertex(Vector3.Lerp(vertA.position, vertInC.position, lerpAC), Utils.InterpolateNormal(vertA.normal, vertInC.normal, lerpAC));

                    filteredTris.Add(new MeshTriangle(vertA, vertInB, vertInC));
                }
            }
            tris = filteredTris;
        }

        // a -> b -> bX -> aX (counter clockwise)
        public static void GetBestTriangles(MeshVertex a, MeshVertex b, MeshVertex aX, MeshVertex bX, out MeshTriangle triA, out MeshTriangle triB)
        {
            // Option a -> b -> bX and a -> bX -> aX
            float area1 = Vector3.Magnitude(Vector3.Cross(b.position - a.position, bX.position - a.position)) * 0.5f;
            float area2 = Vector3.Magnitude(Vector3.Cross(bX.position - a.position, aX.position - a.position)) * 0.5f;

            float ratioA = area1 / area2;
            ratioA = (ratioA > 1.0f) ? 1.0f / ratioA : ratioA;

            // Option a -> b -> aX and b -> bX -> aX
            area1 = Vector3.Magnitude(Vector3.Cross(aX.position - b.position, a.position - b.position)) * 0.5f;
            area2 = Vector3.Magnitude(Vector3.Cross(bX.position - b.position, aX.position - b.position)) * 0.5f;

            float ratioB = area1 / area2;
            ratioB = (ratioB > 1.0f) ? 1.0f / ratioB : ratioB;

            if (ratioA > ratioB)
            {
                triA = new MeshTriangle(a, b, bX);
                triB = new MeshTriangle(a, bX, aX);
            }
            else
            {
                triA = new MeshTriangle(a, b, aX);
                triB = new MeshTriangle(b, bX, aX);
            }
        }

        private static bool CloseToBlockBorder(Vector3 position, float minDistance)
        {
            float distanceX = math.abs(position.x % 8);
            float distanceZ = math.abs(position.z % 8);

            return distanceX < minDistance || distanceZ < minDistance;
        }

        public static void MergeEdgesByDistance(ref List<MeshTriangle> inputList, float mergeDistance = 0.2f, float mergeAngle = 3.0f)
        {
            List<Vector3> vertices;
            List<Vector3> normals;
            List<int> triangles;
            IndexTriangleList(inputList, out vertices, out normals, out triangles);

            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];

                Vector3 normalA = normals[triangles[i]];
                Vector3 normalB = normals[triangles[i + 1]];
                Vector3 normalC = normals[triangles[i + 2]];

                int mergedIndex;
                int replaceIndex;
                Vector3 mergedPosition;
                Vector3 mergedNormal;

                if (Vector3.Distance(a, b) < mergeDistance && Vector3.Angle(normalA, normalB) < mergeAngle)
                {
                    if (CloseToBlockBorder(a, mergeDistance) || CloseToBlockBorder(b, mergeDistance))
                        continue;

                    mergedPosition = (a + b) * 0.5f;
                    mergedNormal = Utils.InterpolateNormal(normalA, normalB, 0.5f);

                    mergedIndex = triangles[i];
                    replaceIndex = triangles[i + 1];
                }
                else if (Vector3.Distance(b, c) < mergeDistance && Vector3.Angle(normalB, normalC) < mergeAngle)
                {
                    if (CloseToBlockBorder(b, mergeDistance) || CloseToBlockBorder(c, mergeDistance))
                        continue;

                    mergedPosition = (b + c) * 0.5f;
                    mergedNormal = Utils.InterpolateNormal(normalB, normalC, 0.5f);

                    mergedIndex = triangles[i + 1];
                    replaceIndex = triangles[i + 2];
                }
                else if (Vector3.Distance(c, a) < mergeDistance && Vector3.Angle(normalC, normalA) < mergeAngle)
                {
                    if (CloseToBlockBorder(c, mergeDistance) || CloseToBlockBorder(a, mergeDistance))
                        continue;

                    mergedPosition = (c + a) * 0.5f;
                    mergedNormal = Utils.InterpolateNormal(normalC, normalA, 0.5f);

                    mergedIndex = triangles[i];
                    replaceIndex = triangles[i + 2];
                }
                else
                {
                    continue;
                }

                vertices[mergedIndex] = mergedPosition;
                normals[mergedIndex] = mergedNormal;

                for (int j = 0; j < triangles.Count; j++)
                {
                    if (triangles[j] == replaceIndex)
                        triangles[j] = mergedIndex;
                }
            }

            List<MeshTriangle> outputList = new List<MeshTriangle>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                if (triangles[i] == triangles[i + 1] || triangles[i] == triangles[i + 2] || triangles[i + 1] == triangles[i + 2])
                    continue;

                MeshVertex a = new MeshVertex(vertices[triangles[i]], normals[triangles[i]]);
                MeshVertex b = new MeshVertex(vertices[triangles[i + 1]], normals[triangles[i + 1]]);
                MeshVertex c = new MeshVertex(vertices[triangles[i + 2]], normals[triangles[i + 2]]);
                outputList.Add(new MeshTriangle(a, b, c));
            }

            inputList = outputList;
        }

        private void SmoothTriangles(ref List<MeshTriangle> inputList)
        {
            List<Vector3> vertices;
            List<Vector3> normals;
            List<int> triangles;
            IndexTriangleList(inputList, out vertices, out normals, out triangles);

            List<MeshTriangle> smoothVertices = new List<MeshTriangle>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                MeshVertex a = new MeshVertex(vertices[triangles[i]], normals[triangles[i]]);
                MeshVertex b = new MeshVertex(vertices[triangles[i + 1]], normals[triangles[i + 1]]);
                MeshVertex c = new MeshVertex(vertices[triangles[i + 2]], normals[triangles[i + 2]]);
                smoothVertices.Add(new MeshTriangle(a, b, c));
            }
            inputList = smoothVertices;
        }

        public static void OffsetIndexedList(ref List<int> triangles, int offset)
        {
            for(int i = 0; i < triangles.Count; i++)
            {
                triangles[i] += offset;
            }
        }

        public static void IndexTriangleList(List<MeshTriangle> inputList, out List<Vector3> vertices, out List<Vector3> normals, out List<int> triangles)
        {
            vertices = new List<Vector3>();
            normals = new List<Vector3>();
            triangles = new List<int>();

            List<int> counts = new List<int>();

            float mergeDistance = 0.01f;

            foreach (MeshTriangle inputVertex in inputList)
            {
                foreach (MeshVertex inputVert in inputVertex.Vertices())
                {
                    int existingIndex = -1;
                    for (int i = 0; i < vertices.Count; i++)
                    {
                        if (Vector3.Distance(inputVert.position, vertices[i]) < mergeDistance)
                        {
                            existingIndex = i;
                            break;
                        }
                    }

                    if (existingIndex == -1)
                    {
                        triangles.Add(vertices.Count);
                        vertices.Add(inputVert.position);
                        normals.Add(inputVert.normal);
                        counts.Add(1);
                    }
                    else
                    {
                        triangles.Add(existingIndex);

                        normals[existingIndex] = normals[existingIndex] + inputVert.normal;
                        counts[existingIndex] = counts[existingIndex] + 1;
                    }
                }
            }

            for (int i = 0; i < normals.Count; i++)
            {
                normals[i] = Vector3.Normalize(normals[i] / counts[i]);
            }
        }
    }
}