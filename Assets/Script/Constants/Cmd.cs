using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Script.Constants
{
    

    public static class Cmd
    {

        public const short LOGIN = 1;
        public const short REGISTER = 2;
        public const short LOGOUT = 3;
        public const short HEARTBEAT = 4; // Giữ kết nối (Ping)
        public const short CREATE_CHARACTER = 5;

        // ==========================================
        // 2. NHÂN VẬT & DI CHUYỂN (100 - 199)
        // ==========================================
        public const short PLAYER_MOVE = 100;
        public const short ENTER_MAP = 101;     // Ví dụ: Load vào Làng Lá
        public const short CHANGE_MAP = 102;
        public const short FORCE_MOVE = 103;

        

        public const short PLAYER_ADD = 104;    
        public const short PLAYER_REMOVE = 105; 
        public const short PLAYER_LIST = 106;  

        public const short PLAYER_MOVE_BATCH = 107;

        public const short CHANGE_ZONE = 108;
        // ==========================================
        // 3. CHIẾN ĐẤU & KỸ NĂNG (200 - 299)
        // ==========================================
        public const short USE_SKILL = 200;     // Phóng phi tiêu / Chidori
        public const short PLAYER_DIE = 201;
        public const short REVIVE = 202;

        // ==========================================
        // 4. VẬT PHẨM & TÚI ĐỒ (300 - 399)
        // ==========================================
        public const short USE_ITEM = 300;      // Bơm máu
        public const short PICK_ITEM = 301;     // Nhặt đồ trên đất



        public const short CLIENT_READY = 400;



        // ==========================================
        //  QUÁI VẬT
        // ==========================================
        public const short MOB_ADD = 500;
        public const short MOB_LIST = 501;
        public const short MOB_MOVE= 502;
        public const short MOB_DIE = 503;
        public const short MOB_MOVE_BATCH = 504;

    

        // ==========================================
        // 5. CẬP NHẬT TÀI NGUYÊN & KHÁC (600 - ...)
        // ==========================================
        public const short CHECK_VERSION = 600;


        // he thong combat

        public const short PLAYER_TAKE_DAMGE = 900;
        public const short PLAYER_HEAL = 901;



        public const short NPC_LIST = 800;


    }
}
