using UnityEngine;
using System.Collections.Generic;

public class BoneWeightCalculator : MonoBehaviour
{
    public float influenceRadius = 10.0f;

    public void AssignBoneWeights(SkinnedMeshRenderer skinnedMeshRenderer)
    {
        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Transform[] bones = skinnedMeshRenderer.bones;
        BoneWeight[] boneWeights = new BoneWeight[vertices.Length];

        Dictionary<int, List<int>> vertexNeighbors = BuildVertexAdjacencyList(vertices.Length, triangles);

        for (int i = 0; i < vertices.Length; i++)
        {
            boneWeights[i] = CalculateBoneWeight(vertices[i], vertices, bones, vertexNeighbors, i);
        }

        mesh.boneWeights = boneWeights;
    }

    Dictionary<int, List<int>> BuildVertexAdjacencyList(int vertexCount, int[] triangles)
    {
        Dictionary<int, List<int>> adjacencyList = new Dictionary<int, List<int>>();
        for (int i = 0; i < vertexCount; i++)
            adjacencyList[i] = new List<int>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v1 = triangles[i];
            int v2 = triangles[i + 1];
            int v3 = triangles[i + 2];

            if (!adjacencyList[v1].Contains(v2)) adjacencyList[v1].Add(v2);
            if (!adjacencyList[v2].Contains(v1)) adjacencyList[v2].Add(v1);

            if (!adjacencyList[v2].Contains(v3)) adjacencyList[v2].Add(v3);
            if (!adjacencyList[v3].Contains(v2)) adjacencyList[v3].Add(v2);

            if (!adjacencyList[v3].Contains(v1)) adjacencyList[v3].Add(v1);
            if (!adjacencyList[v1].Contains(v3)) adjacencyList[v1].Add(v3);
        }

        return adjacencyList;
    }

    BoneWeight CalculateBoneWeight(Vector3 vertex, Vector3[] vertices, Transform[] bones, Dictionary<int, List<int>> vertexNeighbors, int vertexIndex)
    {
        float[] weights = new float[bones.Length];
        float totalWeight = 0;

        for (int b = 0; b < bones.Length; b++)
        {
            Dictionary<int, float> vertexDistances = new Dictionary<int, float>();
            for (int i = 0; i < vertices.Length; i++)
                vertexDistances[i] = float.MaxValue;

            int closestVertexIndex = FindClosestVertex(bones[b].position, vertices);
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(closestVertexIndex);
            vertexDistances[closestVertexIndex] = 0;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int neighbor in vertexNeighbors[current])
                {
                    float edgeDistance = Vector3.Distance(
                        new Vector3(vertices[current].x, vertices[current].y, 0.0f),
                        new Vector3(vertices[neighbor].x, vertices[neighbor].y, 0.0f)
                    ) + vertexDistances[current];
                    if (edgeDistance < vertexDistances[neighbor] && edgeDistance < influenceRadius)
                    {
                        vertexDistances[neighbor] = edgeDistance;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            if (vertexDistances[vertexIndex] < influenceRadius)
            {
                float weight = Mathf.Lerp(1.0f, 0.0f, vertexDistances[vertexIndex] / influenceRadius);
                weights[b] = weight;
                totalWeight += weight;
            }
        }

        BoneWeight boneWeight = new BoneWeight();
        if (totalWeight > 0)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] /= totalWeight; // Normalize
            }
        }

        List<KeyValuePair<int, float>> weightList = new List<KeyValuePair<int, float>>();
        for (int i = 0; i < weights.Length; i++)
        {
            weightList.Add(new KeyValuePair<int, float>(i, weights[i]));
        }

        weightList.Sort((a, b) => b.Value.CompareTo(a.Value)); // Sort in descending order

        boneWeight.boneIndex0 = weightList[0].Key;
        boneWeight.weight0 = Mathf.Max(weightList[0].Value, 0.01f); // Ensure a minimum weight to avoid zero
        boneWeight.boneIndex1 = weightList[1].Key;
        boneWeight.weight1 = Mathf.Max(weightList[1].Value, 0.01f) * 0.5f; // Ensure a minimum weight to avoid zero
        boneWeight.boneIndex2 = weightList[2].Key;
        boneWeight.weight2 = weightList[2].Value * 0.25f;
        boneWeight.boneIndex3 = weightList[3].Key;
        boneWeight.weight3 = weightList[3].Value * 0.075f;

        return boneWeight;
    }

    int FindClosestVertex(Vector3 bonePosition, Vector3[] vertices)
    {
        float minDistance = float.MaxValue;
        int closestVertexIndex = -1;

        // Project bonePosition onto a 2D plane
        Vector3 bonePosition2D = new Vector3(bonePosition.x, bonePosition.y, 0.0f);

        for (int i = 0; i < vertices.Length; i++)
        {
            // Project each vertex onto a 2D plane
            Vector3 vertex2D = new Vector3(vertices[i].x, vertices[i].y, 0.0f);
            float dist = Vector3.Distance(bonePosition2D, vertex2D);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestVertexIndex = i;
            }
        }

        return closestVertexIndex;
    }
}
