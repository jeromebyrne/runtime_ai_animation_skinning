using System.Collections.Generic;
using UnityEngine;

public class SkeletonMeshAnimator : MonoBehaviour
{
    private AnimDataImporter m_animDataImporter = new AnimDataImporter();

    private int m_currentFrame = -1;
    private float frameRate = 1/10.0f;
    private float lastFrameChange = 0.0f;

    [SerializeField]
    private GameObject joinVisualPrefab = null;
    private List<GameObject> jointVisuals = new List<GameObject>();

    private int lockDisplayFrame = -1; // -1 to unlock and animate
    private bool applyRotations = true;
    private Vector2[] lastPositions;
    
    void Start()
    {
        m_animDataImporter.Import("unpickled.json");

        Mesh mesh = CreateMesh();
        
        var bones = CreateSkeleton(m_animDataImporter.AnimData.joints_names, 
                                            m_animDataImporter.AnimData.joints_parents,
                                            m_animDataImporter.AnimData.joints,
                                            m_animDataImporter.AnimData.a_pose_joints);
        
        SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
        SetupBoneWeightsAndPoses(mesh, bones, skinnedMeshRenderer);
    }
    
    Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();
        
        SkinnedMeshRenderer skinnedRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
        if (skinnedRenderer == null) {
            skinnedRenderer = gameObject.AddComponent<SkinnedMeshRenderer>();
        }
        skinnedRenderer.material = Resources.Load<Material>("barbarian_mat");

        skinnedRenderer.sharedMesh = mesh;
        
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
        
        lastPositions = new Vector2[jointNames.Length];
        
        for (int i = 0; i < jointNames.Length; i++) 
        {
            GameObject bone = new GameObject(jointNames[i]); 
            bone.transform.localPosition = new Vector3(a_poseJoints[i][0], a_poseJoints[i][1], 0.0f);  // Local position set

            bones[i] = bone.transform;  
            if (jointParents[i] != -1) 
            {  
                bones[i].parent = bones[jointParents[i]]; 
            }
            // Store initial local position
            lastPositions[i] = bones[i].localPosition; 

            // create debug joint image
            GameObject jointVisual = Instantiate(joinVisualPrefab, bone.transform.position, Quaternion.identity);
            jointVisual.transform.position = new Vector3(a_poseJoints[i][0], a_poseJoints[i][1], 0.0f);
            DebugJointVisualizer djv = jointVisual.GetComponent<DebugJointVisualizer>();
            djv.m_SpriteRenderer.color = Color.green;
            djv.m_text.text = jointNames[i];
            
            jointVisuals.Add(jointVisual);
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

    void Animate()
    {
        lastFrameChange -= Time.deltaTime;

        int numFrames = m_animDataImporter.AnimData.joints.Count;

        int nextFrame = m_currentFrame;
        if (lastFrameChange <= 0.0f)
        {
            nextFrame++;
            if (nextFrame >= numFrames)
            {
                nextFrame = 0;
            }
            
            lastFrameChange = frameRate;
        }

        SetDisplayFrame(nextFrame);
    }

    void SetDisplayFrame(int frame)
    {
        if (m_currentFrame == frame)
        {
            return;
        }

        m_currentFrame = frame;
        
        var frameJoints = m_animDataImporter.AnimData.joints[m_currentFrame];

        SkinnedMeshRenderer renderer = gameObject.GetComponent<SkinnedMeshRenderer>();
        
        for (int i = 0; i < renderer.bones.Length && i < frameJoints.Count; i++)
        {
            Vector3 newPosition = new Vector3(frameJoints[i][0], frameJoints[i][1], 0);
            renderer.bones[i].position = newPosition;
        }
    }

    void ApplyJointRotations()
    {
        if (!applyRotations)
        {
            return;
        }
    
        var frameJoints = m_animDataImporter.AnimData.joints[m_currentFrame];
        SkinnedMeshRenderer renderer = gameObject.GetComponent<SkinnedMeshRenderer>();

        // Process each bone according to the hierarchy defined by joints_parents
        for (int i = 0; i < renderer.bones.Length; i++)
        {
            if (renderer.bones[i] == null || i >= frameJoints.Count)
                continue;

            int parentIndex = m_animDataImporter.AnimData.joints_parents[i];
            Transform parentBone = (parentIndex != -1 && parentIndex < renderer.bones.Length) ? renderer.bones[parentIndex] : null;

            if (parentBone != null)
            {
                ApplyRotation(renderer.bones[i], parentBone, i, parentIndex);
            }
        }
    }
    
    void ApplyRotation(Transform bone, Transform parentBone, int index, int parentIndex)
    {
        Vector2 currentParentPos = new Vector2(parentBone.localPosition.x, parentBone.localPosition.y);
        Vector2 currentChildPos = new Vector2(bone.localPosition.x, bone.localPosition.y);

        Vector2 lastParentPos = lastPositions[parentIndex];
        Vector2 lastChildPos = lastPositions[index];

        Vector2 previousDirection = lastChildPos - lastParentPos;
        Vector2 currentDirection = currentChildPos - currentParentPos;

        if (previousDirection == Vector2.zero || currentDirection == Vector2.zero)
        {
            return; // Avoid zero vector normalization
        }

        previousDirection.Normalize();
        currentDirection.Normalize();

        float angle = Vector2.SignedAngle(previousDirection, currentDirection);
        bone.localRotation = Quaternion.Euler(0, 0, bone.localRotation.eulerAngles.z + angle);

        // Update last known positions
        lastPositions[index] = currentChildPos;
        lastPositions[parentIndex] = currentParentPos;
    }

    void UpdateDebugJoints()
    {
        SkinnedMeshRenderer renderer = gameObject.GetComponent<SkinnedMeshRenderer>();
        
        for (int i = 0; i < renderer.bones.Length; i++)
        {
            jointVisuals[i].transform.position = renderer.bones[i].position;
        }
    }
    
    void Update()
    {
        
        if (lockDisplayFrame > -1)
        {
            SetDisplayFrame(lockDisplayFrame);
        }
        else
        {
            Animate();
        }
        
        ApplyJointRotations();

        UpdateDebugJoints();
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
}
