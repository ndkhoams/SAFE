# SAFE 22 – Plugin tính Từ biến (Creep) & Co ngót (Shrinkage) theo EC2

Plugin cho **CSI SAFE v22** giúp:

1. Tính **hệ số từ biến** `φ(∞, t₀)` và **biến dạng co ngót tổng** `ε_cs` theo **EN 1992-1-1 (Eurocode 2)** – mục 3.1.4 và Phụ lục B.
2. Liệt kê các **load case long-term** trong mô hình SAFE và **tự động ghi** giá trị Creep Coefficient / Shrinkage Strain / Aging Coefficient vào các case được chọn (xem mục *Giới hạn OAPI* bên dưới).

> Kết quả công thức đã được đối chiếu khớp với eurocodeapplied.com.

## 1. Cơ sở lý thuyết

### Hệ số từ biến (Annex B)
- `f_cm = f_ck + 8`
- Kích thước quy ước: `h₀ = 2·A_c / u`
- Tuổi hiệu chỉnh nhiệt độ (B.10): `t_T = exp(−(4000/(273+T) − 13.65))·t₀`
- Tuổi hiệu chỉnh loại xi măng (B.9): `t₀ = t_T·(9/(2 + t_T^1.2) + 1)^α ≥ 0.5` với α = −1 (S), 0 (N), +1 (R)
- `β(f_cm) = 16.8/√f_cm`, `β(t₀) = 1/(0.1 + t₀^0.20)`
- `φ_RH` phụ thuộc `f_cm` so với 35 MPa (có hệ số α₁, α₂ khi f_cm > 35)
- `φ₀ = φ_RH · β(f_cm) · β(t₀)`; với `t → ∞`: `β_c → 1` nên `φ(∞,t₀) = φ₀`

### Co ngót (3.1.4)
- Co ngót khô: `ε_cd,0 = 0.85·[(220 + 110·α_ds1)·exp(−α_ds2·f_cm/10)]·10⁻⁶·β_RH`, `β_RH = 1.55·[1 − (RH/100)³]`
- `k_h` nội suy theo `h₀`: (100→1.0), (200→0.85), (300→0.75), (≥500→0.70)
- Co ngót tự sinh: `ε_ca(∞) = 2.5·(f_ck − 10)·10⁻⁶`
- Tổng: `ε_cs = ε_cd + ε_ca`

### Ví dụ kiểm chứng
f_ck=25, A_c=0.22 m², u=2.0 m, RH=75%, t₀=28 ngày, T=30°C, xi măng loại N →
**φ(∞,t₀) ≈ 1.854**, **ε_cs ≈ 31.83×10⁻⁵** (khớp eurocodeapplied.com).

## 2. Cấu trúc dự án
```
SafeCreepShrinkagePlugin/
├─ SafeCreepShrinkagePlugin.csproj   # .NET Framework 4.8 class library (WinForms)
├─ cPlugin.cs                        # Điểm vào plugin (SAFEv1.cPluginContract)
├─ EC2CreepShrinkage.cs              # Engine tính toán EC2 (thuần, không phụ thuộc SAFE)
├─ SafeModelHelper.cs                # Đọc load case & ghi giá trị qua OAPI
└─ MainForm.cs                       # Giao diện nhập liệu / kết quả / chọn load case
```

## 3. Build
1. Mở bằng Visual Studio 2022.
2. Plugin cần tham chiếu thư viện OAPI của SAFE. Mặc định csproj trỏ tới:
   `C:\Program Files\Computers and Structures\SAFE 22\SAFEv1.dll`
   Nếu SAFE cài ở nơi khác, sửa property `SafeApiPath` trong `.csproj` hoặc build bằng:
   `msbuild /p:SafeApiPath="D:\...\SAFEv1.dll"`
3. Build cấu hình **Release** → ra `SafeCreepShrinkagePlugin.dll`.

> Nếu dùng thư viện CSI API liên sản phẩm (.NET 8 / `CSiAPIv1`), đổi `using SAFEv1;` thành `using CSiAPIv1;`.

## 4. Cài vào SAFE 22
1. Trong SAFE: **Tools ▸ Add/Show Plugins**.
2. Thêm plugin mới, trỏ tới `SafeCreepShrinkagePlugin.dll`.
3. Nhập tên lớp đầy đủ: `SafeCreepShrinkagePlugin.cPlugin`.
4. Sau khi thêm, chạy plugin từ menu **Tools**.

## 5. Sử dụng
1. Nhập f_ck, A_c, u (chu vi tiếp xúc khô), RH, t₀, nhiệt độ T, t_s, loại xi măng.
2. Bấm **Tính** → xem `φ(∞,t₀)`, `ε_cd`, `ε_ca`, `ε_cs` và các giá trị trung gian.
3. Tích chọn các load case long-term cần áp (các case có tên gợi ý long/term/creep/crack được tự tích sẵn).
4. Bấm **Áp vào load case** → plugin ghi Creep / Shrinkage / Aging vào các case được chọn.

## 6. Giới hạn OAPI (quan trọng)
OAPI của SAFE **không phải lúc nào cũng phơi bày** tham số cracking long-term (creep/shrinkage/aging) để ghi tự động. Vì vậy hàm áp giá trị được viết theo cơ chế *best-effort* bằng reflection: nó dò các phương thức `Set...Crack/Creep/Shrink/LongTerm` trên đối tượng `LoadCases` và gọi nếu chữ ký phù hợp, đồng thời ghi log chi tiết.

Nếu phiên bản SAFE của bạn không hỗ trợ ghi qua OAPI, plugin vẫn hiển thị đầy đủ giá trị và nút **Copy** để bạn dán tay vào hộp thoại *Load Case Data ▸ Nonlinear (Long Term Cracked)*.
