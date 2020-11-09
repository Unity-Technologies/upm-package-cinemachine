using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Splines;
using Unity.Collections;
using Unity.Mathematics;
using Cinemachine;

public class RuntimeDollyTrackTest
{
    private CinemachineDollyCart DollyCart;
    private CinemachineSplinePath path;

    [SetUp]
    public void Setup()
    {
        DollyCart = new GameObject().AddComponent<CinemachineDollyCart>();
        path = new GameObject().AddComponent<CinemachineSplinePath>();
        var array = new NativeArray<float3>(4, Allocator.Temp);
        array[0] = new float3(7, 1, -6);
        array[1] = new float3(13, 1, -6);
        array[2] = new float3(13, 1, 1);
        array[3] = new float3(7, 1, 1);
        Spline.CreateLinear(path.Spline, array, true);
        DollyCart.m_Path = path;
        DollyCart.m_UpdateMethod = CinemachineDollyCart.UpdateMethod.Update;
    }

    [TearDown]
    public void Teardown()
    {
        Object.Destroy(path.gameObject);
        Object.Destroy(DollyCart.gameObject);
    }

    [UnityTest]
    public IEnumerator LinearInterpolationTestDistance()
    {
        DollyCart.m_PositionUnits = CinemachinePathBase.PositionUnits.Distance;
        DollyCart.m_Position = 0;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(7, 1, -6)), 0, 0.1f);
        
        DollyCart.m_Position = 0.5f;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(7.5f, 1, -6)), 0, 0.1f);
        
        DollyCart.m_Position = 6;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(12.98846f, 1, -6)), 0, 0.1f);
        
        DollyCart.m_Position = 10;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(13, 1, -2)), 0, 0.1f);
        
        DollyCart.m_Position = 15;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(11, 1, 1)), 0, 0.1f);
        
        DollyCart.m_Position = 20;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(7, 1, 0)), 0, 0.1f);
        
        DollyCart.m_Position = 26;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(7, 1, -6)), 0, 0.1f);
    }

    [UnityTest]
    public IEnumerator LinearInterpolationTestNormalized()
    {
        DollyCart.m_PositionUnits = CinemachinePathBase.PositionUnits.Normalized;
        DollyCart.m_Position = 0;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(7, 1, -6)), 0, 0.1f);
        
        DollyCart.m_Position = 0.125f;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(10.25f, 1, -6)), 0, 0.1f);
        
        DollyCart.m_Position = 0.5f;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(13, 1, 1)), 0, 0.1f);
        
        DollyCart.m_Position = 0.75f;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(7, 1, 0.5f)), 0, 0.1f);
        
        DollyCart.m_Position = 1;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(7, 1, -6)), 0, 0.1f);
    }

    [UnityTest]
    public IEnumerator LinearInterpolationTestPathUnits()
    {
        DollyCart.m_PositionUnits = CinemachinePathBase.PositionUnits.PathUnits;
        DollyCart.m_Position = 0;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(7, 1, -6)), 0, 0.1f);
        
        DollyCart.m_Position = 0.125f;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(7.75f, 1, -6)), 0, 0.1f);
        
        DollyCart.m_Position = 0.5f;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(10, 1, -6)), 0, 0.1f);
        
        DollyCart.m_Position = 1;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(13, 1, -6)), 0, 0.1f);
        
        DollyCart.m_Position = 1.5f;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(13, 1, -2.5f)), 0, 0.1f);
        
        DollyCart.m_Position = 1.75f;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(13, 1, -0.75f)), 0, 0.1f);
        
        DollyCart.m_Position = 3;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(7, 1, 1)), 0, 0.1f);
        
        DollyCart.m_Position = 3.5f;
        yield return null;
        UnityEngine.Assertions.Assert.AreApproximatelyEqual(Vector3.Distance(DollyCart.transform.position, new Vector3(7, 1, -2.5f)), 0, 0.1f);
    }
}
