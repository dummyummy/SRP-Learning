using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [System.Serializable]
    public struct BloomSettings
    {
        [Range(0, 16)]
        public int maxIterations;

        [Min(1f)] public int downscaleLimit;
        
        public bool bicubicUpsampling;
        
        [Min(0f)]
        public float threshold;

        [Range(0f, 1f)]
        public float thresholdKnee;
        
        [Min(0f)]
        public float intensity;
    }
    
    [SerializeField]
    BloomSettings bloom = default;
    
    public BloomSettings Bloom => bloom;
    
    [SerializeField]
    Shader shader = default;

    [System.NonSerialized]
    Material material;

    public Material Material
    {
        get
        {
            if (material == null && shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }
            return material;
        }
    }
}
