using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Assets.Script.Combat
{
    /// <summary>
    /// Chữ bay lên rồi mờ dần (số sát thương, "LEVEL UP"...). Tạo hoàn toàn bằng code,
    /// không cần prefab: DamagePopup.Show(vịTrí, "-95", Color.yellow).
    /// Dùng lại đối tượng (pool): hết giờ thì ẩn + cất vào kho thay vì Destroy — trước đây mỗi số sát thương tạo 1 TextMeshPro mới
    /// (chiêu diện rộng 8 quái = 8 lần tạo trong 1 khung hình). Tối đa MaxActive chữ cùng lúc, quá thì bỏ chữ cũ nhất.
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        private const float LifeTime = 0.9f;
        private const float RiseSpeed = 1.6f;
        private const int MaxActive = 40;

        private static readonly Stack<DamagePopup> Pool = new Stack<DamagePopup>();
        private static readonly LinkedList<DamagePopup> Active = new LinkedList<DamagePopup>();

        private TextMeshPro _text;
        private float _age;
        private Color _baseColor;
        private LinkedListNode<DamagePopup> _node;

        public static void Show(Vector3 worldPos, string message, Color color, float size = 5f)
        {
            if (Active.Count >= MaxActive) Active.First.Value.Release();   // quá nhiều chữ: bỏ chữ cũ nhất
            DamagePopup popup = null;
            while (Pool.Count > 0 && popup == null) popup = Pool.Pop();     // đối tượng bị huỷ theo scene → bỏ qua
            if (popup == null) popup = Create();

            // Lệch ngang ngẫu nhiên chút để nhiều số liên tiếp không đè lên nhau
            popup.transform.position = worldPos + new Vector3(Random.Range(-0.3f, 0.3f), 1.9f, 0f);
            var tmp = popup._text;
            tmp.text = message;
            tmp.fontSize = size;
            tmp.color = color;
            popup._baseColor = color;
            popup._age = 0f;
            popup.gameObject.SetActive(true);
            popup._node = Active.AddLast(popup);
        }

        private static DamagePopup Create()
        {
            var go = new GameObject("DamagePopup");
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;
            tmp.sortingOrder = 100; // vẽ đè lên nhân vật/quái
            var popup = go.AddComponent<DamagePopup>();
            popup._text = tmp;
            return popup;
        }

        /// <summary>Hết giờ: ẩn và cất vào kho để lần sau dùng lại.</summary>
        private void Release()
        {
            if (_node != null) { Active.Remove(_node); _node = null; }
            gameObject.SetActive(false);
            Pool.Push(this);
        }

        private void OnDestroy()
        {
            // Bị huỷ theo scene (đổi map): gỡ khỏi danh sách đang chạy; bản trong Pool là null → Show tự bỏ qua
            if (_node != null) { Active.Remove(_node); _node = null; }
        }

        private void Update()
        {
            _age += Time.deltaTime;
            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

            float t = _age / LifeTime;
            var c = _baseColor;
            c.a = 1f - t;
            _text.color = c;

            if (_age >= LifeTime) Release();
        }
    }
}
