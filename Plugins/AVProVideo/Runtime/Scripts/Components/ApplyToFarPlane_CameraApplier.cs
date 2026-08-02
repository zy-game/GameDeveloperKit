using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This class will be attached to the object created by the ApplyToFar plane to register the camera that is currently, trying to render,
/// this is needed so we only render to the correct camera in the shader
/// </summary>
public class ApplyToFarPlane_CameraApplier : MonoBehaviour
{
    [SerializeField] private Material _material;
    public Material Material
    {
        get { return _material; }
        set { _material = value; }
    }

    private int _CurrentCamID;

    void Awake()
    {
        _CurrentCamID = Shader.PropertyToID("_CurrentCamID");
    }

    // this is called before the rendering of the object, by a specific camera, Camera.current is also changed
    // to be the camera currently rendering at the time.
    void OnWillRenderObject()
    {
        if (_material)
        {
        #if UNITY_6000_4_OR_NEWER
            _material.SetInteger(_CurrentCamID, Camera.current.GetEntityId().GetHashCode());
        #else
            _material.SetFloat(_CurrentCamID, Camera.current.GetInstanceID());
        #endif
        }
    }
}
