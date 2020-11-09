#if !UNITY_2019_3_OR_NEWER
#define CINEMACHINE_PHYSICS
#define CINEMACHINE_PHYSICS_2D
#endif

using UnityEngine;
using UnityEditor;
using System;

namespace Cinemachine.Editor
{
    internal static class CinemachineMenu
    {
        // Assets Menu
        private const string m_CinemachineAssetsRootMenu = "Assets/Create/Cinemachine/";

        [MenuItem(m_CinemachineAssetsRootMenu + "BlenderSettings")]
        private static void CreateBlenderSettingAsset()
        {
            ScriptableObjectUtility.Create<CinemachineBlenderSettings>();
        }

        [MenuItem(m_CinemachineAssetsRootMenu + "NoiseSettings")]
        private static void CreateNoiseSettingAsset()
        {
            ScriptableObjectUtility.Create<NoiseSettings>();
        }

        [MenuItem(m_CinemachineAssetsRootMenu + "Fixed Signal Definition")]
        private static void CreateFixedSignalDefinition()
        {
            ScriptableObjectUtility.Create<CinemachineFixedSignal>();
        }
        
        // GameObject Menu
        private const string m_CinemachineGameObjectRootMenu = "GameObject/Cinemachine/";
        private const int m_MenuPriority = 11; // right after Camera
        
        [MenuItem(m_CinemachineGameObjectRootMenu + "Virtual Camera", false, m_MenuPriority)]
        public static CinemachineVirtualCamera CreateVirtualCamera()
        {
            return InternalCreateVirtualCamera(
                "CM vcam", true, typeof(CinemachineComposer), typeof(CinemachineTransposer));
        }

        [MenuItem(m_CinemachineGameObjectRootMenu + "FreeLook Camera", false, m_MenuPriority)]
        private static void CreateFreeLookCamera()
        {
            CreateCameraBrainIfAbsent();
            GameObject go = InspectorUtility.CreateGameObject(
                    GenerateUniqueObjectName(typeof(CinemachineFreeLook), "CM FreeLook"),
                    typeof(CinemachineFreeLook));
            if (SceneView.lastActiveSceneView != null)
                go.transform.position = SceneView.lastActiveSceneView.pivot;
            Selection.activeGameObject = go;
        }

        [MenuItem(m_CinemachineGameObjectRootMenu + "Blend List Camera", false, m_MenuPriority)]
        private static void CreateBlendListCamera()
        {
            CreateCameraBrainIfAbsent();
            GameObject go = InspectorUtility.CreateGameObject(
                    GenerateUniqueObjectName(typeof(CinemachineBlendListCamera), "CM BlendListCamera"),
                    typeof(CinemachineBlendListCamera));
            if (SceneView.lastActiveSceneView != null)
                go.transform.position = SceneView.lastActiveSceneView.pivot;
            Undo.RegisterCreatedObjectUndo(go, "create Blend List camera");
            var vcam = go.GetComponent<CinemachineBlendListCamera>();
            Selection.activeGameObject = go;

            // Give it a couple of children
            var child1 = CreateDefaultVirtualCamera();
            Undo.SetTransformParent(child1.transform, go.transform, "create BlendListCam child");
            var child2 = CreateDefaultVirtualCamera();
            child2.m_Lens.FieldOfView = 10;
            Undo.SetTransformParent(child2.transform, go.transform, "create BlendListCam child");

            // Set up initial instruction set
            vcam.m_Instructions = new CinemachineBlendListCamera.Instruction[2];
            vcam.m_Instructions[0].m_VirtualCamera = child1;
            vcam.m_Instructions[0].m_Hold = 1f;
            vcam.m_Instructions[1].m_VirtualCamera = child2;
            vcam.m_Instructions[1].m_Blend.m_Style = CinemachineBlendDefinition.Style.EaseInOut;
            vcam.m_Instructions[1].m_Blend.m_Time = 2f;
        }

        [MenuItem(m_CinemachineGameObjectRootMenu + "State-Driven Camera", false, m_MenuPriority)]
        private static void CreateStateDivenCamera()
        {
            CreateCameraBrainIfAbsent();
            GameObject go = InspectorUtility.CreateGameObject(
                    GenerateUniqueObjectName(typeof(CinemachineStateDrivenCamera), "CM StateDrivenCamera"),
                    typeof(CinemachineStateDrivenCamera));
            if (SceneView.lastActiveSceneView != null)
                go.transform.position = SceneView.lastActiveSceneView.pivot;
            Undo.RegisterCreatedObjectUndo(go, "create state driven camera");
            Selection.activeGameObject = go;

            // Give it a child
            Undo.SetTransformParent(CreateDefaultVirtualCamera().transform, go.transform, "create state driven camera");
        }

#if CINEMACHINE_PHYSICS
        [MenuItem(m_CinemachineGameObjectRootMenu + "ClearShot Camera", false, m_MenuPriority)]
        private static void CreateClearShotVirtualCamera()
        {
            CreateCameraBrainIfAbsent();
            GameObject go = InspectorUtility.CreateGameObject(
                    GenerateUniqueObjectName(typeof(CinemachineClearShot), "CM ClearShot"),
                    typeof(CinemachineClearShot));
            if (SceneView.lastActiveSceneView != null)
                go.transform.position = SceneView.lastActiveSceneView.pivot;
            Undo.RegisterCreatedObjectUndo(go, "create ClearShot camera");
            Selection.activeGameObject = go;

            // Give it a child
            var child = CreateDefaultVirtualCamera();
            Undo.SetTransformParent(child.transform, go.transform, "create ClearShot camera");
            var collider = Undo.AddComponent<CinemachineCollider>(child.gameObject);
            collider.m_AvoidObstacles = false;
            Undo.RecordObject(collider, "create ClearShot camera");
        }
#endif

