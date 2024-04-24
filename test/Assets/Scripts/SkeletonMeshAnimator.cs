using System.Collections.Generic;
using UnityEngine;

public class SkeletonMeshAnimator : MonoBehaviour
{
    private const float KFrameRate = 1/10.0f;
    
    private readonly AnimDataImporter _animDataImporter = new AnimDataImporter(); // loads json with anim data
    private int _currentFrame = -1;
    private float _lastFrameChange;
    private readonly int _lockDisplayFrame = -1; // -1 to unlock and animate
    private readonly bool _applyRotations = true; // for debugging
    private Vector2[] _lastPositions;
    private readonly List<GameObject> _jointVisuals = new List<GameObject>();
    private SkinnedMeshRenderer _skinnedMeshRenderer;
    
    [SerializeField] private GameObject joinVisualPrefab;
    [SerializeField] private string animDataFile;
    [SerializeField] private string animMaterial;
    
    void Start()
    {
        _animDataImporter.Import(animDataFile);

        Mesh mesh = CreateMesh();
        
        var bones = CreateSkeleton(_animDataImporter.AnimData.joints_names, 
                                            _animDataImporter.AnimData.joints_parents,
                                            _animDataImporter.AnimData.a_pose_joints);

        SetupBoneWeightsAndPoses(bones);
    }
    
    Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();
        
        _skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
        if (_skinnedMeshRenderer == null) _skinnedMeshRenderer = gameObject.AddComponent<SkinnedMeshRenderer>();
        _skinnedMeshRenderer.material = Resources.Load<Material>(animMaterial);
        _skinnedMeshRenderer.sharedMesh = mesh;
        
        var vertData = _animDataImporter.AnimData.vertices_uv;
    
        // reformat the vertices from x,y to x,y,z to satisfy Unity Mesh
        Vector3[] vertices = new Vector3[vertData.Count];

        for (int i = 0; i < vertData.Count; ++i)
        {
            Vector3 v = new Vector3(vertData[i][1],vertData[i][0], 1.0f);
            vertices[i] = v;
        }
        
        // flatten the triangle data to satisfy unity Mesh
        int[] triangles = new int[_animDataImporter.AnimData.triangles.Count * 3];

        int currentTriangleIndex = 0;

