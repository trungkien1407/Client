using UnityEngine;

namespace Assets.Script.Map
{
    /// <summary>
    /// Chỗ sinh 1 con quái (vị trí = chân quái; quái đi tuần quanh đây nên mặt đất ±3 ô nên bằng phẳng).
    /// templateId = id quái trong DB (monster_template). respawnTime: mili giây hồi sinh, 0 = không hồi (phó bản).
    /// </summary>
    public class MapMobSpot : MonoBehaviour
    {
        public int templateId;
        public int respawnTime = 7000;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.4f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(1f, 1f));
            Gizmos.DrawLine(transform.position + Vector3.left * 3, transform.position + Vector3.right * 3);
        }
    }
}
