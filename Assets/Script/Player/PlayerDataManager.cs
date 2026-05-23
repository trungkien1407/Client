using Assets.Script.Constants;
using Assets.Script.Manager;
using Assets.Script.Models;
using Assets.Script.Network;
using Assets.Script.UI;
using UnityEngine;
using Assets.Script.Interfaces; // Thêm thư viện này để gọi UpdateHp

namespace Assets.Script.Player
{
    public class PlayerDataManager : MonoBehaviour
    {
        public static PlayerDataManager Instance { get; private set; }

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            NetworkEventDispatcher.Instance.AddHandler(Cmd.PLAYER_HEAL, OnPlayerHeal);

            // Nhớ bỏ comment dòng này khi Server đã code xong vụ gửi Damage nhé
            //NetworkEventDispatcher.Instance.AddHandler(Cmd.PLAYER_TAKE_DAMAGE, OnPlayerTakeDamage);
        }

        private void OnPlayerTakeDamage(byte[] data)
        {
            MessageReader reader = new MessageReader(data);
            int playerId = reader.ReadInt();
            int amount = reader.ReadInt();
            int currenthp = reader.ReadInt();
            int maxhp = reader.ReadInt();
            byte currentmp = reader.ReadByte(); // Cẩn thận: Trên kia bạn đọc MP là Int, ở đây lại là Byte? Hãy chắc chắn khớp với Server nhé.
            byte maxmp = reader.ReadByte();
            reader.Cleanup();

            UpdateHealthLogic(playerId, currenthp, maxhp, currentmp, maxmp, -amount, 0);
        }

        private void OnPlayerHeal(byte[] data)
        {
            MessageReader reader = new MessageReader(data);
            int playerId = reader.ReadInt();
            int hpheal = reader.ReadInt();
            int mpheal = reader.ReadInt();
            int currenthp = reader.ReadInt();
            int maxhp = reader.ReadInt();
            int currentmp = reader.ReadInt();
            int maxmp = reader.ReadInt();
            reader.Cleanup();

            UpdateHealthLogic(playerId, currenthp, maxhp, currentmp, maxmp, hpheal, mpheal);
        }

        // Tạo 1 hàm chung để xử lý cả Bơm máu lẫn Mất máu cho gọn code
        private void UpdateHealthLogic(int playerId, int currentHp, int maxHp, int currentMp, int maxMp, int hpChangeAmount, int mpChangeAmount)
        {
            Debug.Log($"[1] Nhận data từ Server - PlayerID: {playerId} | Máu mới: {currentHp}/{maxHp}");

            // 1. NẾU LÀ CHÍNH MÌNH
            if (playerId == NetworkPlayerManager.Instance.myPlayerId)
            {
                // Cập nhật UI góc trái màn hình
                if (UISetup.Instance != null)
                {
                    UISetup.Instance.SetupHealth(currentHp, maxHp, currentMp, maxMp);
                }
                // Hiện chữ nhảy lên đầu
                PlayerVisualController myVisual = GetPlayerVisualController(playerId);
                if (myVisual != null)
                {
                    if (hpChangeAmount > 0)  /* myVisual.ShowFloatingText($"+{hpChangeAmount}", Color.green); */;
                    else if (hpChangeAmount < 0) /* myVisual.ShowFloatingText($"{hpChangeAmount}", Color.red); */ ;

                    if (mpChangeAmount > 0) /* myVisual.ShowFloatingText($"+{mpChangeAmount}", Color.blue); */ ;
                }
            }
            // 2. NẾU LÀ NGƯỜI CHƠI KHÁC (REMOTE PLAYER)
            else
            {
                // Lấy data của thằng kia ra
                RemotePlayer rp = NetworkPlayerManager.Instance.GetRemotePlayer(playerId);
                if (rp != null)
                {
                    // [QUAN TRỌNG NHẤT] Dòng này sẽ làm thanh máu trên đầu và khung Target UI tự động giật theo!
                    rp.UpdateHp(currentHp);

                    // Hiện chữ nhảy lên đầu
                    PlayerVisualController targetVisual = rp.GetComponent<PlayerVisualController>();
                    if (targetVisual != null)
                    {
                        if (hpChangeAmount > 0) /* targetVisual.ShowFloatingText($"+{hpChangeAmount}", Color.green); */ ;
                        else if (hpChangeAmount < 0) /* targetVisual.ShowFloatingText($"{hpChangeAmount}", Color.red); */ ;
                    }
                }
            }
        }

        // Hoàn thiện hàm hỗ trợ tìm PlayerVisualController
        private PlayerVisualController GetPlayerVisualController(int id)
        {
            if (id == NetworkPlayerManager.Instance.myPlayerId)
            {
                if (NetworkPlayerManager.Instance.localPlayer != null)
                    return NetworkPlayerManager.Instance.localPlayer.GetComponent<PlayerVisualController>();
            }
            else
            {
                RemotePlayer rp = NetworkPlayerManager.Instance.GetRemotePlayer(id);
                if (rp != null) return rp.GetComponent<PlayerVisualController>();
            }
            return null;
        }

        private void OnDestroy()
        {
            if (NetworkEventDispatcher.Instance != null)
            {
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.PLAYER_HEAL, OnPlayerHeal);
                //NetworkEventDispatcher.Instance.RemoveHandler(Cmd.PLAYER_TAKE_DAMAGE, OnPlayerTakeDamage);
            }
        }
    }
}