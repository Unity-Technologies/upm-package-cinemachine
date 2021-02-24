#if !UNITY_2019_3_OR_NEWER
#define CINEMACHINE_PHYSICS
#define CINEMACHINE_PHYSICS_2D
#endif

using UnityEngine;

namespace Cinemachine
{
    /// <summary>An ad-hoc collection of helpers, used by Cinemachine
    /// or its editor tools in various places</summary>
    [DocumentationSorting(DocumentationSortingAttribute.Level.Undoc)]
    public static class RuntimeUtility
    {
        /// <summary>Convenience to destroy an object, using the appropriate method depending 
        /// on whether the game is playing</summary>
        /// <param name="obj">The object to destroy</param>
        public static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(obj);
                else
                    UnityEngine.Object.DestroyImmediate(obj);
#else
                UnityEngine.Object.Destroy(obj);
#endif
            }
        }

        /// <summary>
        /// Check whether a GameObject is a prefab.  
        /// For editor only - some things are disallowed if prefab.  In runtime, will always return false.
        /// </summary>
        /// <param name="gameObject">the object to check</param>
        /// <returns>If editor, checks if object is a prefab or prefab instance.  
        /// In runtime, returns false always</returns>
        public static bool IsPrefab(GameObject gameObject)
        {
#if UNITY_EDITOR
            return UnityEditor.PrefabUtility.GetPrefabInstanceStatus(gameObject)
                    != UnityEditor.PrefabInstanceStatus.NotAPrefab;
#else
            return false;
#endif
        }
        
