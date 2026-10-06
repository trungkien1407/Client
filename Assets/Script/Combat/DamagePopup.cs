using TMPro;
using UnityEngine;

namespace Assets.Script.Combat
{
    /// <summary>
    /// Chữ bay lên rồi mờ dần (số sát thương, "LEVEL UP"...). Tạo hoàn toàn bằng code,
    /// không cần prefab: DamagePopup.Show(vịTrí, "-95", Color.yellow).
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        private const float LifeTime = 0.9f;
        private const float RiseSpeed = 1.6f;

        private TextMeshPro _text;
        private float _age;
        private Color _baseColor;

        public static void Show(Vector3 worldPos, string message, Color color, float size = 5f)
        {
            var go = new GameObject("DamagePopup");
            // Lệch ngang ngẫu nhiên chút để nhiều số liên tiếp không đè lên nhau
            go.transform.position = worldPos + new Vector3(Random.Range(-0.3f, 0.3f), 1.9f, 0f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = message;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;
            tmp.sortingOrder = 100; // vẽ đè lên nhân vật/quái

            var popup = go.AddComponent<DamagePopup>();
            popup._text = tmp;
            popup._baseColor = color;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

            float t = _age / LifeTime;
            var c = _baseColor;
            c.a = 1f - t;
            _text.color = c;

            if (_age >= LifeTime) Destroy(gameObject);
        }
    }
}
