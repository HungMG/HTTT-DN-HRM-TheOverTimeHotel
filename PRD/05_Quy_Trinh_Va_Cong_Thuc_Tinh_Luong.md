# PRD 05: Quy Trình & Công Thức Tính Lương Khách Sạn 2 Tầng (Gross → Net)

## 1. Công Thức Tính Lương Chuẩn Khách Sạn

Thu nhập của nhân sự tại The OverTime Hotel được tính toán tự động qua công thức 2 tầng minh bạch:

$$\text{Lương Thực Nhận (Net)} = \text{Tổng Thu Nhập (Gross)} - \text{Tổng Khấu Trừ}$$

---

### Tầng 1: Tổng Thu Nhập (Gross)

$$\text{Gross} = \text{Lương Ngày Công} + \text{Tiền Tăng Ca (OT)} + \text{Phụ Cấp Ca Đêm} + \text{Phụ Cấp Ăn Ca} + \text{Tiền Thưởng}$$

1. **Lương Ngày Công Thực Tế:**
   $$\text{Lương Ngày Công} = \left(\frac{\text{Lương Cơ Bản theo HĐLĐ}}{26}\right) \times \text{Số Ngày Công Thực Tế}$$
   *(26 là số ngày công chuẩn trong tháng; ngày công thực tế tính từ số ca check-in hợp lệ + ngày nghỉ phép hưởng lương được duyệt).*

2. **Tiền Tăng Ca (Overtime - OT):**
   $$\text{Tiền OT} = \left(\frac{\text{Lương Cơ Bản}}{26 \times 8}\right) \times \text{Số Giờ OT} \times \text{Hệ Số OT}$$
   - Ngày làm việc bình thường: Hệ số = **1.5** (150%).
   - Ngày nghỉ cuối tuần: Hệ số = **2.0** (200%).
   - Ngày Lễ, Tết: Hệ số = **3.0** (300%).

3. **Phụ Cấp Ca Đêm (Theo Điều 98 Bộ luật Lao động):**
   $$\text{Phụ Cấp Ca Đêm} = \left(\frac{\text{Lương Cơ Bản}}{26 \times 8}\right) \times \text{Số Giờ Làm Việc Ca Đêm} \times 0.30$$
   *(Cộng thêm ít nhất 30% tiền lương tính theo đơn giá tiền lương của ngày làm việc bình thường).*

4. **Phụ Cấp Ăn Ca & Tiền Thưởng:**
   - Phụ cấp ăn ca: 30.000 VNĐ / ngày công thực tế có mặt tại khách sạn.
   - Tiền thưởng: Thưởng doanh thu buồng phòng, thưởng phục vụ tiệc cưới sự kiện hoặc tiền thưởng hiệu quả công việc (KPI).

---

### Tầng 2: Tổng Khấu Trừ

$$\text{Tổng Khấu Trừ} = \text{Bảo Hiểm Bắt Buộc (10.5%)} + \text{Tiền Phạt Vi Phạm} + \text{Thuế TNCN}$$

1. **Bảo Hiểm Bắt Buộc (Trừ vào lương người lao động 10.5%):**
   - Bảo hiểm xã hội (BHXH): **8.0%** tính trên lương cơ bản.
   - Bảo hiểm y tế (BHYT): **1.5%** tính trên lương cơ bản.
   - Bảo hiểm thất nghiệp (BHTN): **1.0%** tính trên lương cơ bản.
   - $\text{Tổng Trừ BH} = \text{Lương Cơ Bản} \times 10.5\%$.

2. **Tiền Phạt Kỷ Luật Lao Động (Tự động từ dữ liệu Chấm công):**
   - Đi trễ từ 1 đến 15 phút: 50.000 VNĐ / lần.
   - Đi trễ từ 16 đến 30 phút: 100.000 VNĐ / lần.
   - Vắng mặt không phép (Bỏ ca): Trừ toàn bộ lương ngày công đó + phạt 200.000 VNĐ.

