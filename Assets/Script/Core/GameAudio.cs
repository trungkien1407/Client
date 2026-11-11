using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Script.Core
{
    /// <summary>
    /// NỐI ÂM THANH VÀO SỰ KIỆN GAME — AudioManager chỉ biết "phát file tên X"; lớp này quyết định KHI NÀO phát tiếng nào.
    ///
    ///   Tiếng hiệu ứng: GameAudio.Play(GameAudio.LevelUp) ở chỗ xảy ra sự kiện (CombatNetwork, GameDataNetwork...).
    ///   Nhạc nền: tự chọn theo thứ tự ưu tiên  đứng gần boss > phó bản > map quái > map an toàn (làng / trường — server báo trong MAP_INFO).
    ///   Tiếng bấm nút: bắt MỌI Button / Toggle trên mọi canvas (cả prefab) — không phải gắn tay từng nút.
    ///
    /// File âm thanh: Resources/Audio/&lt;tên&gt; (tên = hằng bên dưới). Thiếu file → im lặng, game vẫn chạy.
    /// Demo nội bộ: Tools → Naruto → Demo art → 3 chép âm thanh rip vào _Demo/Resources/Audio (không commit).
    /// </summary>
    public static class GameAudio
    {
        // ---- Tên tiếng hiệu ứng (SFX) ----
        public const string Click = "click", Hit = "hit", Hurt = "hurt", LevelUp = "level_up", Coin = "coin",
            QuestDone = "quest_done", UpgradeOk = "upgrade_ok", UpgradeFail = "upgrade_fail", Die = "die", BossAppear = "boss_appear";

        // ---- Tên nhạc nền (lặp) ----
        public const string BgmLogin = "bgm_login", BgmVillage = "bgm_village", BgmField = "bgm_field",
            BgmDungeon = "bgm_dungeon", BgmBoss = "bgm_boss";

        /// <summary>Mọi tên file game tìm (DemoArtTool / docs dùng để liệt kê).</summary>
        public static readonly string[] AllKeys =
        {
            Click, Hit, Hurt, LevelUp, Coin, QuestDone, UpgradeOk, UpgradeFail, Die, BossAppear,
            BgmLogin, BgmVillage, BgmField, BgmDungeon, BgmBoss
        };

        /// <summary>Đứng trong bấy nhiêu ô quanh boss thì đổi nhạc boss (boss thường đứng cả map như Cóc Chúa — ở xa vẫn nghe nhạc map).</summary>
        public const float BossRange = 10f;

        private static string _mapMusic = BgmLogin;
        private static bool _dungeon, _boss;

        public static void Play(string key) => AudioManager.Play(key);

        /// <summary>Màn đăng nhập / tải game.</summary>
        public static void LoginScreen() { _mapMusic = BgmLogin; _dungeon = false; _boss = false; Refresh(); }

        /// <summary>Vào map mới (MAP_INFO): map an toàn (làng / trường — có Mộc Nhân tập đánh vẫn là làng) → nhạc làng, còn lại → nhạc bãi luyện.</summary>
        public static void EnterMap(bool safe)
        {
            _mapMusic = safe ? BgmVillage : BgmField;
            _dungeon = false;
            Refresh();
        }

        /// <summary>DUNGEON_STATE: đang chơi phó bản (state 2) → nhạc phó bản.</summary>
        public static void SetDungeon(bool playing) { _dungeon = playing; Refresh(); }

        /// <summary>Boss còn sống cách mình ≤ BossRange ô (Clicker kiểm tra mỗi giây) → nhạc boss + tiếng gầm lúc tới gần.</summary>
        private static void SetBoss(bool alive)
        {
            if (alive == _boss) return;
            _boss = alive;
            if (alive) Play(BossAppear);
            Refresh();
        }

        private static void Refresh() => AudioManager.PlayMusic(_boss ? BgmBoss : _dungeon ? BgmDungeon : _mapMusic);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("[GameAudio]");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Clicker>();
        }

        /// <summary>
        /// Chạy ngầm: (1) chạm / bấm chuột trúng nút đang bật → tiếng "click"; (2) mỗi giây xem có boss trên map không.
        /// Bắt bằng raycast của EventSystem lúc nhấn xuống → không cần sửa prefab nào.
        /// </summary>
        private sealed class Clicker : MonoBehaviour
        {
            private readonly List<RaycastResult> _hits = new List<RaycastResult>();
            private PointerEventData _ped;
            private EventSystem _pedOwner;   // EventSystem mà _ped tạo cho (đổi scene → tạo lại)
            private float _nextBossCheck;

            private void Update()
            {
                if (Input.GetMouseButtonDown(0)) CheckClick(Input.mousePosition);   // cảm ứng: Unity giả lập chuột trái cho ngón đầu

                if (Time.unscaledTime < _nextBossCheck) return;
                _nextBossCheck = Time.unscaledTime + 1f;
                var mobs = Manager.NetworkMobManager.Instance;
                var me = mobs != null ? Manager.NetworkPlayerManager.Instance?.localPlayer : null;
                SetBoss(me != null && mobs.AnyBossNear(me.transform.position, BossRange));
            }

            private void CheckClick(Vector2 pos)
            {
                var es = EventSystem.current;
                if (es == null) return;
                if (_ped == null || _pedOwner != es) { _ped = new PointerEventData(es); _pedOwner = es; }
                _ped.position = pos;
                _hits.Clear();
                es.RaycastAll(_ped, _hits);
                if (_hits.Count == 0) return;
                // Chỉ phần tử trên cùng: nút nằm dưới cửa sổ khác không kêu
                var sel = _hits[0].gameObject.GetComponentInParent<Selectable>();
                if (sel != null && sel.IsInteractable() && (sel is Button || sel is Toggle)) Play(Click);
            }
        }
    }
}
