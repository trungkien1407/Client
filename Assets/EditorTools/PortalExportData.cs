using UnityEngine;
using Assets.Script.Map; // Đảm bảo đã using namespace chứa PortalDirection

namespace Assets.Script.EditorTools
{
    public class PortalExportData : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Thông số Cổng")]
        public int targetMapId;
        public float targetX;
        public float targetY;
        public int width = 1;
        public int height = 1;

        [Header("Hiển thị")]
        public PortalDirection direction = PortalDirection.Right; 

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0, 1, 0, 0.4f);
            Gizmos.DrawCube(transform.position + new Vector3(width / 2f, height / 2f, 0), new Vector3(width, height, 1));
            UnityEditor.Handles.Label(transform.position + new Vector3(width / 2f, height, 0), $"To Map: {targetMapId}\nDir: {direction}");
        }
#endif
    }
}