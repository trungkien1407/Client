using System;

namespace Assets.Script.Core
{
    /// <summary>
    /// Đánh dấu 1 ô Inspector ĐƯỢC PHÉP để trống.
    /// Tool "Tools/Naruto/2. Kiểm tra Project" quét mọi ô kéo-thả (Object / AssetReference) trong scene;
    /// ô nào trống mà KHÔNG có [Optional] sẽ bị báo "[CẦN ĐIỀN]".
    ///
    /// Ví dụ:  [Optional] public GameObject jumpFXObj;   // chưa có hiệu ứng nhảy thì để trống
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class OptionalAttribute : Attribute { }
}
