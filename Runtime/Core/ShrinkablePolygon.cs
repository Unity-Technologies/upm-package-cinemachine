﻿using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine.Utility;
using ClipperLib;
using UnityEngine;
using UnityEngine.Assertions;

namespace Cinemachine
{
    /// <summary>
    /// ShrinkablePolygon represents points with shrink directions.
    /// </summary>
    internal class ShrinkablePolygon
    {
        /// <summary>
        /// 2D point with shrink direction.
        /// </summary>
        public struct ShrinkablePoint2
        {
            public Vector2 m_Position;
            public Vector2 m_OriginalPosition;
            public Vector2 m_ShrinkDirection;
            public bool m_CantIntersect;
            public static readonly Vector2 m_Vector2NaN = new Vector2(float.NaN, float.NaN);

            // public ShrinkablePoint2()
            // {
            //     m_OriginalPosition = m_Vector2NaN;
            // }
            //
            // public ShrinkablePoint2(Vector2 mPosition, Vector2 mOriginalPosition, Vector2 mShrinkDirection, 
            //     bool mCantIntersect)
            // {
            //     m_Position = mPosition;
            //     m_OriginalPosition = mOriginalPosition;
            //     m_ShrinkDirection = mShrinkDirection;
            //     m_CantIntersect = mCantIntersect;
            // }
        }

        public List<ShrinkablePoint2> m_Points;
        public float m_WindowDiagonal;
        public int m_State;
        public readonly float m_AspectRatio;
        public readonly float m_AspectRatioBasedDiagonal;
        public readonly Vector2[] m_NormalDirections;
        public float m_MinArea;
        public List<Vector2> m_IntersectionPoints;
        
        private float m_area;
        private bool m_clockwiseOrientation;

        /// <summary>
        /// Default constructor initializing points and intersection points.
        /// </summary>
        private ShrinkablePolygon()
        {
            m_Points = new List<ShrinkablePoint2>();
            m_IntersectionPoints = new List<Vector2>();
        }

        /// <summary>
        /// Parameterized constructor for points and aspect ratio.
        /// </summary>
        public ShrinkablePolygon(List<Vector2> points, float aspectRatio) : this()
        {
            m_Points = new List<ShrinkablePoint2>(points.Count);
            for (int i = 0; i < points.Count; ++i)
            {
                m_Points.Add(new ShrinkablePoint2
                {
                    m_Position = points[i],
                    m_OriginalPosition = points[i],
                });
            }
            
            m_AspectRatio = aspectRatio;
            m_AspectRatioBasedDiagonal = Mathf.Sqrt(m_AspectRatio*m_AspectRatio + 1);
            m_NormalDirections = new[]
            {
                Vector2.up,
                new Vector2(m_AspectRatio, 1),
                new Vector2(m_AspectRatio, 0),
                new Vector2(m_AspectRatio, -1),
                Vector2.down,
                new Vector2(-m_AspectRatio, -1),
                new Vector2(-m_AspectRatio, 0),
                new Vector2(-m_AspectRatio, 1),
            };
            
            ComputeSignedArea();
            ComputeNormals(true);
        }

        /// <summary>
        /// Creates and returns a deep copy of this subPolygons.
        /// </summary>
        /// <returns>Deep copy of this subPolygons</returns>
        public ShrinkablePolygon DeepCopy()
        {
            return new ShrinkablePolygon(m_AspectRatio, m_AspectRatioBasedDiagonal, m_NormalDirections)
            {
                m_clockwiseOrientation = m_clockwiseOrientation,
                m_area = m_area,
                m_MinArea = m_MinArea,
                m_WindowDiagonal = m_WindowDiagonal,
                m_State = m_State,
                
                // deep
                m_Points = m_Points.ConvertAll(point =>
                    new ShrinkablePoint2
                    {
                        m_Position = point.m_Position,
                        m_OriginalPosition = point.m_OriginalPosition,
                        m_ShrinkDirection = point.m_ShrinkDirection,
                        m_CantIntersect = point.m_CantIntersect,
                    }),
                m_IntersectionPoints =
                    m_IntersectionPoints.ConvertAll(intersection => new Vector2(intersection.x, intersection.y))
            };
        }
        
        /// <summary>
        /// Private constructor for shallow copying normal directions.
        /// </summary>
        public ShrinkablePolygon(float aspectRatio, float aspectRatioBasedDiagonal, Vector2[] normalDirections) 
            : this()
        {
            m_AspectRatio = aspectRatio;
            m_AspectRatioBasedDiagonal = aspectRatioBasedDiagonal;
            m_NormalDirections = normalDirections; // shallow copy is enough here
        }

        /// <summary>
        /// Computes signed area and determines whether a subPolygons is oriented clockwise or counter-clockwise.
        /// </summary>
        /// <returns>Area of the subPolygons</returns>
        private float ComputeSignedArea()
        {
            m_area = 0;
            for (int i = 0; i < m_Points.Count; ++i)
            {
                ShrinkablePoint2 p1 = m_Points[i];
                ShrinkablePoint2 p2 = m_Points[(i + 1) % m_Points.Count];

                m_area += (p2.m_Position.x - p1.m_Position.x) * (p2.m_Position.y + p1.m_Position.y);
            }

            m_clockwiseOrientation = m_area > 0;
            return m_area;
        }

