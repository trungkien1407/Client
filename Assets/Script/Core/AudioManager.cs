using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.Core
{
    /// <summary>
    /// ÂM THANH tối giản: phát tiếng hiệu ứng theo tên + nhạc nền, âm lượng lấy từ Cài đặt (PlayerPrefs).
    ///
    ///   AudioManager.Play("upgrade_ok");      // tiếng hiệu ứng (SFX)
    ///   AudioManager.PlayMusic("bgm_village"); // nhạc nền (lặp)
    ///
    /// [CẦN ĐIỀN khi mua âm thanh] đặt file vào Assets/Resources/Audio/ đúng TÊN dưới đây (mp3/ogg/wav đều được):
    ///   upgrade_ok, upgrade_fail, level_up, coin, hit, click, bgm_village, bgm_field, bgm_boss
    /// Chưa có file thì Play() im lặng (không lỗi). Sau này nhiều âm thanh thì chuyển sang Addressables.
    /// </summary>
    public static class AudioManager
    {
        public const string PrefMusic = "vol_music", PrefSfx = "vol_sfx";
        private static AudioSource _sfx, _music;
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

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
            Ensure();
            _sfx.PlayOneShot(clip, SfxVolume);
        }

        public static void PlayMusic(string key)
        {
            var clip = Load(key);
            if (clip == null) return;
            Ensure();
            if (_music.clip == clip && _music.isPlaying) return;
            _music.clip = clip;
            _music.volume = MusicVolume;
            _music.Play();
        }
    }
}