3. **Thuế Thu Nhập Cá Nhân (TNCN):**
   - Khấu trừ theo biểu thuế lũy tiến từng phần của Nhà nước sau khi trừ gia cảnh bản thân (11.000.000 VNĐ/tháng) và người phụ thuộc (4.400.000 VNĐ/tháng/người). *(Mức giảm trừ này là của Luật Thuế TNCN 07/2007/QH12 — xem cảnh báo tại mục 3.2, **cần xác minh** theo luật đang hiệu lực.)*

#### 3.1. Trình tự tính thuế TNCN (thứ tự bắt buộc)

$$\text{Thu nhập chịu thuế} = \text{Tổng Gross (Tầng 1)} - \text{Các khoản không chịu thuế}$$

$$\text{Thu nhập tính thuế} = \text{Thu nhập chịu thuế} - \text{Giảm trừ bản thân} - (\text{Giảm trừ người phụ thuộc} \times \text{Số người phụ thuộc})$$

$$\text{Tiền thuế TNCN} = \sum_{\text{bậc } i} \left(\text{Phần thu nhập tính thuế thuộc bậc } i \times \text{Thuế suất}_i\right)$$

- Nếu $\text{Thu nhập tính thuế} \leq 0$ → **Tiền thuế = 0** (không phát sinh nghĩa vụ nộp, không cấp âm).
- **Phụ cấp ăn ca (30.000đ/ngày) là khoản không chịu thuế** — đã được cờ `chiu_thue_tncn = FALSE` trong bảng `CAU_HINH_PHU_CAP`. Vì vậy khoản này **vẫn cộng vào Gross** (mục 4 Tầng 1) nhưng **không đưa vào Thu nhập chịu thuế**. Đây là lý do dòng "Phụ cấp ăn ca" phải in trên phiếu lương ở bảng Thu Nhập chứ không nằm ở bảng Khấu Trừ.
- Các khoản cộng/trừ vào thu nhập chịu thuế ngoài các bảo hiểm bắt buộc (vd: phụ cấp trách nhiệm quản lý, phụ cấp điện thoại — cờ `chiu_thue_tncn = TRUE`) **chưa chốt cụ thể, cần xác minh**.

#### 3.2. Bảng thuế suất TNCN lũy tiến từng phần

> ⚠️ **CẢNH BÁO — BẢNG NÀY CẦN XÁC MINH TRƯỚC KHI ĐƯA VÀO CODE.**
> PRD hiện đang dùng mức giảm trừ **11.000.000đ/tháng** (bản thân) và **4.400.000đ/tháng/người** (người phụ thuộc). Đây là mức giảm trừ của **Luật Thuế TNCN số 07/2007/QH12**. Trong quá trình tra cứu, dữ liệu thuế hiện hành trên nhiều nguồn **mâu thuẫn nhau** (có bảng 5 bậc, có bảng 7 bậc; mức giảm trừ người phụ thuộc có nơi ghi 6,2 triệu, nơi ghi 9,5 triệu) → **chưa xác minh được bảng nào là đúng**, nên PRD **không tự điền số liệu đoán**.
> Hệ thống đang hardcode bảng thuế trong code, nên đây là rủi ro sai số tiền thật cho nhân viên — cần đối chiếu với **văn bản luật chính thức** hoặc tư vấn thuế trước khi chốt.

**Cấu trúc bảng thuế suất (code cần hardcode):**

| Thuế suất | Phần thu nhập tính thuế (VNĐ/tháng) | Phần thu nhập tính thuế (VNĐ/năm) |
|:---:|---|---|
| `i` | Từ mốc dưới → mốc trên | Từ mốc dưới → mốc trên |

**Mốc cần xác minh — hai phương án đang tồn tại:**

*Phương án A — bảng 5 bậc (tương thích với mức giảm trừ 11tr/4,4tr đang có trong PRD và trong `ChiTietTruThueTncn.cs`):*

