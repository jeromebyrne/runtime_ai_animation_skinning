using System.Collections.Generic;
using UnityEngine;

public class MeshAnimator : MonoBehaviour
{
    private AnimDataImporter m_animDataImporter = new AnimDataImporter();

    private int m_currentFrame = 0;
    private float frameRate = 1/2.0f;
    private float lastFrameChange = 0.0f;

    [SerializeField]
    private GameObject joinVisualPrefab = null;
    private List<GameObject> jointVisuals = new List<GameObject>();
    
    void Start()
    {
        m_animDataImporter.Import("unpickled.json");

        bool isAnimatedMesh = true;
        
        Mesh mesh = CreateMesh(isAnimatedMesh);
        
        if (isAnimatedMesh)
        {
            var bones = CreateSkeleton(m_animDataImporter.AnimData.joints_names, 
                                                m_animDataImporter.AnimData.joints_parents,
                                                m_animDataImporter.AnimData.joints,
                                                m_animDataImporter.AnimData.a_pose_joints);

            SetupBoneWeightsAndPoses(mesh, bones, gameObject.GetComponent<SkinnedMeshRenderer>());
        }
    }
    
    Mesh CreateMesh(bool isSkinnedMesh)
    {
        Mesh mesh = new Mesh();

        if (!isSkinnedMesh)
        {
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;
        
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer == null) {
                renderer = gameObject.AddComponent<MeshRenderer>();
            }
            renderer.material = Resources.Load<Material>("barbarian_mat");
        }
        else
        {
            SkinnedMeshRenderer skinnedRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedRenderer == null) {
                skinnedRenderer = gameObject.AddComponent<SkinnedMeshRenderer>();
            }
            skinnedRenderer.material = Resources.Load<Material>("barbarian_mat");

