#if CINEMACHINE_UNITY_INPUTSYSTEM

using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinemachine
{
    /// <summary>
    /// Finds InputSystem package's default mouse control look input asset, and returns a reference to it.
    /// </summary>
    static class CinemachineDefaultMouseInput
    {
        static InputActionReference s_InputActionReference = null;
        static CinemachineDefaultMouseInput()
        {
            var inputActionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Packages/com.unity.inputsystem/" +
                "InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions");

            InputAction look;
            var enumerator = inputActionAsset.GetEnumerator();
            for (int i = 0; i < 40; ++i)
            {
                if (enumerator.Current != null && 
                    enumerator.Current.ToString() == "Player/Look[/Mouse/delta,/Pen/delta]")
                {
                    look = enumerator.Current;
                    s_InputActionReference = InputActionReference.Create(look);
                    s_InputActionReference.name = "PlayerLook";
                    break;
                }
                enumerator.MoveNext();
            }
        }
        static InputActionReference GetInputActionReference()
        {
            return s_InputActionReference;
        }

        static GUIContent m_InputProviderAddLabel = new GUIContent(
            "Add CinemachineInputProvider", "Adds CinemachineInputProvider to this vcam, if it does not have one already, " +
            "enabling the vcam to read input from Input Actions. By default, a simple mouse XY input action is added.");
        
        /// <summary>
        /// Adds a button that adds adds CinemachineInputProvider component to the vcam with a default look control
        /// (XY axis), if the gameobject has at least one component or extension that requires input and the vcam does
        /// not already have a CinemachineInputProvider component. For a component or extension to require input,
        /// the component or extension needs to override InputRequired in CinemachineComponentBase or
        /// CinemachineExtension respectively.
        /// <seealso cref="CinemachineVirtualCameraBaseEditor"/>
        /// </summary>
        /// <param name="gameObject">The gameObject to which we'd like to add the CinemachineInputProvider
        /// via a Button interface</param>
        public static void InputProviderButton(GameObject gameObject)
        {
            var inputProvider = gameObject.GetComponent<CinemachineInputProvider>();
            if (inputProvider != null) return;
            
            GUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("InputSystem package is installed, but it is not used to control this vcam.", 
                MessageType.Info);
            var helpBoxHeight = GUILayoutUtility.GetLastRect().height;
            var rect = EditorGUILayout.GetControlRect(true);
            rect.height = helpBoxHeight;
            if (GUI.Button(rect, m_InputProviderAddLabel))
            {
                inputProvider = gameObject.AddComponent<CinemachineInputProvider>();
                inputProvider.XYAxis = GetInputActionReference();
            }
            GUILayout.EndHorizontal();
        }
    }
}

#endif