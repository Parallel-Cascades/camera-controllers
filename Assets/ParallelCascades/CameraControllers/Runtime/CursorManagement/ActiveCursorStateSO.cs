using System;
using UnityEngine;

namespace ParallelCascades.CameraControllers.Runtime.CursorManagement
{
    [CreateAssetMenu(menuName = "Parallel Cascades/Camera Controllers/Active Cursor State SO", order = 0)]
    public class ActiveCursorStateSO : ScriptableObject
    {
        [SerializeField] private CursorStateSO m_cursorState;

        public void SetCursorState(CursorStateSO value)
        {
            if (m_cursorState == value) return;
            m_cursorState = value;
            m_cursorState.Apply();
        }
    }
}