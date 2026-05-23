using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.EditorTools
{
    internal class ObjectExportData : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Thông số Object")]
        public int templateId = 1;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
          //  UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, $"NPC ID: {templateId}");
        }
#endif
    }
}
