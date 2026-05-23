using UnityEngine;

namespace Assets.Script.EditorTools
{
    public class NpcExportData : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Thông số NPC")]
        public int templateId = 1;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, $"NPC ID: {templateId}");
        }
#endif
    }
}