            skinnedRenderer.sharedMesh = mesh;
        }
        
        var vertData = m_animDataImporter.AnimData.vertices_uv;
    
        // reformat the vertices from x,y to x,y,z to satisfy Unity Mesh
        Vector3[] vertices = new Vector3[vertData.Count];

        for (int i = 0; i < vertData.Count; ++i)
        {
            Vector3 v = new Vector3(vertData[i][1],vertData[i][0], 1.0f);
            vertices[i] = v;
        }
        
        // flatten the triangle data to satisfy unity Mesh
        int[] triangles = new int[m_animDataImporter.AnimData.triangles.Count * 3];

        int currentTriangleIndex = 0;

        foreach (var t in m_animDataImporter.AnimData.triangles)
        {
            for (int i = 0; i < 3; ++i)
            {
                triangles[currentTriangleIndex] = t[i];
                currentTriangleIndex++;
            }
        }
        
        // reformat the UVs into Vector2 to satisfy Unity Mesh
        Vector2[] uvs = new Vector2[vertData.Count];    
        float uvXScale = 1.15f; // hack workaround
        for (int i = 0; i < vertData.Count; ++i)
        {
            // bug: in the data uv is actually vu
            Vector2 uv = new Vector2(vertData[i][1] * uvXScale,vertData[i][0]);
            uvs[i] = uv;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    Transform[] CreateSkeleton(string[] jointNames, 
                                int[] jointParents, 
                                List <List <float[] >> joints,
                                List <float[] > a_poseJoints)
    {
        Transform[] bones = new Transform[jointNames.Length];

        for (int i = 0; i < jointNames.Length; i++) 
        {
            GameObject bone = new GameObject(jointNames[i]);  // Create a bone as GameObject
            
            // set initial pose
            bone.transform.position = new Vector3(a_poseJoints[i][0], a_poseJoints[i][1], 0.0f); // Adjust accordingly
            
            bones[i] = bone.transform;  // Store the Transform component
            if (jointParents[i] != -1) 
            {  // Assuming -1 indicates no parent
                bones[i].parent = bones[jointParents[i]];  // Set parent using Transform components
            }

            // create debug joint image
            GameObject jointVisual = Instantiate(joinVisualPrefab, bone.transform.position, Quaternion.identity);
            jointVisual.transform.position = new Vector3(a_poseJoints[i][0], a_poseJoints[i][1], 0.0f);
            DebugJointVisualizer djv = jointVisual.GetComponent<DebugJointVisualizer>();
            djv.m_SpriteRenderer.color = Color.green;
            djv.m_text.text = jointNames[i];
        }

        return bones;
    }
    
    void SetupBoneWeightsAndPoses(Mesh mesh, Transform[] bones, SkinnedMeshRenderer renderer) 
    {
        // Bind poses are necessary for the bones relative to the mesh
        Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++) {
            bindPoses[i] = bones[i].worldToLocalMatrix * renderer.transform.localToWorldMatrix;
        }
        
        mesh.bindposes = bindPoses;
        
        renderer.bones = bones;  
        renderer.rootBone = bones[0];
        
        // make the MeshAnimation the parent for easy viewing in Editor
        renderer.rootBone.parent = gameObject.transform;

        BoneWeightCalculator bwCalc = gameObject.AddComponent<BoneWeightCalculator>();
        bwCalc.AssignBoneWeights(renderer);
    }
    
    BoneWeight CreateRandomBoneWeight(int numberOfBones)
    {
        int numberOfInfluences = 1;
        
        BoneWeight weight = new BoneWeight();
        
        // Generate random indices and weights
        int[] indices = new int[numberOfInfluences];
        float[] weights = new float[numberOfInfluences];
        float totalWeight = 0;

        for (int i = 0; i < numberOfInfluences; i++)
        {
            indices[i] = Random.Range(0, numberOfBones);
            weights[i] = Random.value;
            totalWeight += weights[i];
        }

        // Normalize weights
        for (int i = 0; i < numberOfInfluences; i++)
        {
            weights[i] /= totalWeight;
        }

        // Assign normalized weights and indices
        weight.boneIndex0 = indices[0]; weight.weight0 = weights[0];
        if (numberOfInfluences > 1) { weight.boneIndex1 = indices[1]; weight.weight1 = weights[1]; }
        if (numberOfInfluences > 2) { weight.boneIndex2 = indices[2]; weight.weight2 = weights[2]; }
        if (numberOfInfluences > 3) { weight.boneIndex3 = indices[3]; weight.weight3 = weights[3]; }

        return weight;
    }

    void Animate()
    {
        lastFrameChange -= Time.deltaTime;

        int numFrames = m_animDataImporter.AnimData.joints.Count;

        if (lastFrameChange <= 0.0f)
        {
            m_currentFrame++;
            if (m_currentFrame == numFrames)
            {
                m_currentFrame = 0;
            }
            
            lastFrameChange = frameRate;
        }

        var frameJoints = m_animDataImporter.AnimData.joints[m_currentFrame];

        SkinnedMeshRenderer renderer = gameObject.GetComponent<SkinnedMeshRenderer>();
        if (frameJoints.Count != renderer.bones.Length)
        {
            return;
        }

        for (int i = 0; i < renderer.bones.Length && i < frameJoints.Count; i++)
        {
            renderer.bones[i].position = new Vector3(frameJoints[i][0], frameJoints[i][1], 0);
            // renderer.bones[i].localScale = new Vector3(5.0f, 5.0f, 1.0f);
        }
    }
    
    void Update()
    {
        Animate();

        DrawTriangles();

        // LogBoneWeights(GetComponent<SkinnedMeshRenderer>());
    }
    
    void OnDrawGizmos()
    {
        /*
        SkinnedMeshRenderer renderer = gameObject.GetComponent<SkinnedMeshRenderer>();
        if (renderer == null)
        {
            return;
        }
        
        Gizmos.color = Color.red;

        foreach (Transform bone in renderer.bones)
        {
            Gizmos.DrawSphere(bone.position, 0.01f);
        }
        */
    }

    void DrawTriangles()
    {
        float duration = 0.01f;
        Color lineColor = Color.green;

        float xOffset = 1.5f;
        
        var skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        if (skinnedMeshRenderer == null) return;
        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        if (mesh == null) return;

        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        Transform transform = skinnedMeshRenderer.transform;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 p1 = transform.TransformPoint(vertices[triangles[i]]);
            Vector3 p2 = transform.TransformPoint(vertices[triangles[i + 1]]);
            Vector3 p3 = transform.TransformPoint(vertices[triangles[i + 2]]);

            p1.x += xOffset;
            p2.x += xOffset;
            p3.x += xOffset;

            Debug.DrawLine(p1, p2, lineColor, duration);
            Debug.DrawLine(p2, p3, lineColor, duration);
            Debug.DrawLine(p3, p1, lineColor, duration);
        }
    }
    
    void LogBoneWeights(SkinnedMeshRenderer renderer)
    {
        Mesh mesh = renderer.sharedMesh;
        BoneWeight[] weights = mesh.boneWeights;

        for (int i = 0; i < weights.Length; i++)
        {
            Debug.Log("Vertex " + i + " weights: " + weights[i].weight0 + ", " + weights[i].weight1);
        }
    }
}
