using UnityEngine;

public class BoneWeightCalculator : MonoBehaviour
{
    public float influenceRadius = 1.06f;  

    public void AssignBoneWeights(SkinnedMeshRenderer skinnedMeshRenderer)
    {
        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Transform[] bones = skinnedMeshRenderer.bones;
        BoneWeight[] boneWeights = new BoneWeight[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            boneWeights[i] = CalculateBoneWeight(vertices[i], bones);
        }

        mesh.boneWeights = boneWeights;
    }

    BoneWeight CalculateBoneWeight(Vector3 vertex, Transform[] bones)
    {
        float[] weights = new float[bones.Length];
        float totalWeight = 0;

        float largestDistance = 0.0f;
        float smallestDistance = 999999.0f;

        // Calculate weights based on inverse distance, scaled by influenceRadius
        for (int i = 0; i < bones.Length; i++)
        {
            Vector3 bonePos = bones[i].position;
            // flippedBoneYPos.y *= -1;
            
            float dist = Vector3.Distance(vertex, bonePos);

            if (dist < smallestDistance)
            {
                smallestDistance = dist;
            }

            if (dist > largestDistance)
            {
                largestDistance = dist;
            }

            if (dist < influenceRadius)
            {
                // Scale weight by distance within the influence radius
                weights[i] = Mathf.Lerp(1.0f, 0.0f, dist / influenceRadius);  // Linear decrease of influence
                totalWeight += weights[i];
            }
        }

        // Normalize weights and select top 4
        BoneWeight boneWeight = new BoneWeight();
        int[] indices = new int[4];
        float[] topWeights = new float[4];

        for (int i = 0; i < bones.Length; i++)
        {
            weights[i] /= totalWeight;  // Normalize
            TryInsertWeight(ref indices, ref topWeights, i, weights[i]);
        }

        boneWeight.boneIndex0 = indices[0]; boneWeight.weight0 = topWeights[0];
        boneWeight.boneIndex1 = indices[1]; boneWeight.weight1 = topWeights[1];
        boneWeight.boneIndex2 = indices[2]; boneWeight.weight2 = topWeights[2];
        boneWeight.boneIndex3 = indices[3]; boneWeight.weight3 = topWeights[3];

        return boneWeight;
    }

    // Helper method to maintain top 4 weights
    void TryInsertWeight(ref int[] indices, ref float[] topWeights, int index, float weight)
    {
        for (int i = 0; i < 4; i++)
        {
            if (weight > topWeights[i])
            {
                for (int j = 3; j > i; j--)
                {
                    topWeights[j] = topWeights[j - 1];
                    indices[j] = indices[j - 1];
                }
                topWeights[i] = weight;
                indices[i] = index;
                break;
            }
        }
    }
}