| Bậc | Thuế suất | Thu nhập tính thuế cả năm |
|:---:|:---:|---|
| 1 | 5% | ≤ 52.000.000 |
| 2 | 10% | > 52.000.000 – 104.000.000 |
| 3 | 15% | > 104.000.000 – 160.000.000 |
| 4 | 20% | > 160.000.000 – 320.000.000 |
| 5 | 25% | > 320.000.000 |

*Phương án B — bảng 7 bậc (nguồn cho biết đã ban hành Luật Thuế TNCN số **109/2025/QH15**, hiệu lực từ **01/01/2026** — cần xác minh tồn tại thực tế không):*

| Bậc | Thuế suất | Thu nhập tính thuế cả năm | | Mức giảm trừ |
|:---:|:---:|---|---|---|
| 1 | 2% | ≤ 15.000.000 | | Bản thân: 15.500.000đ/tháng |
| 2 | 6% | > 15.000.000 – 40.000.000 | | Người phụ thuộc: **6.200.000đ** hoặc **9.500.000đ/tháng** |
| 3 | 10% | > 40.000.000 – 80.000.000 | | *(hai nguồn cho hai con số khác nhau)* |
| 4 | 14% | > 80.000.000 – 160.000.000 | | |
| 5 | 18% | > 160.000.000 – 250.000.000 | | |
| 6 | 22% | > 250.000.000 – 400.000.000 | | |
| 7 | 30% | > 400.000.000 | | |

*(Ngoài ra còn có nguồn cho bảng 7 bậc với mốc 15/60/200/320/520/1.000 triệu và thuế suất 2/6/10/16/22/28/34% — càng cho thấy dữ liệu bất nhất, **không được chọn bừa**.)*

**Việc cần làm để chốt bảng thuế:**
1. Xác minh **Luật Thuế TNCN nào đang có hiệu lực** tại thời điểm chạy hệ thống (07/2007/QH12 hay 109/2025/QH15).
2. Xác minh **mức giảm trừ bản thân và người phụ thuộc** đúng theo văn bản đó.
3. **Cập nhật đồng thời** các nơi đang hardcode giá trị cũ: trường `giam_tru_ban_than` mặc định `11000000.00m` trong `ChiTietTruThueTncn.cs`, `AppDbContext.cs`, và các file SQL (`database_schema_3nf.sql`, `20260930_upgrade_to_23_tables.sql`).
4. **Rà soát lại bảng này mỗi khi luật thay đổi** — đây là điểm dễ sai nhất của toàn hệ thống lương. Nên ghi thuế suất vào bảng cấu hình tham chiếu (tương tự `CAU_HINH_TANG_CA` cho hệ số OT) thay vì if/else cứng trong code, để rà soát chỉ cần sửa dữ liệu.

---

## 2. Quy Trình Khóa Sổ & Phát Hành Phiếu Lương PDF

```
┌─────────────────┐       ┌─────────────────┐       ┌─────────────────┐       ┌─────────────────┐
│ 1. ĐỐI SOÁT CÔNG│──────►│2. CHẠY TÍNH TOÁN│──────►│ 3. DUYỆT BẢNG   │──────►│ 4. SINH FILE PDF│
│ Chốt quẹt thẻ   │       │ Áp dụng công thức│      │ Khóa sổ kỳ lương│       │ QuestPDF        │
│ & Đơn nghỉ phép │       │ 2 tầng Gross->Net│       │(da_khoa_so=true)│       │ Gửi tới App NV │

└─────────────────┘       └─────────────────┘       └─────────────────┘       └─────────────────┘
```

