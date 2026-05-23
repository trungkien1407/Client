using System;
using System.Collections.Generic;

namespace Assets.Script.Map
{
    public enum PortalDirection
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3
    }

    [Serializable]
    public class MapData
    {
        public int mapId;
        public string mapName;
        public int width;
        public int height;
        public int originX;
        public int originY;

        // Mảng va chạm hợp nhất (0: đi được, 1: đất/tường, 2: nước)
        public byte[][] collisionMap;

        // Danh sách thực thể và cổng dịch chuyển cho Server quản lý
        public List<NpcData> npcs;
        public List<PortalData> portals;
        public List<MobData> monsters;
    }

    [Serializable]
    public class NpcData
    {
        public int templateId;
        public float x;
        public float y;
    }

    [Serializable]
    public class PortalData
    {
        public float x;
        public float y;
        public int width;
        public int height;
        public int targetMapId;
        public float targetX;
        public float targetY;
        public int direction;
    }

    [Serializable]
    public class MobData
    {
        public int templateId;
        public float x;
        public float y;
        public int respawnTime;
    }
}