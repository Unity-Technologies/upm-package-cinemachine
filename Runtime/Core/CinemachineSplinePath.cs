using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Cinemachine
{
    /// <summary>Defines a world-space path, consisting of an array of waypoints,
    /// each of which has position and roll settings.  Bezier interpolation
    /// is performed between the waypoints, to get a smooth and continuous path.
    /// The path will pass through all waypoints, and (unlike CinemachinePath) first 
    /// and second order continuity is guaranteed</summary>
    [DocumentationSorting(DocumentationSortingAttribute.Level.UserRef)]
    [AddComponentMenu("Cinemachine/CinemachineSplinePath")]
    [SaveDuringPlay]
    [DisallowMultipleComponent]
    public sealed class CinemachineSplinePath : CinemachinePathBase, ISplineProvider
    {

        readonly Spline[] m_SplineArray = new Spline[1];

        [SerializeField]
        Spline m_Spline = new Spline();

        public Spline Spline
        {
            get => m_Spline;
            set => m_Spline = value;
        }

        IEnumerable<Spline> ISplineProvider.Splines
        {
            get
            {
                m_SplineArray[0] = Spline;
                return m_SplineArray;
            }
        }

        private float3 EvaluateCurvePosition(int segmentIndex, float t)
        {
            if (Spline == null)
                return float.PositiveInfinity;

            return SplineUtility.EvaluateSegmentPosition(Spline, segmentIndex, t);
        }

        private float3 EvaluateCurveTangent(int segmentIndex, float t)
        {
            if (Spline == null)
                return float.PositiveInfinity;

            return SplineUtility.EvaluateSegmentTangent(Spline, segmentIndex, t);
        }

        

        /// <summary>The minimum value for the path position</summary>
        public override float MinPos { get { return 0; } }

        /// <summary>The maximum value for the path position</summary>
        public override float MaxPos
        {
            get
            {
                int count = Spline.KnotCount - 1;
                if (count < 1)
                    return 0;
                return Spline.Closed ? count + 1 : count;
            }
        }
        /// <summary>True if the path ends are joined to form a continuous loop</summary>
        public override bool Looped { get { return Spline.Closed; } }

        /// <summary>When calculating the distance cache, sample the path this many 
        /// times between points</summary>
        public override int DistanceCacheSampleStepsPerSegment { get { return m_Resolution; } }

        private void OnValidate() { InvalidateDistanceCache(); }

        private void Reset()
        {
            m_Spline = new Spline();
            m_Appearance = new Appearance();
            InvalidateDistanceCache();
        }

        /// <summary>Call this if the path changes in such a way as to affect distances
        /// or other cached path elements</summary>
        public override void InvalidateDistanceCache()
        {
            base.InvalidateDistanceCache();
        }

        /// <summary>Returns standardized position</summary>
        float GetBoundingIndices(float pos, out int indexA, out int indexB)
        {
            pos = StandardizePos(pos);
            int numWaypoints = Spline.KnotCount;
            if (numWaypoints < 2)
                indexA = indexB = 0;
            else
            {
                indexA = Mathf.FloorToInt(pos);
                if (indexA >= numWaypoints)
                {
                    // Only true if looped
                    pos -= MaxPos;
                    indexA = 0;
                }
                indexB = indexA + 1;
                if (indexB == numWaypoints)
                {
                    if (Looped)
                        indexB = 0;
                    else
                    {
                        --indexB;
                        --indexA;
                    }
                }
            }
            return pos;
        }

        /// <summary>Get a worldspace position of a point along the path</summary>
        /// <param name="pos">Position along the path.  Need not be normalized.</param>
        /// <returns>World-space position of the point along at path at pos</returns>
        public override Vector3 EvaluatePosition(float pos)
        {
            Vector3 result = Vector3.zero;
            if (Spline.KnotCount > 0)
            {
                int indexA, indexB;
                pos = GetBoundingIndices(pos, out indexA, out indexB);
                if (indexA == indexB)
                    result = EvaluateCurvePosition(indexA, 0);
                else
                    result = EvaluateCurvePosition(indexA, pos - indexA);
            }
            return transform.TransformPoint(result);
        }

        /// <summary>Get the tangent of the curve at a point along the path.</summary>
        /// <param name="pos">Position along the path.  Need not be normalized.</param>
        /// <returns>World-space direction of the path tangent.
        /// Length of the vector represents the tangent strength</returns>
        public override Vector3 EvaluateTangent(float pos)
        {
            Vector3 result = transform.rotation * Vector3.forward;
            if (Spline.KnotCount > 1)
            {
                int indexA, indexB;
                pos = GetBoundingIndices(pos, out indexA, out indexB);
                if (indexA == indexB)
                    result = EvaluateCurveTangent(indexA, 0);
                else
                    result = EvaluateCurveTangent(indexA, pos - indexA);
            }
            return transform.TransformDirection(result);
        }

        /// <summary>Get the orientation the curve at a point along the path.</summary>
        /// <param name="pos">Position along the path.  Need not be normalized.</param>
        /// <returns>World-space orientation of the path, as defined by tangent, up, and roll.</returns>
        public override Quaternion EvaluateOrientation(float pos)
        {
            Quaternion transformRot = transform.rotation;
            Vector3 transformUp = transformRot * Vector3.up;
            Quaternion result = transformRot;
            return result;
        }

        // same as Quaternion.AngleAxis(roll, Vector3.forward), just simplified
        Quaternion RollAroundForward(float angle)
        {
            float halfAngle = angle * 0.5F * Mathf.Deg2Rad;
            return new Quaternion(
                0,
                0,
                Mathf.Sin(halfAngle),
                Mathf.Cos(halfAngle));
        }
    }
}