1. **Mẫu Phiếu Lương Chuẩn (QuestPDF Template):**
   - **Header:** Logo The OverTime Hotel, Tên kỳ lương, Mã NV, Họ tên, Phòng ban, Chức vụ.
   - **Bảng Thu Nhập (Gross)** — phải khớp đủ 5 khoản với công thức Tầng 1:

     | # | Khoản mục | Công thức hiển thị |
     |:---:|---|---|
     | 1 | Lương công thực tế | `(Lương CB / 26) × Số ngày công thực tế` |
     | 2 | Tiền OT | `(Lương CB / 26 / 8) × Số giờ OT × Hệ số` — tách dòng theo hệ số 1.5 / 2.0 / 3.0 nếu kỳ lương có cả 3 loại |
     | 3 | Phụ cấp ca đêm 30% | `(Lương CB / 26 / 8) × Số giờ ca đêm × 0.30` |
     | 4 | **Phụ cấp ăn ca** | `30.000đ × Số ngày công thực tế` |
     | 5 | Thưởng | Thưởng doanh thu buồng phòng / sự kiện / KPI |
     | | **TỔNG GROSS** | **= Tổng 5 dòng trên** |

   - **Bảng Khấu Trừ** — phải khớp đủ 5 khoản với công thức Tầng 2:

     | # | Khoản mục | Công thức hiển thị |
     |:---:|---|---|
     | 1 | BHXH | `8% × Lương cơ bản` |
     | 2 | BHYT | `1.5% × Lương cơ bản` |
     | 3 | BHTN | `1% × Lương cơ bản` |
     | 4 | **Thuế TNCN** | Khấu trừ theo biểu lũy tiến từng phần — xem mục *3.1 & 3.2* |
     | 5 | Phạt vi phạm | Gom từ các khoản phạt đi trễ / về sớm / bỏ ca trong kỳ |
     | | **TỔNG KHẤU TRỪ** | **= Tổng 5 dòng trên** |

   - **Điều kiện cân bằng (bắt buộc kiểm chứng trước khi sinh PDF):**
     $$\text{Tổng Gross in} = \text{Lương công thực tế} + \text{Tiền OT} + \text{Phụ cấp ca đêm} + \text{Phụ cấp ăn ca} + \text{Thưởng}$$
     $$\text{Tổng Khấu Trừ in} = \text{BHXH} + \text{BHYT} + \text{BHTN} + \text{Thuế TNCN} + \text{Phạt vi phạm}$$
     $$\text{Lương Net} = \text{Tổng Gross in} - \text{Tổng Khấu Trừ in}$$
     - Thiếu dòng **Phụ cấp ăn ca** (thuộc Gross) hoặc dòng **Thuế TNCN** (thuộc khấu trừ) sẽ làm phiếu lương **không cân bằng** — hai dòng này bắt buộc phải có trên mẫu, kể cả khi giá trị bằng 0.
     - *Ngoại lệ:* nếu bật tính năng **Tạm ứng lương** (`loai_khoan_tru = 'TAM_UNG'`, có sẵn trong bảng `CAU_HINH_KHAU_TRU`), dòng này trở thành dòng thứ 6 của bảng Khấu Trừ và **phải được bổ sung vào công thức Tầng 2** ở mục 1 để bảng cân bằng.
   - **Dòng tổng kết:** Lương Net in đậm kèm số tiền viết bằng chữ.

---

## 3. Quy Tắc Làm Tròn & Kiểu Dữ Liệu Tiền Tệ

