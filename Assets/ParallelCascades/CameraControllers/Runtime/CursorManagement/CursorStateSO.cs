using UnityEngine;

namespace ParallelCascades.CameraControllers.Runtime.CursorManagement
{
    public abstract class CursorStateSO : ScriptableObject
    {
        public abstract Texture2D GetCursor();
        [SerializeField] private Vector2 m_hotspot;
        public virtual Vector2 Hotspot => m_hotspot;

        public void Apply()
        {
            Cursor.SetCursor(GetCursor(), Hotspot, CursorMode.Auto);
        }
    }
}