        /// <summary>
        /// Computes normalized normals for all points. If fixBigCornerAngles is true, then adds additional points for
        /// corners with reflex angles to ensure correct offsets.
        /// </summary>
        private void ComputeNormals(bool fixBigCornerAngles)
        {
            var edgeNormals = new List<Vector2>(m_Points.Count);
            for (int i = 0; i < m_Points.Count; ++i)
            {
                Vector2 edge = m_Points[(i + 1) % m_Points.Count].m_Position - m_Points[i].m_Position;
                Vector2 normal = m_clockwiseOrientation ? new Vector2(edge.y, -edge.x) : new Vector2(-edge.y, edge.x); 
                edgeNormals.Add(normal.normalized);
            }

            // calculating normals
            for (int i = 0; i < m_Points.Count; ++i)
            {
                int prevEdgeIndex = i == 0 ? edgeNormals.Count - 1 : i - 1;
                var mPoint = m_Points[i];
                mPoint.m_ShrinkDirection = (edgeNormals[i] + edgeNormals[prevEdgeIndex]).normalized;
                m_Points[i] = mPoint;
            }

            if (fixBigCornerAngles)
            {
                // we need to fix corners with reflex angle (negative for polygons oriented clockwise,
                // positive for polygons oriented counterclockwise)
                // fixing means that we add more shrink directions, because at this corners the offset from the
                // camera middle point can be different depending on which way the camera comes from
                // worst case: every point has negative angle
                // (not possible in practise, but makes the algorithm simpler)
                // so all in all we will have 3 times as many points as before (1 + 2 extra for each point)
                
                // original points are placed with padding _ 0 _ _ 1 _ _ 2 _ _ ...
                List<ShrinkablePoint2> extendedPoints = new ShrinkablePoint2[m_Points.Count * 3].ToList();
                for (int i = 0; i < m_Points.Count; ++i)
                {
                    extendedPoints[i * 3 + 1] = m_Points[i];
                    
                    int prevEdgeIndex = i == 0 ? edgeNormals.Count - 1 : i - 1;
                    float angle = Vector2.SignedAngle(edgeNormals[i], edgeNormals[prevEdgeIndex]);
                    if (m_clockwiseOrientation && angle < 0 ||
                        !m_clockwiseOrientation && angle > 0)
                    {
                        var shrinkablePoint2 = extendedPoints[i * 3 + 1];
                        shrinkablePoint2.m_OriginalPosition = ShrinkablePoint2.m_Vector2NaN;
                        extendedPoints[i * 3 + 1] = shrinkablePoint2;
                        
                        int prevIndex = (i == 0 ? m_Points.Count - 1 : i - 1);
                        extendedPoints[i * 3 + 0] = new ShrinkablePoint2
                        {
                            m_Position = Vector2.Lerp(m_Points[i].m_Position, m_Points[prevIndex].m_Position, 0.01f),
                            m_ShrinkDirection = m_Points[i].m_ShrinkDirection,
                            m_CantIntersect = true,
                            m_OriginalPosition = ShrinkablePoint2.m_Vector2NaN,
                        };
                        
                        int nextIndex = (i == m_Points.Count - 1 ? 0 : i + 1);
                        extendedPoints[i * 3 + 2] = new ShrinkablePoint2
                        {
                            m_Position = Vector2.Lerp(m_Points[i].m_Position, m_Points[nextIndex].m_Position, 0.01f),
                            m_ShrinkDirection = m_Points[i].m_ShrinkDirection,
                            m_CantIntersect = true,
                            m_OriginalPosition = ShrinkablePoint2.m_Vector2NaN,
                        };
                    }
                }
                
                // TODO: remove unused
                // remove unused padding
                for (int index = extendedPoints.Count - 1; index >= 0; index--)
                {
                    if (extendedPoints[index].m_Position == Vector2.zero &&
                        extendedPoints[index].m_OriginalPosition == Vector2.zero &&
                        extendedPoints[index].m_ShrinkDirection == Vector2.zero
                        )
                    {
                        extendedPoints.RemoveAt(index);
                    }
                }

                m_Points = extendedPoints;
            }
        }
    
        /// <summary>
        /// Computes shrink directions that respect the aspect ratio of the camera. If the camera window is a square,
        /// then the shrink directions will be equivalent to the normals.
        /// </summary>
        public void ComputeAspectBasedShrinkDirections()
        {
            // cache current shrink directions to check for change later
            var cachedShrinkDirections = new List<Vector2>(m_Points.Count);
            for (int i = 0; i < m_Points.Count; ++i)
            {
                cachedShrinkDirections.Add(m_Points[i].m_ShrinkDirection);
            }
            
            // calculate shrink directions
            ComputeNormals(false);
            for (int i = 0; i < m_Points.Count; ++i)
            {
                int prevIndex = i == 0 ? m_Points.Count - 1 : i - 1;
                int nextIndex = i == m_Points.Count - 1 ? 0 : i + 1;

                var mPoint = m_Points[i];
                mPoint.m_ShrinkDirection = CalculateShrinkDirection(mPoint.m_ShrinkDirection, 
                    m_Points[prevIndex].m_Position, mPoint.m_Position, m_Points[nextIndex].m_Position);
                m_Points[i] = mPoint;
            }

            // update m_State, if change happened based on the cached shrink directions
            if (cachedShrinkDirections.Count != m_Points.Count)
            {
                m_State++; // m_State change if more points where added
            }
            else
            {
                for (var index = 0; index < cachedShrinkDirections.Count; index++)
                {
                    if (cachedShrinkDirections[index] != m_Points[index].m_ShrinkDirection)
                    {
                        m_State++; // m_State change when even one shrink direction has been changed
                        break;
                    }
                }
            }
        }
        
