using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New Random Water Tile", menuName = "2D/Tiles/RandomWaterTile")]
public class RandomWaterTile : TileBase
{
    [Header("Animation Settings")]
    public Sprite[] m_AnimatedSprites;
    public float m_AnimationSpeed = 1f;

    // BẮT BUỘC: Giúp Tile hiển thị đúng trên Tile Palette và Scene lúc chưa chạy animation
    public override void GetTileData(Vector3Int location, ITilemap tilemap, ref TileData tileData)
    {
        if (m_AnimatedSprites != null && m_AnimatedSprites.Length > 0)
        {
            // Lấy frame đầu tiên làm hình ảnh đại diện mặc định
            tileData.sprite = m_AnimatedSprites[0];
        }
    }

    // Xử lý hoạt ảnh lệch nhịp
    public override bool GetTileAnimationData(Vector3Int location, ITilemap tilemap, ref TileAnimationData tileAnimationData)
    {
        if (m_AnimatedSprites == null || m_AnimatedSprites.Length == 0)
        {
            return false;
        }

        tileAnimationData.animatedSprites = m_AnimatedSprites;
        tileAnimationData.animationSpeed = m_AnimationSpeed;

        // Dùng GetHashCode của X và Y để tạo ra một "seed" ngẫu nhiên nhưng cố định cho ô đó
        System.Random prng = new System.Random(location.x.GetHashCode() ^ location.y.GetHashCode());

        // Random từ 0 đến 10 giây để chệch nhịp hoàn toàn, mất trật tự
        float randomOffset = (float)prng.NextDouble() * 10f;

        tileAnimationData.animationStartTime = randomOffset;

        return true;
    }
}