using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.Network
{
    /// <summary>
    /// LỚP NỀN cho mọi script cần nghe gói tin từ server.
    ///
    /// Thay vì tự AddHandler trong Start + nhớ RemoveHandler trong OnDestroy (quên là bị gọi vào
    /// object đã huỷ → lỗi), kế thừa lớp này và gọi Listen(...) — lớp nền tự gỡ khi object bị huỷ.
    ///
    ///   public class MyManager : NetworkListener
    ///   {
    ///       protected override void RegisterHandlers()
    ///       {
    ///           Listen(Cmd.MOB_DIE, OnMobDie);
    ///       }
    ///       private void OnMobDie(byte[] data) { ... }
    ///   }
    ///
    /// Nếu NetworkEventDispatcher chưa sẵn (thứ tự Awake/Start khác nhau) thì lớp nền tự thử lại mỗi frame.
    /// </summary>
    public abstract class NetworkListener : MonoBehaviour
    {
        private readonly List<KeyValuePair<short, Action<byte[]>>> _handlers = new List<KeyValuePair<short, Action<byte[]>>>();
        private bool _registered;

        /// <summary>Gọi Listen(cmd, hàm) cho từng gói muốn nghe. Chỉ chạy 1 lần.</summary>
        protected abstract void RegisterHandlers();

        protected void Listen(short cmd, Action<byte[]> handler)
        {
            NetworkEventDispatcher.Instance.AddHandler(cmd, handler);
            _handlers.Add(new KeyValuePair<short, Action<byte[]>>(cmd, handler));
        }

        /// <summary>Lớp con có Update riêng thì nhớ gọi base.Update() (hoặc gọi EnsureRegistered()).</summary>
        protected virtual void Update() => EnsureRegistered();

        protected void EnsureRegistered()
        {
            if (_registered || NetworkEventDispatcher.Instance == null) return;
            _registered = true;
            RegisterHandlers();
        }

        protected virtual void OnDestroy()
        {
            var d = NetworkEventDispatcher.Instance;
            if (d != null)
                foreach (var kv in _handlers) d.RemoveHandler(kv.Key, kv.Value);
            _handlers.Clear();
        }
    }
}
