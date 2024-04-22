using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JointVisualizer : MonoBehaviour
{
    [SerializeField]
    private GameObject m_jointPrefab;

    private AnimDataImporter m_animDataImporter = new AnimDataImporter();
    private List<List<GameObject>> m_jointMarkersParents = new List<List<GameObject>>();
    private int m_currentFrame = 0;
    private float frameRate = 1/5.0f;
    private float lastFrameChange = 0.0f;
    
    void Start()
    {
        m_animDataImporter.Import("unpickled.json");
        
        CreateJoints();

        lastFrameChange = frameRate;
    }
    
    void Update()
    {
        Animate();
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

        for (int i = 0; i < numFrames; i++)
        {
            List<GameObject> framejointMarkers = m_jointMarkersParents[i];
            
            foreach (GameObject g in framejointMarkers)
            {
                g.SetActive(i == m_currentFrame);
            }
        }
    }

    void CreateJoints()
    {
        int numFrames = m_animDataImporter.AnimData.joints.Count;

        for (int i = 0; i < numFrames; ++i)
        {
            List<float[]> frameJoints = m_animDataImporter.AnimData.joints[i];

            m_jointMarkersParents.Add(new List<GameObject>());
            
            GameObject jmParent = new GameObject("jointParent");
            
            foreach (var coords in frameJoints)
            {
                Vector3 initialPosition = new Vector3(coords[0], coords[1], 0);
                GameObject jointMarker = Instantiate(m_jointPrefab, initialPosition, Quaternion.identity);
                jointMarker.transform.parent = jmParent.transform;
            }
            
            m_jointMarkersParents[i].Add(jmParent);
        }
        
        // reposition
        foreach (var jmp in m_jointMarkersParents)
        {
            foreach (GameObject go in jmp)
            {
                go.transform.localScale = new Vector3(10.0f, -10.0f, 1f);
                go.transform.localPosition = new Vector3(-5.0f, 5.0f, 0.0f);
                go.SetActive(false);
            }
        }
    }
}