        private static readonly int FloatToIntScaler = 10000000; // same as in Physics2D

        /// <summary>
        /// Converts shrinkable polygons into a simple path made of 2D points.
        /// </summary>
        /// <param name="shrinkablePolygons">input shrinkable polygons</param>
        /// <param name="frustumHeight">Frustum height requested by the user.
        /// For the path touching the corners this may be relevant.</param>
        /// <param name="path">output result</param>
        public static void ConvertToPath(in List<ShrinkablePolygon> shrinkablePolygons, float frustumHeight,
            out List<List<Vector2>> path, out bool hasIntersections)
        {
            hasIntersections = false;
            // convert shrinkable polygons points to int based points for Clipper
            List<List<IntPoint>> clip = new List<List<IntPoint>>(shrinkablePolygons.Count);
            int index = 0;
            for (var polyIndex = 0; polyIndex < shrinkablePolygons.Count; polyIndex++)
            {
                var polygon = shrinkablePolygons[polyIndex];
                clip.Add(new List<IntPoint>(polygon.m_Points.Count));
                foreach (var point in polygon.m_Points)
                {
                    clip[index].Add(new IntPoint(point.m_Position.x * FloatToIntScaler,
                        point.m_Position.y * FloatToIntScaler));
                }
                index++;

                // add a thin line to each intersection point, thus connecting disconnected polygons
                foreach (var intersectionPoint in polygon.m_IntersectionPoints)
                {
                    hasIntersections = true;
                    
                    Vector2 closestPoint = polygon.ClosestPolygonPoint(intersectionPoint);
                    Vector2 direction = (closestPoint - intersectionPoint).normalized;
                    Vector2 epsilonNormal = new Vector2(direction.y, -direction.x) * 0.01f;

                    clip.Add(new List<IntPoint>(4));
                    Vector2 p1 = closestPoint + epsilonNormal;
                    Vector2 p2 = intersectionPoint + epsilonNormal;
                    Vector3 p3 = intersectionPoint - epsilonNormal;
                    Vector3 p4 = closestPoint - epsilonNormal;

                    clip[index].Add(new IntPoint(p1.x * FloatToIntScaler, p1.y * FloatToIntScaler));
                    clip[index].Add(new IntPoint(p2.x * FloatToIntScaler, p2.y * FloatToIntScaler));
                    clip[index].Add(new IntPoint(p3.x * FloatToIntScaler, p3.y * FloatToIntScaler));
                    clip[index].Add(new IntPoint(p4.x * FloatToIntScaler, p4.y * FloatToIntScaler));

                    index++;
                }
                
                foreach (var point in polygon.m_Points)
                {
                    if (!point.m_OriginalPosition.IsNaN())
                    {
                        Vector2 corner = point.m_OriginalPosition;
                        Vector2 shrinkDirection = point.m_Position - corner;
                        float cornerDistance = shrinkDirection.sqrMagnitude;
                        if (shrinkDirection.x > 0)
                        {
                            shrinkDirection *= (polygon.m_AspectRatio / shrinkDirection.x);
                        }
                        else if (shrinkDirection.x < 0)
                        {
                            shrinkDirection *= -(polygon.m_AspectRatio / shrinkDirection.x);
                        }
                        if (shrinkDirection.y > 1)
                        {
                            shrinkDirection *= (1f / shrinkDirection.y);
                        }
                        else if (shrinkDirection.y < -1)
                        {
                            shrinkDirection *= -(1f / shrinkDirection.y);
                        }

                        shrinkDirection *= frustumHeight;
                        if (shrinkDirection.sqrMagnitude > cornerDistance)
                        {
                            continue; // camera is already touching this point
                        }
                        Vector2 cornerTouchingPoint = corner + shrinkDirection;
                        Vector2 epsilonNormal = new Vector2(shrinkDirection.y, -shrinkDirection.x).normalized * 0.01f;
                        clip.Add(new List<IntPoint>(4));
                        Vector2 p1 = point.m_Position + epsilonNormal;
                        Vector2 p2 = cornerTouchingPoint + epsilonNormal;
                        Vector2 p3 = cornerTouchingPoint - epsilonNormal;
                        Vector2 p4 = point.m_Position - epsilonNormal;
                        clip[index].Add(new IntPoint(p1.x * FloatToIntScaler, p1.y * FloatToIntScaler));
                        clip[index].Add(new IntPoint(p2.x * FloatToIntScaler, p2.y * FloatToIntScaler));
                        clip[index].Add(new IntPoint(p3.x * FloatToIntScaler, p3.y * FloatToIntScaler));
                        clip[index].Add(new IntPoint(p4.x * FloatToIntScaler, p4.y * FloatToIntScaler));
        
                        index++;
                    }
    
                }
            }

            // Merge polygons with Clipper
            List<List<IntPoint>> solution = new List<List<IntPoint>>();
            Clipper c = new Clipper();
            c.AddPaths(clip, PolyType.ptClip, true);
            c.Execute(ClipType.ctUnion, solution, 
                PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);
            
            // Convert result to float points
            path = new List<List<Vector2>>(solution.Count);
            foreach (var polySegment in solution)
            {
                var pathSegment = new List<Vector2>(polySegment.Count);
                for (index = 0; index < polySegment.Count; index++)
                {
                    var p_int = polySegment[index];
                    var p = new Vector2(p_int.X / (float) FloatToIntScaler, p_int.Y / (float) FloatToIntScaler);
                    pathSegment.Add(p);
                }
                path.Add(pathSegment);
            }
        }

