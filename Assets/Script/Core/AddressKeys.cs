namespace Assets.Script.Core
{
    /// <summary>
    /// TẤT CẢ key Addressables mà CODE gọi bằng chuỗi nằm ở đây (1 chỗ duy nhất).
    /// Đổi tên address trong cửa sổ Addressables Groups thì phải sửa ở đây cho khớp.
    /// Tool "Tools/Naruto/2. Kiểm tra Project" sẽ báo nếu thiếu key nào.
    ///
    /// Những asset gán bằng AssetReference trong Inspector (database, atlas, prefab quái/NPC)
    /// KHÔNG cần key — Addressables tìm theo GUID, đổi address thoải mái.
    /// </summary>
    public static class AddressKeys
    {
        // ---- Group "Core" ----
        public const string BasePlayer = "BasePlayer";          // Prefabs/basePlayer.prefab

        // ---- Group "Characters" ----
        /// <summary>Dữ liệu xương Spine theo hệ: 1 Naruto (Kiếm), 2 Sakura (Tiêu), 3 Sasuke (Hoả).</summary>
        public static string CharacterSkeleton(int classType) => $"Char_Data_{classType}";

        /// <summary>Label gắn cho các NinjaClassData hiện ở màn tạo nhân vật.</summary>
        public const string ClassDataLabel = "ClassData";

        // ---- Group "Maps" ----
        /// <summary>Prefab map: Map_1, Map_2... (mapId do server gửi).</summary>
        public static string Map(int mapId) => $"Map_{MapArt(mapId)}";

        /// <summary>
        /// [CẦN ĐIỀN khi có art] Map chưa có prefab riêng thì mượn hình map khác (cùng khung va chạm bên server):
        ///   3 Hang Ốc Sên (phó bản) → Map_2 · 4 Lôi Đài → Map_2.
        /// Khi có prefab Map_3 / Map_4 trong Addressables (group "Maps") thì XOÁ dòng tương ứng ở đây.
        /// </summary>
        private static int MapArt(int mapId) => mapId switch
        {
            3 => 2,
            4 => 2,
            _ => mapId,
        };
    }
}
