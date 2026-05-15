using UnityEngine;

namespace ParallelCascades.CameraControllers.Runtime.CursorManagement
{
    [CreateAssetMenu(menuName = "Parallel Cascades/Camera Controllers/Cursor State SO", order = 0)]
    public class SingleCursorStateSO : CursorStateSO
    {
        [Tooltip("Leaving this empty will set the default system cursor")]
        [SerializeField] private Texture2D m_cursor;
        public override Texture2D GetCursor() => m_cursor;
    }
}