        /// <summary>
        /// Checks whether p is inside or outside the polygons. The algorithm determines if a point is inside based
        /// on a horizontal raycast from p. If the ray intersects the polygon odd number of times, then p is inside.
        /// Otherwise, p is outside.
        /// </summary>
        /// <param name="polygons">Input polygons</param>
        /// <param name="p">Input point.</param>
        /// <returns>True, if inside. False, otherwise.</returns>
        public static bool IsInside(in List<List<Vector2>> polygons, in Vector2 p)
        {
            float minX = Single.PositiveInfinity;
            float maxX = Single.NegativeInfinity;
            foreach (var path in polygons)
            {
                foreach (var point in path)
                {
                    var pointInWorldCoordinates = point;
                    minX = Mathf.Min(minX, pointInWorldCoordinates.x);
                    maxX = Mathf.Max(maxX, pointInWorldCoordinates.x);
                }
            }
            float polygonXWidth = maxX - minX;
            if (!(minX <= p.x && p.x <= maxX))
            {
                return false; // p is outside to the left or to the right
            }
            
            int intersectionCount = 0;
            Vector2 camRayEndFromCamPos2D = p + Vector2.right * polygonXWidth;
            foreach (var polygon in polygons)
            {
                for (int index = 0; index < polygon.Count; ++index)
                {
                    Vector2 p1 = polygon[index];
                    Vector2 p2 = polygon[(index + 1) % polygon.Count];
                    int intersectionType = UnityVectorExtensions.FindIntersection(p, camRayEndFromCamPos2D, p1, p2, 
                        out _);
                    if (intersectionType == 2)
                    {
                        intersectionCount++;
                    }
                }
            }

            return intersectionCount % 2 != 0; // inside polygon when odd number of intersections
        }

        
        /// <summary>
        /// Finds midpoint of a rectangle's side touching CA and CB.
        /// D1 - D2 defines the side or diagonal of a rectangle touching CA and CB.
        /// </summary>
        /// <returns>Midpoint of a rectangle's side touching CA and CB.</returns>
        private static Vector2 FindMidPoint(in Vector2 A, in Vector2 B, in Vector2 C, in Vector2 D1, in Vector2 D2)
        {
            Vector2 CA = (A - C);
            Vector2 CB = (B - C);

            float gamma = UnityVectorExtensions.Angle(CA, CB);
            if (gamma <= 0.05f || 179.95f <= gamma) 
            { 
                return (A + B) / 2; // too narrow angle, so just return the mid point
            }
            Vector2 D1D2 = D1 - D2;
            Vector2 D1C = C - B;
            float beta = UnityVectorExtensions.Angle(D1C, D1D2);
            Vector2 D2D1 = D2 - D1;
            Vector2 D2C = C - A;
            float alpha = UnityVectorExtensions.Angle(D2C, D2D1);
            if (Math.Abs(gamma + beta + alpha - 180) > 0.5f)
            {
                D1D2 = D2 - D1;
                D1C = C - B;
                beta = UnityVectorExtensions.Angle(D1C, D1D2);
                D2D1 = D1 - D2;
                D2C = C - A;
                alpha = UnityVectorExtensions.Angle(D2C, D2D1);
            }
            if (alpha <= 0.05f || 179.95f <= alpha || 
                beta <= 0.05f || 179.95f <= beta)
            {
                return (A + B) / 2; // too narrow angle, so just return the mid point
            }

            float c = D1D2.magnitude;
            float a = (c / Mathf.Sin(gamma * Mathf.Deg2Rad)) * Mathf.Sin(alpha * Mathf.Deg2Rad);
            float b = (c / Mathf.Sin(gamma * Mathf.Deg2Rad)) * Mathf.Sin(beta * Mathf.Deg2Rad);

            Vector2 M1 = C + CB.normalized * Mathf.Abs(a);
            Vector2 M2 = C + CA.normalized * Mathf.Abs(b);

            Vector2 dist1 = (A + B) / 2 - C;
            Vector2 dist2 = (M1 + M2) / 2 - C;
            
            return dist1.sqrMagnitude < dist2.sqrMagnitude ? (A + B) / 2 : (M1 + M2) / 2;
        }
        
