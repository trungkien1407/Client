#!/usr/bin/env bash
# ============================================================================
# CHẠY UNIT TEST CLIENT (Unity Test Framework, EditMode) bằng dòng lệnh — không cần mở Unity Editor.
#   bash chay_unit_test.sh
# Phải ĐÓNG Unity Editor trước (1 project chỉ mở được 1 Unity). Mất ~1–3 phút (Unity phải biên dịch project).
# Test nằm ở Assets/Editor/Tests/*.cs. Mở bằng giao diện: Unity → Window → General → Test Runner → EditMode → Run All.
# Giải thích unit test là gì, viết test mới thế nào: repo Server docs/hoc/VIET_UNIT_TEST.md §10
# ============================================================================
set -uo pipefail
cd "$(dirname "$0")"
UNITY="${UNITY:-/d/Du Lieu/2022.3.62f3/Editor/Unity.exe}"
OUT="$(pwd)/Logs"
mkdir -p "$OUT"
echo "==> Chạy EditMode test (log: Logs/unit_test.log)..."
"$UNITY" -batchmode -projectPath "$(pwd -W 2>/dev/null || pwd)" -runTests -testPlatform EditMode \
  -testResults "$OUT/unit_test.xml" -logFile "$OUT/unit_test.log"
code=$?
if grep -q "error CS" "$OUT/unit_test.log"; then
  echo "LỖI BIÊN DỊCH (code C# sai) — xem Logs/unit_test.log:"; grep "error CS" "$OUT/unit_test.log" | head -10; exit 1
fi
# Kết quả nằm ở thẻ <test-run ... total= passed= failed=> của file XML
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' "$OUT/unit_test.xml" | head -1
grep -o '<test-case [^>]*result="Failed"[^>]*' "$OUT/unit_test.xml" | grep -o 'fullname="[^"]*"' | sed 's/^/  HỎNG: /'
[ $code -eq 0 ] && echo "==> TẤT CẢ ĐỀU QUA" || echo "==> CÓ CA HỎNG (mở Logs/unit_test.xml hoặc Test Runner trong Unity để xem lý do)"
exit $code