        foreach (var t in _animDataImporter.AnimData.triangles)
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
            // flip: in the data uv is actually vu
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
                                List <float[] > aPoseJoints)
    {
        Transform[] bones = new Transform[jointNames.Length];
        
        _lastPositions = new Vector2[jointNames.Length];
        
        for (int i = 0; i < jointNames.Length; i++) 
        {
            GameObject bone = new GameObject(jointNames[i]); 
            bone.transform.localPosition = new Vector3(aPoseJoints[i][0], aPoseJoints[i][1], 0.0f);  // Local position set

            bones[i] = bone.transform;  
            if (jointParents[i] != -1) 
            {  
                bones[i].parent = bones[jointParents[i]]; 
            }
            // Store initial local position
            _lastPositions[i] = bones[i].localPosition; 

            // create debug joint image
            GameObject jointVisual = Instantiate(joinVisualPrefab, bone.transform.position, Quaternion.identity);
            jointVisual.transform.position = new Vector3(aPoseJoints[i][0], aPoseJoints[i][1], 0.0f);
            DebugJointVisualizer djv = jointVisual.GetComponent<DebugJointVisualizer>();
            djv.m_SpriteRenderer.color = Color.green;
            djv.m_text.text = jointNames[i];
            
            _jointVisuals.Add(jointVisual);
        }

        return bones;
    }
    
    void SetupBoneWeightsAndPoses(Transform[] bones) 
    {
        // Bind poses are necessary for the bones relative to the mesh
        Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++) 
        {
            bindPoses[i] = bones[i].worldToLocalMatrix * _skinnedMeshRenderer.transform.localToWorldMatrix;
        }
        
        _skinnedMeshRenderer.sharedMesh.bindposes = bindPoses;
        _skinnedMeshRenderer.bones = bones;  
        _skinnedMeshRenderer.rootBone = bones[0];
        
        // make the MeshAnimation the parent for easy viewing in Editor
        _skinnedMeshRenderer.rootBone.parent = gameObject.transform;

        BoneWeightCalculator bwCalc = gameObject.AddComponent<BoneWeightCalculator>();
        bwCalc.AssignBoneWeights(_skinnedMeshRenderer);
    }

    void Animate()
    {
        _lastFrameChange -= Time.deltaTime;

        int nextFrame = _currentFrame;
        if (_lastFrameChange <= 0.0f)
        {
            nextFrame++;
            if (nextFrame >= _animDataImporter.AnimData.joints.Count) nextFrame = 0;
 
            _lastFrameChange = KFrameRate;
        }

        SetDisplayFrame(nextFrame);
    }

    void SetDisplayFrame(int frame)
    {
        if (_currentFrame == frame) return;

        _currentFrame = frame;
        
        var frameJoints = _animDataImporter.AnimData.joints[_currentFrame];
        
        for (int i = 0; i < _skinnedMeshRenderer.bones.Length && i < frameJoints.Count; i++)
        {
            // TODO: new Vector here is inefficient, store the joints as Vector3 array at creation
            Vector3 newPosition = new Vector3(frameJoints[i][0], frameJoints[i][1], 0);
            _skinnedMeshRenderer.bones[i].position = newPosition;
        }
    }

    void ApplyJointRotations()
    {
        if (!_applyRotations) return;
    
        var frameJoints = _animDataImporter.AnimData.joints[_currentFrame];

        // Process each bone according to the hierarchy defined by joints_parents
        for (int i = 0; i < _skinnedMeshRenderer.bones.Length; i++)
        {
            int parentIndex = _animDataImporter.AnimData.joints_parents[i];
            Transform parentBone = (parentIndex != -1 && parentIndex < _skinnedMeshRenderer.bones.Length) ? _skinnedMeshRenderer.bones[parentIndex] : null;

            if (parentBone)
            {
                ApplyRotation(_skinnedMeshRenderer.bones[i], parentBone, i, parentIndex);
            }
        }
    }
    
    void ApplyRotation(Transform bone, Transform child, int index, int childIndex)
    {
        // Fetch the initial A-pose positions from the data importer
        // TODO: new Vector here is inefficient, store the joints as Vector3 array at creation
        Vector3 aposeParentPos = new Vector3(_animDataImporter.AnimData.a_pose_joints[index][0],
            _animDataImporter.AnimData.a_pose_joints[index][1], 0.0f);
        Vector3 aposeChildPos = new Vector3(_animDataImporter.AnimData.a_pose_joints[childIndex][0],
            _animDataImporter.AnimData.a_pose_joints[childIndex][1], 0.0f);

        // Current positions in the actual scene
        Vector3 currentParentPos = bone.position;
        Vector3 currentChildPos = child.position;

        // Calculate the initial and current direction from the bone to its child.
        Vector3 initialDirection = (aposeChildPos - aposeParentPos).normalized;
        Vector3 currentDirection = (currentChildPos - currentParentPos).normalized;

        // Check if directions are valid
        if (initialDirection == Vector3.zero || currentDirection == Vector3.zero)  return;  // Skip rotation if the direction vector is zero

        initialDirection.Normalize();
        currentDirection.Normalize();

        // Calculate the rotation directly
        Quaternion desiredRotation = Quaternion.FromToRotation(initialDirection, currentDirection);

        // Apply this rotation to the bone's localRotation directly
        bone.localRotation = desiredRotation;
    }

    void UpdateDebugJoints()
    {
        for (int i = 0; i < _skinnedMeshRenderer.bones.Length; i++)
        {
            _jointVisuals[i].transform.position = _skinnedMeshRenderer.bones[i].position;
        }
    }
    
    void Update()
    {
        if (_lockDisplayFrame > -1)
        {
            SetDisplayFrame(_lockDisplayFrame);
        }
        else
        {
            Animate();
        }
        
        ApplyJointRotations();

        UpdateDebugJoints();
    }
}
