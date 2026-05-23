using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Script.EditorTools
{
    public class MonsterExportData : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Thông số MOB")]
        public int templateId = 1;
        private int width = 1;
        private int height = 1;
        public int respawnTime = 5000;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0, 1, 0, 0.4f); // Màu xanh lá trong suốt
            Gizmos.DrawCube(transform.position + new Vector3(width / 2f, height / 2f, 0), new Vector3(width, height, 1));
        }
#endif
    }
}
