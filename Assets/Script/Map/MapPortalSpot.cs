using UnityEngine;

namespace Assets.Script.Map
{
    /// <summary>
    /// Cổng sang map khác. Người chơi đứng trong khung tâm = vị trí cổng, rộng size + 1 ô mỗi bên (vẽ màu tím)
    /// → sang map {targetMapId}, đứng ở {target}.
    /// {target} nên cách cổng bên kia vài ô, không thì vừa sang đã chạm cổng quay về.
    /// </summary>
    public class MapPortalSpot : MonoBehaviour
    {
        public int targetMapId;
        public Vector2 target;
        public Vector2 size = Vector2.one;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position, new Vector3(size.x + 2f, size.y + 2f));   // khớp server Portal.isInside
        }
    }
}
