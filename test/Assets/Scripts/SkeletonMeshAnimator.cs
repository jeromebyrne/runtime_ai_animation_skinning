using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SkeletonMeshAnimator : MonoBehaviour
{
    private const float KFrameRate = 1 / 15.0f;

    private readonly AnimDataImporter _animDataImporter = new AnimDataImporter(); // loads json with anim data
    private int _currentFrame = -1;
    private float _lastFrameChange;
    private readonly int _lockDisplayFrame = -1; // -1 to unlock and animate
    private readonly bool _applyRotations = true; // for debugging
    private Vector3[] _targetPositions;
    private readonly List<GameObject> _jointVisuals = new List<GameObject>();
    private SkinnedMeshRenderer _skinnedMeshRenderer;

    [SerializeField] private GameObject joinVisualPrefab;
    [SerializeField] private string animDataFile;
    [SerializeField] private string animMaterial;

    bool _debugAPose = true;
    bool _useFastBoneWeightCalc = true;
    bool _showDebugJoints = true;

    // List of joint indices for which to reduce rotation by 50%
    private HashSet<int> jointsToReduceRotation = new HashSet<int> { /* Populate with indices that need reduced rotation */ };

    void Start()
    {
        _animDataImporter.Import(animDataFile);

        Mesh mesh = CreateMesh();

        var bones = CreateSkeleton(_animDataImporter.AnimData.joints_names,
                                            _animDataImporter.AnimData.joints_parents,
                                            _animDataImporter.AnimData.a_pose_joints,
                                            _animDataImporter.AnimData.joints_order);

        _targetPositions = new Vector3[bones.Length];

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

        AssignConsistentDepth(vertices);

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
        float uvXScale = 1.48f; // hack workaround
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
                                List <float[] > aPoseJoints,
                                List<List<int>> jointsOrder)
    {
        Transform[] bones = new Transform[jointNames.Length];
        
        for (int i = 0; i < jointNames.Length; i++) 
        {
            GameObject bone = new GameObject(jointNames[i]);

            float depth = 1.0f; // (float)jointsOrder[0][i];
            bone.transform.localPosition = new Vector3(aPoseJoints[i][0], aPoseJoints[i][1], depth);  // Local position set

            bones[i] = bone.transform;  
            if (jointParents[i] != -1) 
            {  
                bones[i].parent = bones[jointParents[i]]; 
            }

            // create debug joint image
            if (_showDebugJoints)
            {
                GameObject jointVisual = Instantiate(joinVisualPrefab, bone.transform.position, Quaternion.identity);
                jointVisual.transform.localPosition = new Vector3(aPoseJoints[i][0], aPoseJoints[i][1], -1.0f);
                DebugJointVisualizer djv = jointVisual.GetComponent<DebugJointVisualizer>();
                djv.m_SpriteRenderer.color = Color.green;
                djv.m_text.text = (i + 1).ToString() + "_" + jointNames[i];

                _jointVisuals.Add(jointVisual);
            }
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
        if (_useFastBoneWeightCalc)
        {
            FastBoneWeightCalculator fastBwCalc = gameObject.AddComponent<FastBoneWeightCalculator>();
            fastBwCalc.AssignBoneWeights(_skinnedMeshRenderer);
        }
        else
        {
            // This is insanely expensive
            BoneWeightCalculator bwCalc = gameObject.AddComponent<BoneWeightCalculator>();
            bwCalc.AssignBoneWeights(_skinnedMeshRenderer);
        }
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
        if (_debugAPose) return;

        if (_currentFrame == frame) return;

        _currentFrame = frame;

        var frameJoints = _animDataImporter.AnimData.joints[_currentFrame];

        for (int i = 0; i < _skinnedMeshRenderer.bones.Length && i < frameJoints.Count; i++)
        {
            _targetPositions[i] = new Vector3(frameJoints[i][0], frameJoints[i][1], _skinnedMeshRenderer.bones[i].position.z);
        }
    }

    void ApplyJointRotations()
    {
        if (!_applyRotations) return;

        var frameJoints = _animDataImporter.AnimData.joints[_currentFrame];

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
        // Fetch the A-pose positions and zero out their z-values for 2D plane projection
        Vector3 aposeParentPos = new Vector3(
            _animDataImporter.AnimData.a_pose_joints[index][0],
            _animDataImporter.AnimData.a_pose_joints[index][1],
            0.0f
        );
        Vector3 aposeChildPos = new Vector3(
            _animDataImporter.AnimData.a_pose_joints[childIndex][0],
            _animDataImporter.AnimData.a_pose_joints[childIndex][1],
            0.0f
        );

        // Project current positions onto the 2D plane
        Vector3 currentParentPos = new Vector3(bone.position.x, bone.position.y, 0.0f);
        Vector3 currentChildPos = new Vector3(child.position.x, child.position.y, 0.0f);

        Vector3 initialDirection = (aposeChildPos - aposeParentPos).normalized;
        Vector3 currentDirection = (currentChildPos - currentParentPos).normalized;

        if (initialDirection == Vector3.zero || currentDirection == Vector3.zero) return;

        // Calculate the 2D rotation angle
        float angle = Vector3.SignedAngle(initialDirection, currentDirection, Vector3.forward);

        // Convert angle to a 2D quaternion
        Quaternion desiredRotation = Quaternion.Euler(0, 0, angle);

        // If this bone requires reduced rotation, scale the rotation by 50%
        if (jointsToReduceRotation.Contains(index))
        {
            desiredRotation = Quaternion.Slerp(Quaternion.identity, desiredRotation, 0.5f);
        }

        bone.localRotation = desiredRotation;
    }


    void UpdateDebugJoints()
    {
        if (!_showDebugJoints)
        { return; }

        for (int i = 0; i < _skinnedMeshRenderer.bones.Length; i++)
        {
            _jointVisuals[i].transform.position = _skinnedMeshRenderer.bones[i].position;
        }
    }
    
    void Update()
    {
        if (_debugAPose)
        {
            return;
        }

        if (_lockDisplayFrame > -1)
        {
            SetDisplayFrame(_lockDisplayFrame);
        }
        else
        {
            Animate();
        }

        ApplyJointRotations();
        UpdateJointPositions();
        UpdateDebugJoints();
    }

    void UpdateJointPositions()
    {
        if (_targetPositions == null) return;

        for (int i = 0; i < _skinnedMeshRenderer.bones.Length; i++)
        {
            if (_targetPositions.Length > i)
            {
                // Interpolate only the x and y values
                Vector3 currentPosition = _skinnedMeshRenderer.bones[i].position;
                Vector3 targetPosition = _targetPositions[i];

                float interpolatedX = Mathf.Lerp(currentPosition.x, targetPosition.x, Time.deltaTime / KFrameRate);
                float interpolatedY = Mathf.Lerp(currentPosition.y, targetPosition.y, Time.deltaTime / KFrameRate);

                // Create a new position with the same z value as before
                _skinnedMeshRenderer.bones[i].position = new Vector3(interpolatedX, interpolatedY, currentPosition.z);
            }
        }
    }

    void AssignConsistentDepth(Vector3[] vertices)
    {
        float minX = vertices.Min(v => v.x);
        float maxX = vertices.Max(v => v.x);

        for (int i = 0; i < vertices.Length; i++)
        {
            float normalizedX = (vertices[i].x - minX) / (maxX - minX);
            vertices[i].z = -Mathf.Lerp(-1f, 1f, normalizedX);
        }
    }
}
