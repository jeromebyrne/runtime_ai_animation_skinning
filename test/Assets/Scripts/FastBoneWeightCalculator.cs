using System.Collections.Generic;
using UnityEngine;

public class FastBoneWeightCalculator : MonoBehaviour
{
    public float influenceRadius = 1.15f;

    public void AssignBoneWeights(SkinnedMeshRenderer skinnedMeshRenderer)
    {
        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Transform[] bones = skinnedMeshRenderer.bones;
        BoneWeight[] boneWeights = new BoneWeight[vertices.Length];

        Dictionary<int, List<int>> vertexNeighbors = BuildVertexAdjacencyList(vertices.Length, triangles);
        Dictionary<int, Dictionary<int, float>> distanceCache = CacheVertexDistances(vertices, bones);

        for (int i = 0; i < vertices.Length; i++)
        {
            boneWeights[i] = CalculateBoneWeight(vertices[i], vertices, bones, vertexNeighbors, distanceCache, i);
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

    Dictionary<int, Dictionary<int, float>> CacheVertexDistances(Vector3[] vertices, Transform[] bones)
    {
        Dictionary<int, Dictionary<int, float>> cache = new Dictionary<int, Dictionary<int, float>>();

        for (int b = 0; b < bones.Length; b++)
        {
            cache[b] = new Dictionary<int, float>();

            for (int v = 0; v < vertices.Length; v++)
            {
                // Project both vertices and bones onto a 2D plane by zeroing out the z-component
                Vector3 bonePosition2D = new Vector3(bones[b].position.x, bones[b].position.y, 0.0f);
                Vector3 vertex2D = new Vector3(vertices[v].x, vertices[v].y, 0.0f);

                float distance = Vector3.Distance(bonePosition2D, vertex2D);
                cache[b][v] = distance;
            }
        }

        return cache;
    }

    BoneWeight CalculateBoneWeight(Vector3 vertex, Vector3[] vertices, Transform[] bones, Dictionary<int, List<int>> vertexNeighbors, Dictionary<int, Dictionary<int, float>> distanceCache, int vertexIndex)
    {
        float[] weights = new float[bones.Length];
        float totalWeight = 0;

        for (int b = 0; b < bones.Length; b++)
        {
            float dist = distanceCache[b][vertexIndex];

            if (dist < influenceRadius)
            {
                float weight = Mathf.Lerp(1.0f, 0.0f, dist / influenceRadius);
                weights[b] = weight;
                totalWeight += weight;
            }
        }

        BoneWeight boneWeight = new BoneWeight();

        if (totalWeight > 0)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] /= totalWeight;
            }
        }

        List<KeyValuePair<int, float>> weightList = new List<KeyValuePair<int, float>>();
        for (int i = 0; i < weights.Length; i++)
        {
            weightList.Add(new KeyValuePair<int, float>(i, weights[i]));
        }

        weightList.Sort((a, b) => b.Value.CompareTo(a.Value));

        boneWeight.boneIndex0 = weightList[0].Key;
        boneWeight.weight0 = Mathf.Max(weightList[0].Value, 0.01f);
        boneWeight.boneIndex1 = weightList[1].Key;
        boneWeight.weight1 = Mathf.Max(weightList[1].Value, 0.01f) * 0.5f;
        boneWeight.boneIndex2 = weightList[2].Key;
        boneWeight.weight2 = weightList[2].Value * 0.25f;
        boneWeight.boneIndex3 = weightList[3].Key;
        boneWeight.weight3 = weightList[3].Value * 0.075f;

        return boneWeight;
    }
}
