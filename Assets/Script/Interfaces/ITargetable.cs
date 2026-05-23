using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Script.Interfaces
{
    public enum TargetType
    {
        Mob,
        Player,
        NPC,
        Item
    }
        public interface ITargetable
        {
            int GetId();
            string GetTargetName();

            TargetType GetTargetType();
            Transform GetTransform();
            void OnTargeted();
            void OnDeselected();


        }
    }

