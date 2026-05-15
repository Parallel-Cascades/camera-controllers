using System;
using UnityEngine;

namespace ParallelCascades.CameraControllers.Runtime.CursorManagement
{
    [CreateAssetMenu(menuName = "Parallel Cascades/Camera Controllers/Edge Scroll Cursor State SO", order = 0)]
    public class DirectionalCursorStateSO : CursorStateSO
    {
        [Tooltip("Directions are S, N, E, W, SE, SW, NE, NW")]
        [SerializeField] private Texture2D[] m_cursorsArray;
        private int m_currentDirectionIndex = 0;

        [Flags]
        public enum EdgeScrollCursorDirection
        {
            S = 1, N = 2, E = 4, W = 8, SE = 5, SW = 9, NE = 6, NW = 10
        }

        private struct CursorData
        {
            public Texture2D Texture; // null if no tint applied
            public Vector2 Hotspot;
        }

        // CW rotation angles in screen-space (y-down) from S for each direction index:
        // 0=S, 1=N, 2=E, 3=W, 4=SE, 5=SW, 6=NE, 7=NW
        private static readonly float[] s_directionAngles =
        {
              0f,  // S
            180f,  // N
            -90f,  // E
             90f,  // W
            -45f,  // SE
             45f,  // SW
           -135f,  // NE
            135f,  // NW
        };

        public void SetDirection(EdgeScrollCursorDirection direction)
        {
            int newIndex = direction switch
            {
                EdgeScrollCursorDirection.S  => 0,
                EdgeScrollCursorDirection.N  => 1,
                EdgeScrollCursorDirection.E  => 2,
                EdgeScrollCursorDirection.W  => 3,
                EdgeScrollCursorDirection.SE => 4,
                EdgeScrollCursorDirection.SW => 5,
                EdgeScrollCursorDirection.NE => 6,
                EdgeScrollCursorDirection.NW => 7,
                _                            => 0
            };
            if (m_currentDirectionIndex == newIndex) return;
            m_currentDirectionIndex = newIndex;
            Apply();
        }

        public override Texture2D GetCursor()
        {
            if (m_cursorData != null)
                return m_cursorData[m_currentDirectionIndex].Texture ?? m_cursorsArray[m_currentDirectionIndex];
            return m_cursorsArray[m_currentDirectionIndex];
        }

        public override Vector2 Hotspot =>
            m_cursorData != null ? m_cursorData[m_currentDirectionIndex].Hotspot : base.Hotspot;

        private CursorData[] m_cursorData;

        /// <summary>
        /// Rotates a hotspot point around the texture centre by <paramref name="angleDeg"/> degrees
        /// (clockwise in screen-space / Unity cursor coordinates where y increases downward).
        /// </summary>
        private static Vector2 RotateHotspot(Vector2 hotspot, int width, int height, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float cx = (width  - 1) * 0.5f;
            float cy = (height - 1) * 0.5f;
            float dx = hotspot.x - cx;
            float dy = hotspot.y - cy;
            return new Vector2(
                Mathf.Round(cx + dx * cos - dy * sin),
                Mathf.Round(cy + dx * sin + dy * cos)
            );
        }

        private void BuildCursorData()
        {
            if (m_cursorsArray == null) return;
            Vector2 baseHotspot = base.Hotspot; // S-direction hotspot (serialised on base)
            m_cursorData = new CursorData[m_cursorsArray.Length];
            for (int i = 0; i < m_cursorsArray.Length; i++)
            {
                var tex = m_cursorsArray[i];

                float angle = i < s_directionAngles.Length ? s_directionAngles[i] : 0f;
                Vector2 hotspot = tex != null
                    ? RotateHotspot(baseHotspot, tex.width, tex.height, angle)
                    : baseHotspot;
                m_cursorData[i] = new CursorData { Texture = tex, Hotspot = hotspot };
            }
        }

        private void OnEnable() => BuildCursorData();

#if UNITY_EDITOR
        private void OnValidate() => BuildCursorData();
#endif
    }
}