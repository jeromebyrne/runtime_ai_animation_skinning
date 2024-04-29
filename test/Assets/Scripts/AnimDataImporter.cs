using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

public class AnimDataImporter
{
    public class AnimationData
    {
       public List <float[]> vertices_uv; // 2 floats per vertex + uv
       public List <List <float[] > > joints; // 2 floats per joint position
       public List <List <int>> joints_order;
       public List <int[]> triangles; // 3 vertex indices per triangle
       public string[] joints_names;
       public int[] joints_parents;
       public List<float[]> a_pose_joints; // for pose binding
    }

    public AnimationData AnimData { get; private set; }

    public void Import(string filePath)
    {
        string path = Path.Combine(Application.dataPath, "Resources", filePath);
        string json = File.ReadAllText(path);
        
        AnimData = JsonConvert.DeserializeObject<AnimationData>(json);
        
        // hacks
        ApplyWorkarounds();
    }

    private void ApplyWorkarounds()
    {
        // normalize the a_pose since the data is in pixel space
        float imageWidth = 625.0f; // hardcoded for barbarian image
        float imageHeight = 707.0f;
        
        for (int i = 0; i < AnimData.a_pose_joints.Count; i++)
        {
            // the x and y are flipped
            float temp = AnimData.a_pose_joints[i][1];
            AnimData.a_pose_joints[i][1] = AnimData.a_pose_joints[i][0];
            AnimData.a_pose_joints[i][0] = temp;
            
            AnimData.a_pose_joints[i][0] /= imageWidth;
            AnimData.a_pose_joints[i][1] /= imageHeight;

            // flip y
            AnimData.a_pose_joints[i][1] *= -1.0f;
            AnimData.a_pose_joints[i][1] += 1.0f;
            
            // flip x
            // AnimData.a_pose_joints[i][0] *= -1.0f;
            //AnimData.a_pose_joints[i][0] += 1.0f;
            
            // joints seem to be offset from texture so this is a workaround
            AnimData.a_pose_joints[i][0] -= 0.05f;
        }
        
        // joints seem to be smaller than mesh
        // let's increase the joints size
        float xScale = 1.5f;
        float yScale = -1.5f;
        float xOffset = -0.25f;
        float yOffset = 1.25f;

        foreach (List <float[] > frameJoints in AnimData.joints)
        {
            foreach (float[] j in frameJoints)
            {
                j[0] *= xScale;
                j[1] *= yScale;
                
                j[0] += xOffset;
                j[1] += yOffset;
            }
        }
    }
}