        /// <summary>
        /// Calculates shrink direction for thisPoint, based on it's normal and neighbouring points.
        /// </summary>
        /// <param name="normal">normal of thisPoint</param>
        /// <param name="prevPoint">previous neighbouring of thisPoint</param>
        /// <param name="thisPoint">point for which the normal is calculated.</param>
        /// <param name="nextPoint">next neighbouring of thisPoint</param>
        /// <returns>Returns shrink direction for thisPoint</returns>
        private Vector2 CalculateShrinkDirection(in Vector2 normal, 
            in Vector2 prevPoint, in Vector2 thisPoint, in Vector2 nextPoint)
        {
            Vector2 A = prevPoint;
            Vector2 B = nextPoint;
            Vector2 C = thisPoint;

            Vector2 CA = (A - C);
            Vector2 CB = (B - C);
            
            float angle1_abs = Vector2.Angle(CA, normal);
            float angle2_abs = Vector2.Angle(CB, normal);
            
            Vector2 R = normal.normalized * m_AspectRatioBasedDiagonal;
            float angle = Vector2.SignedAngle(R, m_NormalDirections[0]);
            if (0 < angle && angle < 90)
            {
                if (angle - angle1_abs <= 1f && 89 <= angle + angle2_abs)
                {
                    // case 0 - 1 point intersection with camera window
                    R = m_NormalDirections[1];
                }
                else if (angle - angle1_abs <= 0 && angle + angle2_abs < 90)
                {
                    // case 1a - 2 point intersection with camera window's bottom
                    Vector2 M = FindMidPoint(A, B, C, m_NormalDirections[3], m_NormalDirections[5]); // bottom side's midpoint
                    Vector2 rectangleMidPoint = M + m_NormalDirections[0]; // rectangle's midpoint
                    R = rectangleMidPoint - C;
                }
                else if (0 < angle - angle1_abs && 90 <= angle + angle2_abs)
                {
                    // case 1b - 2 point intersection with camera window's left side
                    Vector2 M = FindMidPoint(A, B, C, m_NormalDirections[7], m_NormalDirections[5]); // left side's midpoint
                    Vector2 rectangleMidPoint = M + m_NormalDirections[2]; // rectangle's midpoint
                    R = rectangleMidPoint - C;
                }
                else if (0 < angle - angle1_abs && angle + angle2_abs < 90)
                {
                    // case 2 - 2 point intersection with camera window's diagonal (top-left to bottom-right)
                    Vector2 rectangleMidPoint = FindMidPoint(A, B, C, m_NormalDirections[3], m_NormalDirections[7]); // rectangle's midpoint
                    R = rectangleMidPoint - C;
                }
                else
                {
                    Assert.IsTrue(false, "Error in CalculateShrinkDirection - " +
                                         "Let us know on the Cinemachine forum please!");
                }
            }
            else if (90 < angle && angle < 180)
            {
                if (angle - angle1_abs <= 91 && 179 <= angle + angle2_abs)
                {
                    // case 0 - 1 point intersection with camera window
                    R = m_NormalDirections[3];
                }
                else if (angle - angle1_abs <= 90 && angle + angle2_abs < 180)
                {
                    // case 1a - 2 point intersection with camera window's left
                    Vector2 M = FindMidPoint(A, B, C, m_NormalDirections[0], m_NormalDirections[4]); // left side's midpoint
                    Vector2 rectangleMidPoint = M + m_NormalDirections[2]; // rectangle's midpoint
                    R = rectangleMidPoint - C;
                }
                else if (90 < angle - angle1_abs && 180 <= angle + angle2_abs)
                {
                    // case 1b - 2 point intersection with camera window's top side
                    Vector2 M = FindMidPoint(A, B, C, m_NormalDirections[1], m_NormalDirections[7]); // top side's midpoint
                    Vector2 rectangleMidPoint = M + m_NormalDirections[4]; // rectangle's midpoint
                    R = rectangleMidPoint - C;
                }
                else if (90 < angle - angle1_abs && angle + angle2_abs < 180)
                {
                    // case 2 - 2 point intersection with camera window's diagonal (top-right to bottom-left)
                    Vector2 rectangleMidPoint = FindMidPoint(A, B, C, m_NormalDirections[1], m_NormalDirections[5]); // rectangle's midpoint
                    R = rectangleMidPoint - C;
                }
                else
                {
                    Assert.IsTrue(false, "Error in CalculateShrinkDirection - " +
                                         "Let us know on the Cinemachine forum please!");
                }
            }
            else if (-180 < angle && angle < -90)
            {
                if (angle - angle1_abs <= -179 && -91 <= angle + angle2_abs)
                {
                    // case 0 - 1 point intersection with camera window
                    R = m_NormalDirections[5];
                }
                else if (angle - angle1_abs <= -180 && angle + angle2_abs < -90)
                {
                    // case 1a - 2 point intersection with camera window's top
                    Vector2 M = FindMidPoint(A, B, C, m_NormalDirections[7], m_NormalDirections[1]); // top side's midpoint
                    Vector2 rectangleMidPoint = M + m_NormalDirections[4]; // rectangle's midpoint
                    R = rectangleMidPoint - C;
                }
                else if (-180 < angle - angle1_abs && -90 <= angle + angle2_abs)
                {
                    // case 1b - 2 point intersection with camera window's right side
                    Vector2 M = FindMidPoint(A, B, C, m_NormalDirections[1], m_NormalDirections[3]); // right side's midpoint
                    Vector2 rectangleMidPoint = M + m_NormalDirections[6]; // rectangle's midpoint
                    R = rectangleMidPoint - C;
                }
                else if (-180 < angle - angle1_abs && angle + angle2_abs < -90)
                {
                    // case 2 - 2 point intersection with camera window's diagonal (top-left to bottom-right)
                    Vector2 rectangleMidPoint = FindMidPoint(A, B, C, m_NormalDirections[3], m_NormalDirections[7]); // rectangle's midpoint
                    R = rectangleMidPoint - C;
                }
                else
                {
                    Assert.IsTrue(false, "Error in CalculateShrinkDirection - " +
                                         "Let us know on the Cinemachine forum please!");
                }
            }
            else if (-90 < angle && angle < 0)
            {
                if (angle - angle1_abs <= -89 && -1 <= angle + angle2_abs)
                {
                    // case 0 - 1 point intersection with camera window
                    R = m_NormalDirections[7];
                }
                else if (angle - angle1_abs <= -90 && angle + angle2_abs < 0)
                {
                    // case 1a - 2 point intersection with camera window's right side
                    Vector2 M = FindMidPoint(A, B, C, m_NormalDirections[7], m_NormalDirections[5]); // right side's midpoint
                    Vector2 rectangleMidPoint = M + m_NormalDirections[6]; // rectangle's midpoint
                    R = rectangleMidPoint - C;
                }
                else if (-90 < angle - angle1_abs && 0 <= angle + angle2_abs)
                {
                    // case 1b - 2 point intersection with camera window's bottom side
                    Vector2 M = FindMidPoint(A, B, C, m_NormalDirections[5], m_NormalDirections[3]); // bottom side's mid point
                    Vector2 rectangleMidPoint = M + m_NormalDirections[0]; // rectangle's midpoint
                    R = rectangleMidPoint - C;
                }
                else if (-90 < angle - angle1_abs && angle + angle2_abs < 0)
                {
                    // case 2 - 2 point intersection with camera window's diagonal (top-right to bottom-left)
                    Vector2 rectangleMidPoint = FindMidPoint(A, B, C, m_NormalDirections[1], m_NormalDirections[5]); // rectangle's midpoint
                    R = rectangleMidPoint - C;
                }
                else
                {
                    Assert.IsTrue(false, "Error in CalculateShrinkDirection - " +
                                         "Let us know on the Cinemachine forum please!");
                }
            }
            else
            {
                R.x = Mathf.Clamp(R.x, -m_AspectRatio, m_AspectRatio);
                R.y = Mathf.Clamp(R.y, -1, 1);
            }
            
            return R;
        }