#if CINEMACHINE_PHYSICS
        private static RaycastHit[] s_HitBuffer = new RaycastHit[16];

        /// <summary>
        /// Perform a raycast, but pass through any objects that have a given tag
        /// </summary>
        /// <param name="ray">The ray to cast</param>
        /// <param name="hitInfo">The returned results</param>
        /// <param name="rayLength">Length of the raycast</param>
        /// <param name="layerMask">Layers to include</param>
        /// <param name="ignoreTag">Tag to ignore</param>
        /// <returns>True if something was hit.  Results in hitInfo</returns>
        public static bool RaycastIgnoreTag(
            Ray ray, out RaycastHit hitInfo, float rayLength, int layerMask, in string ignoreTag)
        {
            if (ignoreTag.Length == 0)
            {
                if (Physics.Raycast(
                    ray, out hitInfo, rayLength, layerMask,
                    QueryTriggerInteraction.Ignore))
                {
                    return true;
                }
            }
            else
            {
                int closestHit = -1;
                int numHits = Physics.RaycastNonAlloc(
                    ray, s_HitBuffer, rayLength, layerMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < numHits; ++i)
                {
                    if (s_HitBuffer[i].collider.CompareTag(ignoreTag))
                        continue;
                    if (closestHit < 0 || s_HitBuffer[i].distance < s_HitBuffer[closestHit].distance)
                        closestHit = i;
                }
                if (closestHit >= 0)
                {
                    hitInfo = s_HitBuffer[closestHit];
                    if (numHits == s_HitBuffer.Length)
                        s_HitBuffer = new RaycastHit[s_HitBuffer.Length * 2];   // full! grow for next time
                    return true;
                }
            }
            hitInfo = new RaycastHit();
            return false;
        }

        /// <summary>
        /// Perform a sphere cast, but pass through objects with a given tag
        /// </summary>
        /// <param name="rayStart">Start of the ray</param>
        /// <param name="radius">Radius of the sphere cast</param>
        /// <param name="dir">Normalized direction of the ray</param>
        /// <param name="hitInfo">Results go here</param>
        /// <param name="rayLength">Length of the ray</param>
        /// <param name="layerMask">Layers to include</param>
        /// <param name="ignoreTag">Tag to ignore</param>
        /// <returns>True if something is hit.  Results in hitInfo.</returns>
        public static bool SphereCastIgnoreTag(
            Vector3 rayStart, float radius, Vector3 dir, 
            out RaycastHit hitInfo, float rayLength, 
            int layerMask, in string ignoreTag)
        {
            int closestHit = -1;
            int numHits = Physics.SphereCastNonAlloc(
                rayStart, radius, dir, s_HitBuffer, rayLength, layerMask, 
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < numHits; ++i)
            {
                if (ignoreTag.Length > 0 && s_HitBuffer[i].collider.CompareTag(ignoreTag))
                    continue;
                if (closestHit < 0 || s_HitBuffer[i].distance < s_HitBuffer[closestHit].distance)
                    closestHit = i;
            }
            if (closestHit >= 0)
            {
                hitInfo = s_HitBuffer[closestHit];

                // Are colliders overlapping?  If so, hitInfo will have special
                // values that are not helpful to the caller.  Fix that here.
                if (hitInfo.distance == 0 && hitInfo.normal == -dir)
                {
                    var scratchCollider = GetScratchCollider();
                    scratchCollider.radius = radius;
                    var c = hitInfo.collider;

                    if (Physics.ComputePenetration(
                        scratchCollider, rayStart, Quaternion.identity,
                        c, c.transform.position, c.transform.rotation,
                        out var offsetDir, out var offsetDistance))
                    {
                        hitInfo.point = rayStart + offsetDir * (offsetDistance - radius);
                        hitInfo.distance = radius - offsetDistance;
                        hitInfo.normal = offsetDir;
                    }
                    else
                    {
                        closestHit = -1; // don't know what's going on, just forget about it
                    }
                }
                if (numHits == s_HitBuffer.Length)
                    s_HitBuffer = new RaycastHit[s_HitBuffer.Length * 2]; // full! grow for next time

                return closestHit >= 0;
            }
            hitInfo = new RaycastHit();
            return false;
        }

        private static Collider[] s_ColliderBuffer = new Collider[5];
        private static SphereCollider s_ScratchCollider;
        private static GameObject s_ScratchColliderGameObject;
        const float Epsilon = Utility.UnityVectorExtensions.Epsilon;
        const float PrecisionSlush = 0.001f;

        static SphereCollider GetScratchCollider()
        {
            if (s_ScratchColliderGameObject == null)
            {
                s_ScratchColliderGameObject = new GameObject("Cinemachine Scratch Collider");
                s_ScratchColliderGameObject.hideFlags = HideFlags.HideAndDontSave;
                s_ScratchColliderGameObject.transform.position = Vector3.zero;
                s_ScratchColliderGameObject.SetActive(true);
                s_ScratchCollider = s_ScratchColliderGameObject.AddComponent<SphereCollider>();
                s_ScratchCollider.isTrigger = true;
                var rb = s_ScratchColliderGameObject.AddComponent<Rigidbody>();
                rb.detectCollisions = false;
                rb.isKinematic = true;
            }
            return s_ScratchCollider;
        }

        internal static void DestroyScratchCollider()
        {
            if (s_ScratchColliderGameObject != null)
            {
                s_ScratchColliderGameObject.SetActive(false);
                DestroyObject(s_ScratchColliderGameObject.GetComponent<Rigidbody>());
            }
            DestroyObject(s_ScratchCollider);
            DestroyObject(s_ScratchColliderGameObject);
            s_ScratchColliderGameObject = null;
            s_ScratchCollider = null;
        }

        internal static Vector3 RespectCameraRadius(
            Vector3 cameraPos, 
            Vector3 lookAtPos,
            float cameraRadius,
            LayerMask collideAgainst,
            LayerMask transparentLayers,
            float minimumDistanceFromTarget,
            string ignoreTag)
        {
            Vector3 result = Vector3.zero;
            if (cameraRadius < Epsilon || collideAgainst == 0)
                return result;

            Vector3 dir = cameraPos - lookAtPos;
            float distance = dir.magnitude;
            if (distance > Epsilon)
                dir /= distance;

            // Pull it out of any intersecting obstacles
            RaycastHit hitInfo;
            int numObstacles = Physics.OverlapSphereNonAlloc(
                cameraPos, cameraRadius, s_ColliderBuffer,
                collideAgainst, QueryTriggerInteraction.Ignore);
            if (numObstacles == 0 && transparentLayers != 0
                && distance > minimumDistanceFromTarget + Epsilon)
            {
                // Make sure the camera position isn't completely inside an obstacle.
                // OverlapSphereNonAlloc won't catch those.
                float d = distance - minimumDistanceFromTarget;
                Vector3 targetPos = lookAtPos + dir * minimumDistanceFromTarget;
                if (RaycastIgnoreTag(new Ray(targetPos, dir), 
                    out hitInfo, d, collideAgainst, ignoreTag))
                {
                    // Only count it if there's an incoming collision but not an outgoing one
                    Collider c = hitInfo.collider;
                    if (!c.Raycast(new Ray(cameraPos, -dir), out hitInfo, d))
                        s_ColliderBuffer[numObstacles++] = c;
                }
            }
            if (numObstacles > 0 && distance == 0 || distance > minimumDistanceFromTarget)
            {
                var scratchCollider = GetScratchCollider();
                scratchCollider.radius = cameraRadius;

                Vector3 newCamPos = cameraPos;
                for (int i = 0; i < numObstacles; ++i)
                {
                    Collider c = s_ColliderBuffer[i];
                    if (ignoreTag.Length > 0 && c.CompareTag(ignoreTag))
                        continue;

                    // If we have a lookAt target, move the camera to the nearest edge of obstacle
                    if (distance > minimumDistanceFromTarget)
                    {
                        dir = newCamPos - lookAtPos;
                        float d = dir.magnitude;
                        if (d > Epsilon)
                        {
                            dir /= d;
                            var ray = new Ray(lookAtPos, dir);
                            if (c.Raycast(ray, out hitInfo, d + cameraRadius))
                                newCamPos = ray.GetPoint(hitInfo.distance) - (dir * PrecisionSlush);
                        }
                    }
                    if (Physics.ComputePenetration(
                        scratchCollider, newCamPos, Quaternion.identity,
                        c, c.transform.position, c.transform.rotation,
                        out var offsetDir, out var offsetDistance))
                    {
                        newCamPos += offsetDir * offsetDistance;
                    }
                }
                result = newCamPos - cameraPos;
            }

            // Respect the minimum distance from target - push camera back if we have to
            if (distance > Epsilon && minimumDistanceFromTarget > Epsilon)
            {
                float minDistance = minimumDistanceFromTarget + PrecisionSlush;
                Vector3 newOffset = cameraPos + result - lookAtPos;
                if (newOffset.magnitude < minDistance)
                    result = lookAtPos - cameraPos + dir * minDistance;
            }

            return result;
        }
#endif
    }
}

