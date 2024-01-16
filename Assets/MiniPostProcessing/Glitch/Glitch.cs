using System; 
using System.Collections; 
using System.Collections.Generic; 
using UnityEngine; 
using UnityEngine.Rendering; 
using UnityEngine.Rendering.Universal; 
 
[Serializable, VolumeComponentMenu("Post-processing/Glitch")] 
public class Glitch : VolumeComponent, IPostProcessComponent 
{ 
    [Range(0.0f, 1.0f)] 
    public FloatParameter Fade = new FloatParameter(1); 
 
    [Range(0.0f, 1.0f)] 
    public FloatParameter Speed = new FloatParameter(0.5f); 
 
    [Range(0.0f, 10.0f)] 
    public FloatParameter Amount = new FloatParameter(1f); 
 
    [Range(0.0f, 50.0f)] 
    public FloatParameter BlockLayer1_U = new FloatParameter(9f); 
 
    [Range(0.0f, 50.0f)] 
    public FloatParameter BlockLayer1_V = new FloatParameter(9f); 
 
    [Range(0.0f, 50.0f)] 
    public FloatParameter BlockLayer2_U = new FloatParameter(5f); 
 
    [Range(0.0f, 50.0f)] 
    public FloatParameter BlockLayer2_V = new FloatParameter(5f); 
 
    [Range(0.0f, 50.0f)] 
    public FloatParameter BlockLayer1_Indensity = new FloatParameter(8f); 
 
    [Range(0.0f, 50.0f)] 
    public FloatParameter BlockLayer2_Indensity = new FloatParameter(4f); 
 
    [Range(0.0f, 50.0f)] 
    public FloatParameter RGBSplitIndensity = new FloatParameter(0.5f); 
 
    public bool IsActive() => Amount.value > 0f; 
 
    public bool IsTileCompatible() => false; 
} 