        [MenuItem(m_CinemachineGameObjectRootMenu + "Dolly Camera with Track", false, m_MenuPriority)]
        private static void CreateDollyCameraWithPath()
        {
            CinemachineVirtualCamera vcam = InternalCreateVirtualCamera(
                    "CM vcam", true, typeof(CinemachineComposer), typeof(CinemachineTrackedDolly));
            GameObject go = InspectorUtility.CreateGameObject(
                    GenerateUniqueObjectName(typeof(CinemachineSplinePath), "DollyTrack"),
                    typeof(CinemachineSplinePath));
            if (SceneView.lastActiveSceneView != null)
                go.transform.position = SceneView.lastActiveSceneView.pivot;
            Undo.RegisterCreatedObjectUndo(go, "create track");
            CinemachineSplinePath path = go.GetComponent<CinemachineSplinePath>();
            var dolly = vcam.GetCinemachineComponent<CinemachineTrackedDolly>();
            Undo.RecordObject(dolly, "create track");
            dolly.m_Path = path;
        }

        [MenuItem(m_CinemachineGameObjectRootMenu + "Dolly Track with Cart", false, m_MenuPriority)]
        private static void CreateDollyTrackWithCart()
        {
            GameObject go = InspectorUtility.CreateGameObject(
                    GenerateUniqueObjectName(typeof(CinemachineSplinePath), "DollyTrack"),
                    typeof(CinemachineSplinePath));
            if (SceneView.lastActiveSceneView != null)
                go.transform.position = SceneView.lastActiveSceneView.pivot;
            Undo.RegisterCreatedObjectUndo(go, "create track");
            CinemachineSplinePath path = go.GetComponent<CinemachineSplinePath>();
            Selection.activeGameObject = go;

            go = InspectorUtility.CreateGameObject(
                GenerateUniqueObjectName(typeof(CinemachineDollyCart), "DollyCart"),
                typeof(CinemachineDollyCart));
            if (SceneView.lastActiveSceneView != null)
                go.transform.position = SceneView.lastActiveSceneView.pivot;
            Undo.RegisterCreatedObjectUndo(go, "create cart");
            CinemachineDollyCart cart = go.GetComponent<CinemachineDollyCart>();
            Undo.RecordObject(cart, "create track");
            cart.m_Path = path;
        }

        [MenuItem(m_CinemachineGameObjectRootMenu + "Target Group Camera", false, m_MenuPriority)]
        private static void CreateTargetGroupCamera()
        {
            CinemachineVirtualCamera vcam = InternalCreateVirtualCamera(
                    "CM vcam", true, typeof(CinemachineGroupComposer), typeof(CinemachineTransposer));
            GameObject go = InspectorUtility.CreateGameObject(
                    GenerateUniqueObjectName(typeof(CinemachineTargetGroup), "TargetGroup"),
                    typeof(CinemachineTargetGroup));
            if (SceneView.lastActiveSceneView != null)
                go.transform.position = SceneView.lastActiveSceneView.pivot;
            Undo.RegisterCreatedObjectUndo(go, "create target group");
            vcam.LookAt = go.transform;
            vcam.Follow = go.transform;
        }

        [MenuItem(m_CinemachineGameObjectRootMenu + "Mixing Camera", false, m_MenuPriority)]
        private static void CreateMixingCamera()
        {
            CreateCameraBrainIfAbsent();
            GameObject go = InspectorUtility.CreateGameObject(
                    GenerateUniqueObjectName(typeof(CinemachineMixingCamera), "CM MixingCamera"),
                    typeof(CinemachineMixingCamera));
            if (SceneView.lastActiveSceneView != null)
                go.transform.position = SceneView.lastActiveSceneView.pivot;
            Undo.RegisterCreatedObjectUndo(go, "create MixingCamera camera");
            Selection.activeGameObject = go;

            // Give it a couple of children
            Undo.SetTransformParent(CreateDefaultVirtualCamera().transform, go.transform, "create MixedCamera child");
            Undo.SetTransformParent(CreateDefaultVirtualCamera().transform, go.transform, "create MixingCamera child");
        }

