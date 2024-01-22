using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SSAOParameter
{
    public ClampedIntParameter aoStrenth = new ClampedIntParameter(0, 0, 1);
    public ClampedIntParameter sampleKernelCount = new ClampedIntParameter(64, 4, 64);
    
    public ClampedIntParameter downSample = new ClampedIntParameter(2, 1, 8);
    public ClampedFloatParameter luminanceThreshold = new ClampedFloatParameter(0.6f,0,1);
}
