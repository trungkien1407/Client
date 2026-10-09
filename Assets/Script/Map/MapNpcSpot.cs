using UnityEngine;

namespace Assets.Script.Map
{
    /// <summary>Chỗ đứng 1 NPC (vị trí = chân NPC). templateId = id NPC trong DB (npc_template).</summary>
    public class MapNpcSpot : MonoBehaviour
    {
        public int templateId;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.75f, new Vector3(0.8f, 1.5f));
        }
    }
}
