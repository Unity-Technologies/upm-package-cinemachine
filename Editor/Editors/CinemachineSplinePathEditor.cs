using UnityEditor;
using UnityEditor.Splines;
using UnityEditor.EditorTools;

namespace Cinemachine.Editor
{
    [CustomEditor(typeof(CinemachineSplinePath))]
    internal sealed class CinemachineSplinePathEditor : BaseEditor<CinemachineSplinePath>
    {
        [DrawGizmo(GizmoType.Active | GizmoType.NotInSelectionHierarchy
             | GizmoType.InSelectionHierarchy | GizmoType.Pickable, typeof(CinemachineSplinePath))]
        static void DrawGizmos(CinemachineSplinePath path, GizmoType selectionType)
        {
            var isActive = Selection.activeGameObject == path.gameObject;
            CinemachinePathEditor.DrawPathGizmo(path,
                isActive ? path.m_Appearance.pathColor : path.m_Appearance.inactivePathColor, isActive);
        }
        
    }
}
