using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Splines;
using Unity.Collections;
using Unity.Mathematics;
using Cinemachine;
using Cinemachine.Editor;

public class RuntimeDollyCameraTest
{
    private CinemachineVirtualCamera vcam;
    private CinemachineTrackedDolly dolly;
    private CinemachineSplinePath path;

    [SetUp]
    public void Setup()
    {
        vcam = CinemachineMenu.InternalCreateVirtualCamera("CM vcam", true, typeof(CinemachineComposer), typeof(CinemachineTrackedDolly));
        path = new GameObject().AddComponent<CinemachineSplinePath>();
        var array = new NativeArray<float3>(4, Allocator.Temp);
        array[0] = new float3(7, 1, -6);
        array[1] = new float3(13, 1, -6);
        array[2] = new float3(13, 1, 1);
        array[3] = new float3(7, 1, 1);
        Spline.CreateLinear(path.Spline, array, true);
        dolly = vcam.GetCinemachineComponent<CinemachineTrackedDolly>();
        dolly.m_Path = path;
        dolly.m_ZDamping = 0;
    }

    [TearDown]
    public void Teardown()
    {
        Object.Destroy(path.gameObject);
        Object.Destroy(vcam.gameObject);
    }

    [UnityTest]
    public IEnumerator LinearInterpolationTestDistance()
    {
        dolly.m_PositionUnits = CinemachinePathBase.PositionUnits.Distance;
        dolly.m_PathPosition = 0;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(7, 1, -6)), 0, 0.1f);
        
        dolly.m_PathPosition = 0.5f;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(7.5f, 1, -6)), 0, 0.1f);
        
        dolly.m_PathPosition = 6;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(12.98846f, 1, -6)), 0, 0.1f);
        
        dolly.m_PathPosition = 10;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(13, 1, -2)), 0, 0.1f);
        
        dolly.m_PathPosition = 15;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(11, 1, 1)), 0, 0.1f);
        
        dolly.m_PathPosition = 20;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(7, 1, 0)), 0, 0.1f);
        
        dolly.m_PathPosition = 26;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(7, 1, -6)), 0, 0.1f);
    }

    [UnityTest]
    public IEnumerator LinearInterpolationTestNormalized()
    {
        dolly.m_PositionUnits = CinemachinePathBase.PositionUnits.Normalized;
        dolly.m_PathPosition = 0;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(7, 1, -6)), 0, 0.1f);
        
        dolly.m_PathPosition = 0.125f;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(10.25f, 1, -6)), 0, 0.1f);
        
        dolly.m_PathPosition = 0.5f;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(13, 1, 1)), 0, 0.1f);
        
        dolly.m_PathPosition = 0.75f;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(7, 1, 0.5f)), 0, 0.1f);
        
        dolly.m_PathPosition = 1;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(7, 1, -6)), 0, 0.1f);
    }

    [UnityTest]
    public IEnumerator LinearInterpolationTestPathUnits()
    {
        dolly.m_PositionUnits = CinemachinePathBase.PositionUnits.PathUnits;
        dolly.m_PathPosition = 0;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(7, 1, -6)), 0, 0.1f);
        
        dolly.m_PathPosition = 0.125f;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(7.75f, 1, -6)), 0, 0.1f);
        
        dolly.m_PathPosition = 0.5f;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(10, 1, -6)), 0, 0.1f);
        
        dolly.m_PathPosition = 1;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(13, 1, -6)), 0, 0.1f);
        
        dolly.m_PathPosition = 1.5f;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(13, 1, -2.5f)), 0, 0.1f);
        
        dolly.m_PathPosition = 1.75f;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(13, 1, -0.75f)), 0, 0.1f);
        
        dolly.m_PathPosition = 3;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(7, 1, 1)), 0, 0.1f);
        
        dolly.m_PathPosition = 3.5f;
        vcam.InternalUpdateCameraState(Vector3.up, 0);
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(vcam.m_State.RawPosition, new Vector3(7, 1, -2.5f)), 0, 0.1f);
    }

}
