# =====================================================================================
# ĐO 24 THÚ CƯỠI THEO ĐIỂM ẢNH → bảng số cho Fx/PetMount.cs (Measured: mặt lưng / đỉnh / bề ngang từng bộ thú)
#
# Vì sao đo bằng ảnh: mỗi bộ Spine thú vẽ một cỡ, có mảnh trong suốt + tai / sừng / đuôi / lửa → đo trên khung xương
# (Skeleton.GetBounds) không biết mặt lưng thật ở đâu → người ngồi lơ lửng / thú bé. Ảnh thì thấy đúng cái người chơi thấy.
#
# Cách chạy:
#   1. Build client, server dev đang chạy, rồi chụp:
#        Builds/PC/NinjaOnline.exe -autotest -mountsonly -user clientbot -pass test1234 -shots "D:/Game/Naruto/shots_mount" \
#            -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile <log>
#      → shots_mount/MountPx_NN_<art>.png (ẩn người, chỉ thú, tư thế gốc) + MountPxBg_NN_<art>.png (cùng chỗ, tắt cả thú = nền)
#        + log có dòng "[AutoTest] MOUNTPX tpl= art= feetX= feetY= ppu= scale=" (toạ độ chân, điểm ảnh / đơn vị, tỉ lệ thú lúc chụp)
#   2. python Tools/do_thu_cuoi.py <log> shots_mount
#      → in bảng C# dán vào PetMount.Measured + lưu shots_mount/_do_thu_cuoi.png (vạch đỏ = mặt lưng, vàng = đỉnh) để soát.
# Số in ra tính theo ĐƠN VỊ XƯƠNG của bộ thú (đã nhân tỉ lệ nhập, CHƯA nhân tỉ lệ vẽ) → không phụ thuộc cỡ đang vẽ lúc chụp.
# =====================================================================================
import re
import sys

from PIL import Image, ImageDraw

log, shots = sys.argv[1], sys.argv[2]
rows = []
for line in open(log, encoding='utf-8', errors='ignore'):
    m = re.search(r'MOUNTPX tpl=(\d+) art=(\d+) feetX=([\d.]+) feetY=([\d.]+) ppu=([\d.]+) scale=([\d.]+)', line)
    if m:
        rows.append(tuple(float(g) for g in m.groups()))
out, previews = [], []
for tpl, art, fx, fy, ppu, scale in rows:
    tpl, art = int(tpl), int(art)
    im = Image.open(f'{shots}/MountPx_{tpl:02d}_{art}.png').convert('RGB')
    bg = Image.open(f'{shots}/MountPxBg_{tpl:02d}_{art}.png').convert('RGB')   # cùng chỗ, chỉ tắt hình thú
    W, H = im.size
    px, bgp = im.load(), bg.load()
    x0, x1 = int(max(0, fx - 4 * ppu)), int(min(W, fx + 4 * ppu))
    y0, y1 = int(max(0, fy - 3.5 * ppu)), int(min(H, fy + 0.3 * ppu))

    def solid(x, y):
        a, b = px[x, y], bgp[x, y]
        return max(abs(a[0] - b[0]), abs(a[1] - b[1]), abs(a[2] - b[2])) > 40

    col_top = {}
    for x in range(x0, x1):
        for y in range(y0, y1):
            if solid(x, y) and solid(x, min(y1 - 1, y + 1)) and solid(x, min(y1 - 1, y + 2)):   # 3 điểm liền → bỏ hạt lấp lánh
                col_top[x] = y
                break
    if not col_top:
        print(f'// {tpl} {art}: không thấy hình')
        continue
    xs = sorted(col_top)
    top_px = fy - min(col_top.values())
    width_px = xs[-1] - xs[0]
    band = [col_top[x] for x in range(int(fx - 0.12 * ppu), int(fx + 0.12 * ppu) + 1) if x in col_top]
    back_px = fy - sorted(band)[len(band) // 2] if band else top_px * 0.6
    center_px = (xs[0] + xs[-1]) / 2 - fx
    k = 1.0 / (ppu * scale)
    out.append((tpl, art, back_px * k, top_px * k, width_px * k, center_px * k))
    d = ImageDraw.Draw(im)
    d.line((x0, fy - back_px, x1, fy - back_px), fill=(255, 0, 0), width=2)
    d.line((x0, fy - top_px, x1, fy - top_px), fill=(255, 220, 0), width=1)
    d.line((fx, y0, fx, y1), fill=(0, 160, 255), width=1)
    previews.append(im.crop((x0, y0, x1, y1)).resize((400, int(400 * (y1 - y0) / (x1 - x0)))))

print('        // art → (mặt lưng, đỉnh, bề ngang, tâm hình lệch so với chỗ ngồi) — đơn vị xương; sinh bằng Tools/do_thu_cuoi.py')
for tpl, art, back, top, wide, cen in out:
    print(f'            {{ {art}, new Vector4({back:.3f}f, {top:.3f}f, {wide:.3f}f, {cen:.3f}f) }},   // {tpl}')
if previews:
    cw, ch = previews[0].size
    cols = 6
    sheet = Image.new('RGB', (cols * cw, ((len(previews) + cols - 1) // cols) * ch), 'white')
    for i, p in enumerate(previews):
        sheet.paste(p, ((i % cols) * cw, (i // cols) * ch))
    sheet.save(f'{shots}/_do_thu_cuoi.png')
