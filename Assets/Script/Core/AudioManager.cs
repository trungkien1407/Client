using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.Core
{
    /// <summary>
    /// ÂM THANH tối giản: phát tiếng hiệu ứng theo tên + nhạc nền, âm lượng lấy từ Cài đặt (PlayerPrefs).
    ///
    ///   AudioManager.Play("upgrade_ok");      // tiếng hiệu ứng (SFX)
    ///   AudioManager.PlayMusic("bgm_village"); // nhạc nền (lặp)
    /// KHI NÀO phát tiếng nào: GameAudio.cs (danh sách tên ở GameAudio.AllKeys).
    ///
    /// [CẦN ĐIỀN khi mua âm thanh] đặt file vào Assets/Resources/Audio/ đúng TÊN trong GameAudio.AllKeys (mp3/ogg/wav đều được).
    /// Chưa có file thì Play() im lặng (không lỗi); thiếu nhạc của map mới thì giữ nhạc đang phát. Nhiều âm thanh thì chuyển sang Addressables.
    /// </summary>
    public static class AudioManager
    {
        public const string PrefMusic = "vol_music", PrefSfx = "vol_sfx";
        private static AudioSource _sfx, _music;
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();
        private static readonly Dictionary<string, float> LastPlay = new Dictionary<string, float>();

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(PrefMusic, 0.6f);
            set { PlayerPrefs.SetFloat(PrefMusic, value); if (_music != null) _music.volume = value; }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(PrefSfx, 0.8f);
            set => PlayerPrefs.SetFloat(PrefSfx, value);
        }

        private static void Ensure()
        {
            if (_sfx != null) return;
            var go = new GameObject("[Audio]");
            Object.DontDestroyOnLoad(go);
            _sfx = go.AddComponent<AudioSource>();
            _music = go.AddComponent<AudioSource>();
            _music.loop = true;
            _music.volume = MusicVolume;
        }

        private static AudioClip Load(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (!Cache.TryGetValue(key, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + key); // null nếu chưa có file → im lặng
                Cache[key] = clip;
            }
            return clip;
        }

        public static void Play(string key)
        {
            var clip = Load(key);
            if (clip == null) return;
            // Cùng 1 tiếng trong 60 ms (nhiều gói về cùng khung hình) → phát 1 lần cho khỏi chói tai
            float now = Time.unscaledTime;
            if (LastPlay.TryGetValue(key, out float last) && now - last < 0.06f) return;
            LastPlay[key] = now;
            Ensure();
            _sfx.PlayOneShot(clip, SfxVolume);
        }

        /// <summary>Tên nhạc nền đang phát (autotest kiểm đổi nhạc theo map); null = chưa phát gì.</summary>
        public static string CurrentMusic { get; private set; }

        /// <summary>Có file âm thanh tên {key} không.</summary>
        public static bool Has(string key) => Load(key) != null;

        public static void PlayMusic(string key)
        {
            var clip = Load(key);
            if (clip == null) return;
            CurrentMusic = key;
            Ensure();
            if (_music.clip == clip && _music.isPlaying) return;
            _music.clip = clip;
            _music.volume = MusicVolume;
            _music.Play();
        }
    }
}