1. **Kiểu dữ liệu bắt buộc:**
   - Toàn bộ tiền lương, phụ cấp, phạt, bảo hiểm và thuế tính bằng kiểu **`decimal`** (C#) và cột `DECIMAL(15,2)` (PostgreSQL).
   - **Tuyệt đối không dùng `float` / `double`** ở bất kỳ bước trung gian nào. Sai số dấu phẩy động sẽ tích luỹ qua nhiều phép nhân/chia với hệ số 1.5 / 2.0 / 3.0 và 0.30, gây lệch tiền thật.
   - Hệ số OT (1.5, 2.0, 3.0) và ca đêm (0.30) khai báo dạng `decimal` có hậu tố `m` trong `SalaryConstants.cs` (vd: `0.30m`), đúng theo quy chuẩn viết code của dự án.

2. **Chỉ làm tròn MỘT LẦN duy nhất — tại bước hiển thị:**
   - Mọi phép tính giữa chừng **giữ nguyên độ chính xác `decimal`**, không làm tròn về 0 hay 2 chữ số ở các bước trung gian.
   - Chỉ áp dụng làm tròn tại **hai điểm**: (a) khi ghi xuống CSDL qua `HasPrecision(15, 2)`, và (b) khi in ra phiếu lương PDF.

3. **Quy ước làm tròn:**
   - Dùng `Math.Round(x, MidpointRounding.AwayFromZero)` — quy ước **làm tròn nửa ra xa** (0.5 → 1). **Không** dùng mặc định `ToEven` (0.5 → 0) của C# vì gây bất đồng nhất với thông lệ kế toán Việt Nam.
   - Về mặt hiển thị: tiền Việt Nam không có phần thập phân nên làm tròn về **0 chữ số thập phân** (bỏ hẳn phần thập phân) cho các dòng tiền trên phiếu lương. Riêng cột tỷ lệ % giữ 1–2 chữ số thập phân.

4. **Làm tròn ở đâu — mỗi dòng chi tiết hay chỉ dòng tổng:**
   - **Quy ước đã chọn: làm tròn ở TỪNG DÒNG CHI TIẾT khi hiển thị**, không chỉ ở dòng tổng.
   - *Lý do:* nhân viên đối chiếu từng khoản trên phiếu lương với bảng lương nội bộ. Nếu chỉ làm tròn ở dòng tổng thì tổng các dòng in có thể lệch khỏi tổng in, và không một dòng nào giải thích được phần chênh đó.
   - Áp dụng **cả hai chiều** để bảo đảm bất biến cân bằng nêu ở mục 2:
     - Tổng Gross in = tổng các dòng thu nhập **đã làm tròn**.
     - Tổng Khấu Trừ in = tổng các dòng khấu trừ **đã làm tròn**.
     - Lương Net in = Tổng Gross in − Tổng Khấu Trừ in, lấy **đúng từ hai tổng đã in**, tuyệt đối không tính lại từ giá trị thô chưa làm tròn.
   - Sai lệch tối đa giữa Net tính từ giá trị thô và Net in từ giá trị đã làm tròn là `±(số dòng chi tiết × 0.5đ)`. **Ngưỡng cảnh báo đề xuất: 5.000đ — chưa chốt, cần xác minh** theo quy chế kế toán của khách sạn.

---

## 4. Ngoại Lệ & Quy Tắc Bổ Sung

> Các trường hợp trong mục này **chưa được khách sạn ban hành nội quy chính thức**. Phần *đề xuất* là phương án mặc định để hệ thống vẫn chạy được; khi có nội quy chính thức sẽ cập nhật lại tại đây.

1. **Đi trễ trên 30 phút:**
   - PRD hiện chỉ quy định hai bậc: 1–15 phút → 50.000đ; 16–30 phút → 100.000đ. **Từ 31 phút trở lên chưa có bậc nào → chưa chốt.**
   - **Đề xuất:** từ 31 phút trở lên coi như **vắng mặt không phép (bỏ ca)**, áp dụng nguyên bộ xử lý của mục 2 Tầng 2 (trừ 1 ngày công quy đổi + phạt 200.000đ).
   - *Lý do đề xuất:* tránh tình trạng nhân viên đi trễ gần hết ca nhưng vẫn được hưởng trọn ngày công thực tế.
   - **Cần xác minh:** khách sạn có chấp nhận cách xử lý này không, và có nên tách riêng một bậc "31–60 phút" với mức phạt riêng thay vì gộp thẳng vào bỏ ca.

2. **Ca thiếu giờ (ra về sớm):**
   - **PRD hiện chưa có quy tắc nào cho ca thiếu giờ.**
   - **Đề xuất (khuyến nghị):** trừ lương theo số giờ thiếu thực tế, dùng chung đơn giá giờ với công thức OT ở Tầng 1:
     $$\text{Lương bị trừ} = \left(\frac{\text{Lương Cơ Bản}}{26 \times 8}\right) \times \text{Số Giờ Thiếu} \times \text{Hệ số}$$
   - **Cần xác minh:** hệ số nhân cho giờ thiếu là **1.0** (đơn giá gốc) hay **1.5** (giá OT)? Và có đặt ngưỡng miễn trừ không — ví dụ thiếu ≤ 15 phút thì coi như đủ công, không trừ vì khoản khấu trừ quá nhỏ.
   - *Ghi chú kỹ thuật:* `AttendanceCalculator.ResolveStatus(...)` đã phân loại sẵn trạng thái *về sớm* qua tham số `PhutVeSom`, nhưng chưa có quy tắc quy đổi `PhutVeSom` → tiền. Cần thống nhất khi chốt.
   - *Mâu thuẫn cần rà soát:* bảng cấu hình `CAU_HINH_KHAU_TRU` hiện đang đặt mức phạt vi phạm ca làm việc là **2.000đ/phút** (`ty_le_hoac_dinh_muc = 2000.00`), **khác** với bộ ba mức 50.000 / 100.000 / 200.000đ *trên mỗi lần vi phạm* ghi ở PRD này. Cần chốt rõ tính theo *lần vi phạm* hay theo *phút*, rồi sửa seed cho khớp. **Chưa chốt.**

3. **Về sớm và đi trễ trong cùng một ca:**
   - **Chưa chốt. Đề xuất:** gộp thành **một** lần vi phạm duy nhất, xếp mức phạt theo **tổng số phút vi phạm** (đi trễ + về sớm), tránh phạt kép một ca. **Cần xác minh** với khách sạn.

4. **Ca làm dở / check-in sai ca:**
   - **Chưa chốt. Đề xuất:** nếu nhân viên check-in hợp lệ nhưng rời ca trước giờ kết thúc dự kiến quá nửa ca, coi như vắng mặt không phép và áp dụng bộ xử lý của mục 2 Tầng 2.
   - **Cần xác minh:** ngưỡng "quá nửa ca" cụ thể là bao nhiêu phút, và có tính đội sớm cho phép (ví dụ 15 phút) trước khi áp phạt không.

5. **Mâu thuẫn giữa "bỏ ca" và "ngày công thực tế" — quy ước đã chốt:**

   Mục 2 Tầng 2 nói bỏ ca thì *trừ toàn bộ lương ngày công đó*, trong khi mục 1 Tầng 1 lấy *số ngày công thực tế* làm cơ sở tính lương. Nếu ngày bỏ ca vốn đã bị loại khỏi "ngày công thực tế" thì không tồn tại khoản lương nào để trừ — hai quy định mâu thuẫn nhau.

   - **Quy ước đã chốt: KHÔNG đưa ngày bỏ ca vào "số ngày công thực tế".** Cụ thể:
     - `Số ngày công thực tế` = số ca check-in hợp lệ + số ngày nghỉ phép được duyệt *(giữ nguyên định nghĩa ở Tầng 1 mục 1)*.
     - Ngày bỏ ca không phải ngày công thực tế → **không** cộng vào tổng số ngày công.
     - Vì vậy, câu *trừ toàn bộ lương ngày công đó* được hiểu là **cơ chế bù trừ**, và cài đặt cụ thể là **trừ trực tiếp `Lương Cơ Bản / 26`** (tức 1 ngày công quy đổi), cộng thêm **phạt 200.000 VNĐ / lần**:
       $$\text{Phạt bỏ ca} = \left(\frac{\text{Lương Cơ Bản}}{26}\right) + 200.000\ \text{VNĐ}$$
     - Cách hiểu này **tương đương về kết quả tiền** với việc "tính ngày bỏ ca rồi trừ lại", nhưng không phải sửa lại công thức ngày công thực tế ở Tầng 1 — giữ nguyên Tầng 1 đúng nghĩa *"công thực tế"*.
   - **Cần xác minh:** nếu trong cùng tháng nhân viên bỏ ca *k* ngày thì công thức trên nhân với *k*. Khách sạn có giới hạn số ngày bỏ ca được phép trong tháng không (đề xuất: cần quy định rõ trong nội quy lao động, chưa chốt).