        /// <summary>
        /// ShrinkablePolygon is shrinkable if it has at least one non-zero shrink direction.
        /// </summary>
        /// <returns>True, if subPolygons is shrinkable. False, otherwise.</returns>
        public bool IsShrinkable()
        {
            for (int i = 0; i < m_Points.Count; ++i)
            {
                if (m_Points[i].m_ShrinkDirection != Vector2.zero)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Shrink shrinkablePolygon points towards their shrink direction by shrinkAmount.
        /// </summary>
        public bool Shrink(float shrinkAmount, bool shrinkToPoint)
        {
            m_WindowDiagonal += shrinkAmount;
            float area1 = Mathf.Abs(ComputeSignedArea());
            if (area1 < m_MinArea)
            {
                if (shrinkToPoint)
                {
                    Vector2 center = CenterOfMass();
                    for (int i = 0; i < m_Points.Count; ++i)
                    {
                        var mPoint = m_Points[i];
                        Vector2 direction = center - mPoint.m_Position;
                        // normalize direction so it is within the m_AspectRatio x 1 rectangle.
                        if (Math.Abs(direction.x) > m_AspectRatio ||
                            Math.Abs(direction.y) > 1)
                        {
                            if (direction.x > m_AspectRatio)
                            {
                                direction *= (m_AspectRatio / direction.x);
                            }
                            else if (direction.x < -m_AspectRatio)
                            {
                                direction *= -(m_AspectRatio / direction.x);
                            }
                            if (direction.y > 1)
                            {
                                direction *= (1f / direction.y);
                            }
                            else if (direction.y < -1)
                            {
                                direction *= -(1f / direction.y);
                            }

                            mPoint.m_ShrinkDirection = direction;
                        }
                        else
                        {
                            mPoint.m_ShrinkDirection = Vector2.zero;
                        }

                        m_Points[i] = mPoint;
                    }
                }
                else
                {
                    for (int i = 0; i < m_Points.Count; ++i)
                    {
                        var mPoint = m_Points[i];
                        mPoint.m_ShrinkDirection = Vector2.zero;
                        m_Points[i] = mPoint;
                    }

                    return false;
                }
            }
            for (int i = 0; i < m_Points.Count; ++i)
            {
                var mPoint = m_Points[i];
                mPoint.m_Position += mPoint.m_ShrinkDirection * shrinkAmount;
                m_Points[i] = mPoint;
            }
            return true;
        }

        /// <summary>
        /// Simple center of mass of this shrinkable polygon.
        /// </summary>
        /// <returns>Center of Mass</returns>
        private Vector2 CenterOfMass()
        {
            Vector2 center = Vector2.zero;
            for (var i = 0; i < m_Points.Count; ++i)
            {
                center += m_Points[i].m_Position;
            }

            return center / m_Points.Count;
        }

        /// <summary>
        /// Calculates squared distance to 'P' from closest point to 'P' in the subPolygons
        /// </summary>
        /// <param name="p">Point in space.</param>
        /// <returns>Squared distance to 'P' from closest point to 'P' in the subPolygons</returns>
        private float SqrDistanceTo(Vector2 p)
        {
            float minDistance = float.MaxValue;
            for (int i = 0; i < m_Points.Count; ++i)
            {
                minDistance = Mathf.Min(minDistance, (m_Points[i].m_Position - p).sqrMagnitude);
            }

            return minDistance;
        }

        /// <summary>
        /// Calculates the closest point to the subPolygons from P.
        /// The point returned is going to be one of the points of the subPolygons.
        /// </summary>
        /// <param name="p">Point from which the distance is calculated.</param>
        /// <returns>A point that is part of the subPolygons points and is closest to P.</returns>
        private Vector2 ClosestPolygonPoint(Vector2 p)
        {
            float minDistance = float.MaxValue;
            Vector2 closestPoint = Vector2.zero;
            for (int i = 0; i < m_Points.Count; ++i)
            {
                float sqrDistance = (m_Points[i].m_Position - p).sqrMagnitude;
                if (minDistance > sqrDistance)
                {
                    minDistance = sqrDistance;
                    closestPoint = m_Points[i].m_Position;
                }
            }

            return closestPoint;
        }
  
        /// <summary>
        /// Returns point closest to p that is a point of the subPolygons.
        /// </summary>
        /// <param name="p"></param>
        /// <returns>Closest point to p in ShrinkablePolygon</returns>
        public Vector2 ClosestPolygonPoint(ShrinkablePoint2 p)
        {
            var foundWithNormal = false;
            var minDistance = float.MaxValue;
            var closestPoint = Vector2.zero;
            for (int i = 0; i < m_Points.Count; ++i)
            {
                Vector2 diff = m_Points[i].m_Position - p.m_Position;
                float angle = Vector2.Angle(p.m_ShrinkDirection, diff);
                if (angle < 5 || 175 < angle)
                {
                    foundWithNormal = true;
                    float sqrDistance = diff.sqrMagnitude;
                    if (minDistance > sqrDistance)
                    {
                        minDistance = sqrDistance;
                        closestPoint = m_Points[i].m_Position;
                    }
                }
            }
            if (foundWithNormal)
            {
                return closestPoint;
            }

            for (int i = 0; i < m_Points.Count; ++i)
            {
                Vector2 diff = m_Points[i].m_Position - p.m_Position;
                float sqrDistance = diff.sqrMagnitude;
                if (minDistance > sqrDistance)
                {
                    minDistance = sqrDistance;
                    closestPoint = m_Points[i].m_Position;
                }
            }
            return closestPoint;
        }

        /// <summary>
        /// Removes points that are the same or very close.
        /// </summary>
        public void Simplify(float shrinkAmount)
        {
            if (m_Points.Count <= 4)
            {
                return;
            }

            float distanceLimit = shrinkAmount * 2;
            var canSimplify = true;
            while (canSimplify)
            {
                canSimplify = false;
                for (int i = 0; i < m_Points.Count; ++i)
                {
                    int j = (i + 1) % m_Points.Count;
                    
                    if (!m_Points[i].m_CantIntersect && !m_Points[j].m_CantIntersect) continue;
                    
                    if ((m_Points[i].m_Position - m_Points[j].m_Position).sqrMagnitude <= distanceLimit)
                    {
                        if (m_Points[i].m_CantIntersect)
                        {
                            m_Points.RemoveAt(i);
                        }
                        else if (m_Points[j].m_CantIntersect)
                        {
                            m_Points.RemoveAt(j);
                        }
                        else
                        {
                            m_Points.RemoveAt(j);
                            m_Points.RemoveAt(i);
                        }

                        canSimplify = true;
                        break;
                    }
                }
            }
        }

        /// <summary>Divides subPolygons into subPolygons if there are intersections.</summary>
        /// <param name="shrinkablePolygon">ShrinkablePolygon to divide. ShrinkablePolygon will be overwritten by a subPolygons with possible intersections,
        /// after cutting off the subPolygons part 'left' of the intersection.</param>
        /// <param name="subPolygons">Resulting subPolygons from dividing subPolygons.</param>
        /// <returns>True, if found intersection. False, otherwise.</returns>
        private static bool DivideShrinkablePolygon(ref ShrinkablePolygon shrinkablePolygon, 
            ref List<ShrinkablePolygon> subPolygons)
        {
            // for each edge in subPolygons, but not for edges that are neighbours (e.g. 0-1, 1-2),
            // check for intersections.
            // if we intersect, we need to divide the subPolygons into two shrinkablePolygons (g1,g2) to remove the
            // intersection within a subPolygons.
            // g1 will be 'left' of the intersection, g2 will be 'right' of the intersection.
            // g2 may contain additional intersections.
            for (int i = 0; i < shrinkablePolygon.m_Points.Count; ++i)
            {
                int nextI = (i + 1) % shrinkablePolygon.m_Points.Count;
                for (int j = i + 2; j < shrinkablePolygon.m_Points.Count; ++j)
                {
                    int nextJ = (j + 1) % shrinkablePolygon.m_Points.Count;
                    if (i == nextJ) continue;

                    int intersectionType = UnityVectorExtensions.FindIntersection(
                        shrinkablePolygon.m_Points[i].m_Position, shrinkablePolygon.m_Points[nextI].m_Position,
                        shrinkablePolygon.m_Points[j].m_Position, shrinkablePolygon.m_Points[nextJ].m_Position,
                        out Vector2 intersection);
                    
                    if (intersectionType == 2) // so we divide g into g1 and g2.
                    {
                        var g1 = new ShrinkablePolygon(shrinkablePolygon.m_AspectRatio, 
                            shrinkablePolygon.m_AspectRatioBasedDiagonal, shrinkablePolygon.m_NormalDirections);
                        {
                            g1.m_WindowDiagonal = shrinkablePolygon.m_WindowDiagonal;
                            g1.m_IntersectionPoints.Add(intersection);
                            g1.m_State = shrinkablePolygon.m_State + 1;
                            g1.m_MinArea = shrinkablePolygon.m_MinArea;

                            // g1 -> intersection j+1 ... i
                            var points = new List<ShrinkablePoint2>
                            {
                                new ShrinkablePoint2 {m_Position = intersection, m_ShrinkDirection = Vector2.zero,}
                            };
                            for (int k = (j + 1) % shrinkablePolygon.m_Points.Count;
                                k != (i + 1) % shrinkablePolygon.m_Points.Count;
                                k = (k + 1) % shrinkablePolygon.m_Points.Count)
                            {
                                points.Add(shrinkablePolygon.m_Points[k]);
                            }
                            
                            g1.m_Points = RotateListToLeftmost(points);
                        }
                        subPolygons.Add(g1);

                        var g2 = new ShrinkablePolygon(shrinkablePolygon.m_AspectRatio, 
                            shrinkablePolygon.m_AspectRatioBasedDiagonal, shrinkablePolygon.m_NormalDirections);
                        {
                            g2.m_WindowDiagonal = shrinkablePolygon.m_WindowDiagonal;
                            g2.m_IntersectionPoints.Add(intersection);
                            g2.m_State = shrinkablePolygon.m_State + 1;
                            g2.m_MinArea = shrinkablePolygon.m_MinArea;

                            // g2 -> intersection i+1 ... j
                            var points = new List<ShrinkablePoint2>
                            {
                                new ShrinkablePoint2 {m_Position = intersection, m_ShrinkDirection = Vector2.zero,}
                            };
                            for (int k = (i + 1) % shrinkablePolygon.m_Points.Count;
                                k != (j + 1) % shrinkablePolygon.m_Points.Count;
                                k = (k + 1) % shrinkablePolygon.m_Points.Count)
                            {
                                points.Add(shrinkablePolygon.m_Points[k]);
                            }

                            g2.m_Points = RollListToStartClosestToPoint(points, intersection);
                        }

                        // we need to move the intersection points from the parent subPolygons
                        // to g1 and g2 subPolygons, depending on which is closer to the intersection point.
                        for (int k = 0; k < shrinkablePolygon.m_IntersectionPoints.Count; ++k)
                        {
                            float g1Dist = g1.SqrDistanceTo(shrinkablePolygon.m_IntersectionPoints[k]);
                            float g2Dist = g2.SqrDistanceTo(shrinkablePolygon.m_IntersectionPoints[k]);
                            if (g1Dist < g2Dist)
                            {
                                g1.m_IntersectionPoints.Add(shrinkablePolygon.m_IntersectionPoints[k]);
                            }
                            else
                            {
                                g2.m_IntersectionPoints.Add(shrinkablePolygon.m_IntersectionPoints[k]);
                            }
                        }

                        shrinkablePolygon = g2; // need to continue dividing g2 as it may contain more intersections
                        return true; // subPolygons has nice intersections
                    }
                }
            }

            return false; // subPolygons does not have nice intersections
        }

        /// <summary>
        /// Divides input shrinkable polygon into subpolygons until it has no more intersections.
        /// </summary>
        public static void DivideAlongIntersections(ShrinkablePolygon subPolygons, 
            out List<ShrinkablePolygon> subShrinkablePolygon)
        {
            subShrinkablePolygon = new List<ShrinkablePolygon>();
            var maxIteration = 10; // In practise max 1-3 intersections at the same time in the same frame.
            while (maxIteration > 0 && DivideShrinkablePolygon(ref subPolygons, ref subShrinkablePolygon))
            {
                maxIteration--;
            }
            subShrinkablePolygon.Add(subPolygons); // add remaining subPolygons
        }
        
        /// <summary>
        /// Rotates input List of shrinkable points to start closest to input point in 2D space.
        /// This is important to ensure order independence in algorithm.
        /// </summary>
        /// <param name="point">List will rotate so it's 0th element is as close to point as possible.</param>
        /// <param name="points">List to rotate</param>
        /// <returns>List, in which the 0 element is the closest point in the List to point in 2D space.
        /// Order of points of the original list is preserved</returns>
        private static List<ShrinkablePoint2> RollListToStartClosestToPoint(
            in List<ShrinkablePoint2> points, in Vector2 point)
        {
            int closestIndex = 0;
            Vector2 closestPoint = points[0].m_Position;
            for (int i = 1; i < points.Count; ++i)
            {
                if ((closestPoint - point).sqrMagnitude > (closestPoint - points[i].m_Position).sqrMagnitude)
                {
                    closestIndex = i;
                    closestPoint = points[i].m_Position;
                }
            }

            var pointRolledToStartAtClosestPoint = new List<ShrinkablePoint2>(points.Count);
            for (int i = closestIndex; i < points.Count; ++i)
            {
                pointRolledToStartAtClosestPoint.Add(points[i]);
            }

            for (int i = 0; i < closestIndex; ++i)
            {
                pointRolledToStartAtClosestPoint.Add(points[i]);
            }

            return pointRolledToStartAtClosestPoint;
        }

        /// <summary>
        /// Rotates input List to start from the left-most element in 2D space.
        /// This is important to ensure order independence in algorithm.
        /// </summary>
        /// <param name="points">List to rotate</param>
        /// <returns>List, in which the 0 element is the left-most in 2D space.
        /// Order of points of the original list is preserved</returns>
        private static List<ShrinkablePoint2> RotateListToLeftmost(List<ShrinkablePoint2> points)
        {
            int leftMostPointIndex = 0;
            Vector2 leftMostPoint = points[0].m_Position;
            for (int i = 1; i < points.Count; ++i)
            {
                if (leftMostPoint.x > points[i].m_Position.x)
                {
                    leftMostPointIndex = i;
                    leftMostPoint = points[i].m_Position;
                }
            }

            var pointsRolledToStartAtLeftMostPoint = new List<ShrinkablePoint2>(points.Count);
            for (int i = leftMostPointIndex; i < points.Count; ++i)
            {
                pointsRolledToStartAtLeftMostPoint.Add(points[i]);
            }

            for (int i = 0; i < leftMostPointIndex; ++i)
            {
                pointsRolledToStartAtLeftMostPoint.Add(points[i]);
            }

            return pointsRolledToStartAtLeftMostPoint;
        }
    }
}