        [MenuItem(m_CinemachineGameObjectRootMenu + "2D Camera", false, m_MenuPriority)]
        private static void Create2DCamera()
        {
            InternalCreateVirtualCamera("CM vcam", true, typeof(CinemachineFramingTransposer));
        }

#if !UNITY_2019_1_OR_NEWER
        [MenuItem("Cinemachine/Import Post Processing V2 Adapter Asset Package")]
        private static void ImportPostProcessingV2Package()
        {
            var message = "In Cinemachine 2.4.0 and up, the PostProcessing adapter is built-in, and "
                + "can be auto-enabled by Unity 2019 and up.\n\n"
                + "Unity 2018.4 is unable to auto-detect the presence of PostProcessing, so you must "
                + "manually add a define to your player settings to enable the code.\n\n"
                + "To enable support for PostProcessing v2, please do the following:\n\n"
                + "1. Delete the CinemachinePostProcessing folder from your assets, if it's present\n\n"
                + "2. Open the Player Settings tab in Project Settings\n\n"
                + "3. Add this define: CINEMACHINE_POST_PROCESSING_V2";

            EditorUtility.DisplayDialog("Cinemachine Adapter Code for PostProcessing V2", message, "OK");
        }

        [MenuItem("Cinemachine/Import CinemachineExamples Asset Package")]
        private static void ImportExamplePackage()
        {
            string pkgFile = ScriptableObjectUtility.CinemachineInstallPath
                + "/Extras~/CinemachineExamples.unitypackage";
            if (!System.IO.File.Exists(pkgFile))
                Debug.LogError("Missing file " + pkgFile);
            else
                AssetDatabase.ImportPackage(pkgFile, true);
        }
#endif

        /// <summary>
        /// Create a default Virtual Camera, with standard components
        /// </summary>
        public static CinemachineVirtualCamera CreateDefaultVirtualCamera()
        {
            return InternalCreateVirtualCamera(
                "CM vcam", false, typeof(CinemachineComposer), typeof(CinemachineTransposer));
        }

        /// <summary>
        /// Create a static Virtual Camera, with no procedural components
        /// </summary>
        public static CinemachineVirtualCamera CreateStaticVirtualCamera()
        {
            return InternalCreateVirtualCamera("CM vcam", false);
        }

        /// <summary>
        /// Create a Virtual Camera, with components
        /// </summary>
        internal static CinemachineVirtualCamera InternalCreateVirtualCamera(
            string name, bool selectIt, params Type[] components)
        {
            // Create a new virtual camera
            var brain = CreateCameraBrainIfAbsent();
            GameObject go = InspectorUtility.CreateGameObject(
                    GenerateUniqueObjectName(typeof(CinemachineVirtualCamera), name),
                    typeof(CinemachineVirtualCamera));
            CinemachineVirtualCamera vcam = go.GetComponent<CinemachineVirtualCamera>();
            SetVcamFromSceneView(vcam);
            Undo.RegisterCreatedObjectUndo(go, "create " + name);
            GameObject componentOwner = vcam.GetComponentOwner().gameObject;
            foreach (Type t in components)
                Undo.AddComponent(componentOwner, t);
            vcam.InvalidateComponentPipeline();
            if (brain != null && brain.OutputCamera != null)
                vcam.m_Lens = LensSettings.FromCamera(brain.OutputCamera);
            if (selectIt)
                Selection.activeObject = go;
            return vcam;
        }

        public static void SetVcamFromSceneView(CinemachineVirtualCamera vcam)
        {
            if (SceneView.lastActiveSceneView != null)
            {
                vcam.transform.position = SceneView.lastActiveSceneView.camera.transform.position;
                vcam.transform.rotation = SceneView.lastActiveSceneView.camera.transform.rotation;
                var lens = LensSettings.FromCamera(SceneView.lastActiveSceneView.camera);
                // Don't grab these
                lens.NearClipPlane = LensSettings.Default.NearClipPlane;
                lens.FarClipPlane = LensSettings.Default.FarClipPlane;
                vcam.m_Lens = lens;
            }
        }

        /// <summary>
        /// If there is no CinemachineBrain in the scene, try to create one on the main camera
        /// </summary>
        public static CinemachineBrain CreateCameraBrainIfAbsent()
        {
            CinemachineBrain[] brains = UnityEngine.Object.FindObjectsOfType(
                    typeof(CinemachineBrain)) as CinemachineBrain[];
            CinemachineBrain brain = (brains != null && brains.Length > 0) ? brains[0] : null;
            if (brain == null)
            {
                Camera cam = Camera.main;
                if (cam == null)
                {
                    Camera[] cams = UnityEngine.Object.FindObjectsOfType(
                            typeof(Camera)) as Camera[];
                    if (cams != null && cams.Length > 0)
                        cam = cams[0];
                }
                if (cam != null)
                {
                    brain = Undo.AddComponent<CinemachineBrain>(cam.gameObject);
                }
            }
            return brain;
        }

        /// <summary>
        /// Generate a unique name with the given prefix by adding a suffix to it
        /// </summary>
        public static string GenerateUniqueObjectName(Type type, string prefix)
        {
            int count = 0;
            UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(type);
            foreach (UnityEngine.Object o in all)
            {
                if (o != null && o.name.StartsWith(prefix))
                {
                    string suffix = o.name.Substring(prefix.Length);
                    int i;
                    if (Int32.TryParse(suffix, out i) && i > count)
                        count = i;
                }
            }
            return prefix + (count + 1);
        }
    }
}
