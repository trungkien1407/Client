using Assets.Script.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script
{
    public class Class1 : MonoBehaviour
    {
        public Button Button;
        private void Start()
        {
            
        }

        public void btnClick()
        {
          //  MapManager.Instance.LoadMap(3);
            NetworkMobManager.Instance.SpawnMob(1, 1, 1, 1, 1, 1);
            NetworkNpcManager.Instance.SpawnNpc(1, 1, 5, 0);
            
        }
    }
}
