# Sự kiện khách sạn — điều phối theo ca và tiền theo giờ

## Chính sách mới (23/10/2026)

Sự kiện tạo mới trên Web dùng `TinhTheoGio = true`. Khoản cộng thêm được tính bằng **lương cơ bản tháng / 26 / 8 × số giờ sự kiện thực tế được duyệt × hệ số thưởng sự kiện** (mặc định **2.0**). Lương ca thông thường vẫn tính qua chấm công; không cộng lại lương ca trong dòng tiền sự kiện. Lương 8.000.000đ và hai giờ thực tế được cộng thêm **153.846đ**. Hệ số lưu tại `cau_hinh_khoan_luong` dòng `ma_khoan = 35`, sửa được tại **Cấu hình lương & Phụ cấp → Thưởng**; code đọc qua `EventRateConfig` và fallback về `SalaryConstants.EVENT_HOURLY_RATE` nếu thiếu dòng. Không dùng ma trận định mức cố định của chính sách cũ. Dùng số chính xác để tính và làm tròn đến đồng sau khi tổng hợp theo nhân viên/sự kiện/tháng phát sinh.

### Sự kiện đặt trước lịch tuần

1. HR/Admin đặt sự kiện và địa điểm, khai báo nhu cầu theo phòng ban, vai trò và số người cho toàn sự kiện. Người quản lý được chọn khi tạo/sửa phải là HR đang làm việc, có tài khoản hoạt động. Người quản lý của sự kiện cũ được giữ nguyên đến khi sửa thông tin. Mẫu theo số khách được sinh sẵn; chỉnh một dòng sẽ giữ dòng đó khỏi cập nhật mẫu tự động về sau.
2. Màn hình `/PhanCa` hiển thị nhu cầu của tuần kể cả chưa có người được phân công. Dự báo ca chỉ là số người có lịch giao giờ theo bộ lọc, chưa xác nhận vai trò hoặc khả năng điều động.
3. Xếp ca cân đối trực thường và sự kiện. HR/Admin công bố lịch tuần vào thứ Bảy cho tuần kế tiếp; công bố ngoài lịch thông thường cần lý do. Sửa ca làm ca đó chưa công bố lại.
4. Chọn nhân viên từ ca chính thức, chọn ca nguồn và vai trò. Backend lấy phần giao giữa khoảng nhiệm vụ và ca. Tiệc 15h–20h với ca kết thúc 17h chỉ phân công 15h–17h. Đủ người đầu buổi không bù được khoảng thiếu cuối buổi.
5. Manager chỉ điều hành sự kiện được giao. Backend kiểm tra hợp đồng, nghỉ đã duyệt, nhiệm vụ trùng và định biên bộ phận nguồn trước khi giao/bắt đầu. Nhu cầu chỉ HR/Admin sửa trước khi sự kiện bắt đầu, có lý do; mỗi phòng ban/vai trò có một dòng nhu cầu áp dụng theo thời gian sự kiện.

Hệ số cấu hình áp dụng khi xem trước và chốt khoản mới. Quyết toán đã khóa giữ số tiền và hệ số trong căn cứ; căn cứ theo giờ cũ chưa có trường `Rate` được đọc theo hệ số x1 của chính sách trước đó.

Kỳ lương mới dùng `MO`/`KHOA_SO`; sự kiện chỉ nhận tiền vào kỳ `MO` chưa gửi duyệt/chưa duyệt, hoặc kỳ đã bị từ chối. Các trạng thái nháp/từ chối cũ còn được đọc tương thích. Kiểm tra cả ngày gửi/ngày duyệt, không chỉ tên trạng thái.

Không chạy migration xóa phụ cấp điện thoại/tạm ứng hay seed số người phụ thuộc trên dữ liệu đang dùng trong bước cập nhật code này. Khởi tạo schema chỉ bổ sung cấu trúc, không xóa dòng phiếu lương lịch sử hoặc tự điền số người phụ thuộc mẫu. Các khoản điện thoại/tạm ứng không được phát sinh trong phép tính lương mới.

### Công thực tế, bàn giao và nghiệm thu

- Mỗi phân công nhập một khoảng **Tham gia từ — đến**; tiền cộng thêm tính theo toàn bộ thời lượng của khoảng giờ đó. Không phân loại đi trễ/về sớm, không chia đoạn để trừ khoảng nghỉ và không đối chiếu giờ vào/ra ca để tính tiền sự kiện. Giờ tham gia vẫn phải nằm trong sự kiện, phân công và ca nguồn, không ở tương lai.
- Kiểm tra trùng công của cùng nhân viên trên toàn bộ sự kiện, gồm công còn lưu trong lịch sử phân công đã hủy. Bảng xem trước cảnh báo để đối soát công trùng cũ; việc mở bảng không tự loại bỏ công hoặc tạo khoản chi trả.
- Manager xác nhận giờ bắt đầu và kết thúc tham gia; server lưu người xác nhận và thời điểm. Thiếu hoặc thay đổi dữ liệu chấm công ca không làm thay đổi tiền sự kiện.
- HR/Admin sửa khoảng giờ bằng lý do bắt buộc; giữ nguyên mã công và lưu đầy đủ dữ liệu trước/sau, người xác nhận và thời điểm trong cùng giao dịch. Dữ liệu, giải trình và quyết toán lịch sử đã có được giữ lại.
- Giờ tham gia phải thuộc đúng sự kiện, trong phân công và ca nguồn đã công bố còn hiệu lực, từ giờ bắt đầu đến giờ kết thúc sự kiện. Không tính thêm thời gian chuẩn bị/thu dọn. Công của phân công đã hủy vẫn được hiển thị để đối soát nếu đã phát sinh, kèm cảnh báo thay vì bị bỏ khỏi bảng xem trước.
- Hợp đồng đã chấm dứt vẫn làm căn cứ cho công trong khoảng ngày đã ghi nhận. Hợp đồng chấm dứt thiếu ngày kết thúc cần bổ sung căn cứ; không tự suy ra thời gian còn hiệu lực hoặc dùng mức lương mới nhất.
- Phân công chưa thực hiện được hủy và giữ lịch sử. Đã làm dùng **Kết thúc / bàn giao**; thêm người thay thế trước nếu phần thời gian tương lai thiếu vai trò. Không xóa công để thay người.
- Hủy sự kiện chỉ khi còn lên kế hoạch và chưa có công. Đã thực hiện dùng **Dừng sự kiện**, lưu giờ dừng/lý do, giữ phần công đã làm rồi chuyển chờ nghiệm thu. Tiền sự kiện chỉ tính trong giờ bắt đầu đến kết thúc, hoặc giờ dừng thực tế nếu dừng trước.
- Nút nghiệm thu dành cho HR/Admin sau khi xác nhận công/vắng mặt toàn bộ phân công hiệu lực. Xem trước hiển thị từng người, tháng phát sinh, phút, mức lương theo hợp đồng và tổng cộng. Không có hệ số đánh giá xuất sắc trong công thức mới.
- Chọn hoặc tạo kỳ trả còn `MO`, `NHAP`, `TU_CHOI`, không trước tháng phát sinh. Nghiệm thu tính lại phía server và kiểm tra dấu xem trước để không khóa theo mức lương cũ nếu căn cứ vừa thay đổi.
- Quyết toán lưu công, căn cứ lương, người duyệt/thời điểm và kỳ trả. Đổi lương sau nghiệm thu không định giá lại. Công qua tháng được tách theo tháng Việt Nam; có thể trả bổ sung vào kỳ mở sau.
- Nghiệm thu chưa phải đã chi tiền. Chạy bảng lương nhận đúng khoản đã duyệt của kỳ được chọn, liên kết mỗi khoản một lần. Tính lại và thao tác đồng thời không nhân đôi tiền; người đã nghỉ việc vẫn giữ quyền lợi được duyệt.

### Địa điểm và thay đổi ca

Sự kiện tổ chức cố định **Khách sạn The Overtime**. Biểu mẫu không chọn sảnh/khu, không nhập sức chứa hoặc số phút chuẩn bị/thu dọn; vẫn nhập số khách dự kiến để sinh định mức nhân sự. Server cố định địa điểm và bỏ qua mã sảnh/giờ đệm client cũ gửi lên; các cột mã sảnh và giờ đệm đã được loại khỏi bảng sự kiện. Khách sạn là địa điểm chung, không phải một sảnh độc quyền nên có thể tạo nhiều sự kiện trùng giờ; kiểm tra trùng nhân sự/giờ tham gia vẫn được giữ.

Khi đổi/hủy ca tương lai, phân công sự kiện từ ca đó được hủy hiệu lực và lưu lịch sử, sự kiện tăng phiên bản để quản lý bố trí lại. Ca đã có lịch sử công thực hiện không được sửa/xóa làm mất căn cứ; ca nguồn có lịch sử được giữ bằng trạng thái `DA_HUY` thay vì xóa vật lý. Ca hủy không tính vào lịch hoạt động/chấm công/lương. Trạng thái chấm công thường như hoàn thành không tự coi là thay lịch ca.

### Dữ liệu cũ

Migration không chuyển tiền lịch sử sang chính sách giờ. Sự kiện cũ tiếp tục nghiệm thu theo chính sách cũ, nhưng **không được kết chuyển trước nghiệm thu hoặc định giá lại theo cấu hình mới**. Với khoản cũ chưa vào lương, HR/Admin dùng đối soát để xác nhận số tiền đã lưu, căn cứ và kỳ trả; không sửa số tiền gốc. Các khoản cũ đã kết chuyển được bảo vệ, không tự xóa/tính lại. Nếu còn quyền lợi cũ chưa đối soát, bảng lương báo rõ để xử lý thay vì trả vào kỳ bất kỳ.

### Thu gọn cấu trúc sự kiện (26/10/2026)

Giữ năm bảng lõi, giữ nguyên cột tiền và liên kết lương lịch sử. Nhu cầu chỉ còn sự kiện, phòng ban, vai trò, số người và cờ mẫu; thời gian lấy từ sự kiện. Khi đổi giờ sự kiện, nhu cầu tùy chỉnh theo giờ mới, số người đã sửa được giữ nguyên. Không tạo thêm dòng trùng phòng ban/vai trò.

Áp dụng `20261026_event_schema_cleanup.sql` sau các migration cũ và trước khi chạy bản web này. Dừng web, sao lưu dữ liệu/schema rồi chạy migration trong giao dịch. Migration bỏ 3 cột địa điểm/giờ đệm của `du_an`, 3 cột tên/giờ khung nhu cầu và 2 cột ngoại lệ công. Các giá trị lịch sử có ý nghĩa được lưu trong `nhat_ky_quan_tri` với hành động `TINH_GON_CAU_TRUC_SU_KIEN` và căn cứ JSON. Tên địa điểm lịch sử trong `du_an.dia_diem` giữ nguyên. Bản migration không có cập nhật tiền hay tạo quyết toán mới.

Migration dừng toàn bộ nếu có vai trò nhu cầu trùng hoặc khung nhu cầu khác giờ sự kiện; cần đối soát dữ liệu đó trước, không tự gộp hay bỏ. Chạy lại migration không tạo nhật ký trùng. Không chạy lại các migration cũ sau bước thu gọn vì chúng có thể thêm lại cột cũ.

Khi cần quay lại bản web cũ, dừng web mới và chạy `20261026_event_schema_cleanup.rollback.sql`. Script khôi phục cột và giá trị đã lưu cho những dòng còn tồn tại. Nhu cầu tạo sau thu gọn dùng thời gian sự kiện hiện tại; công mới không có giải trình ngoại lệ cũ. Các bản lưu trong nhật ký vẫn được giữ. Bản sao lưu là căn cứ phục hồi đầy đủ nếu đã thay đổi dữ liệu sau triển khai.

Báo cáo sự kiện theo giờ cộng tiền từ `quyet_toan_su_kien`, mỗi khoản một lần dù nhân viên có nhiều phân công. Báo cáo sự kiện cũ giữ số tiền lịch sử ở phân công; không cộng thêm lần nữa từ quyết toán đối soát.

### Migration và kiểm chứng

Áp dụng các migration sự kiện hiện có trước, sau đó chạy **tường minh**:

`src/OvertimeHotel.HRM.Data/migrations/20261023_event_hourly_shift_planning.sql`

Migration cộng thêm bảng/cột, khóa giao dịch dùng chung và trigger bảo vệ ca nguồn; chạy lại được. Không tự chạy trên database thật lúc khởi động. Trước phát hành: sao lưu, kiểm kê khoản cũ chưa trả và các loại đơn nghỉ (`loai_don.la_nghi_phep`). Danh mục sảnh cũ được giữ cho lịch sử; tạo/sửa sự kiện hiện tại không phụ thuộc danh mục này. Migration chỉ đánh dấu các tên nghỉ thông dụng; loại đơn tùy biến cần HR xác nhận, không suy ra nghỉ từ cờ hưởng lương.

```powershell
dotnet run --project tests/HotelEvents/HotelEvents.csproj
dotnet run --project tests/HotelEventsWeb/HotelEventsWeb.csproj -- --export
python tests/hotel-events-needs.py
python tests/hotel-events-work.py
dotnet run --project tests/PayrollRules/PayrollRules.csproj
node tests/hotel-events-ui.mjs
dotnet build src/OvertimeHotel.HRM.WebAdmin/OvertimeHotel.HRM.WebAdmin.csproj
# Chỉ khi EVENT_TEST_CONNECTION trỏ database kiểm thử đã migration:
dotnet run --project tests/HotelEventsIntegration/HotelEventsIntegration.csproj
dotnet run --project tests/PayrollRules/PayrollRules.csproj -- --integration
```

Hai runner database có ghi dữ liệu fixture; không dùng database thật cho kiểm thử phát triển. Việc kiểm thử tính lương không xác nhận mức thuế/bảo hiểm của hệ thống đã được cập nhật theo quy định hiện hành.

## Tham khảo giao diện và chính sách cũ

Các mô tả dưới đây áp dụng cho sự kiện trước chính sách theo giờ. Quy tắc an toàn về nghiệm thu, giữ lịch sử và đối soát kỳ trả ở phần trên được ưu tiên.

## Cập nhật luồng phụ cấp (21/10/2026)

Sự kiện tạo mới không nhập ngân sách phụ cấp. Trong chi tiết sự kiện, **Thêm người** mở popup chọn nhân viên theo phòng ban, sau đó chọn vai trò và thời gian tham gia. Nhân sự được đánh giá **Hoàn thành** hoặc **Xuất sắc** sẽ nhận mức thưởng theo cấu hình khoản lương của loại sự kiện và phòng ban tại lúc phân công (hoặc định mức chuẩn khi không có cấu hình); **Không tham gia** nhận 0 đ. HR/Admin nghiệm thu sau khi đánh giá toàn bộ nhân sự; lúc đó hệ thống chốt tiền từng người, cộng thành ngân sách trả nhân viên và khóa sự kiện. Kết chuyển lương lấy số tiền đã chốt. Sự kiện đã có trước bản cập nhật giữ cách nhập ngân sách/phụ cấp cũ.

> **Đã thay thế (26/10/2026):** chính sách ma trận định mức nêu trên không còn áp dụng. Sự kiện mới tính theo giờ với hệ số thưởng sự kiện (mặc định 2.0, cấu hình `ma_khoan = 35`). Migration `20261026_remove_legacy_event_bonus_matrix.sql` xóa 27 dòng ma trận (`ma_khoan` 8–34) trong `cau_hinh_khoan_luong`; các dòng `chi_tiet_phieu_luong` lịch sử giữ nguyên tên khoản và số tiền với `ma_khoan = NULL`.

Cần áp dụng `src/OvertimeHotel.HRM.Data/migrations/20261021_event_acceptance_budget.sql` trước khi chạy bản web mới; migration thêm cờ với mặc định `false` để giữ nguyên dữ liệu sự kiện cũ và cột lưu phòng ban tại lúc phân công. Với cấu hình hệ số thưởng sự kiện, chạy `src/OvertimeHotel.HRM.Data/migrations/20261026_add_event_hourly_rate_config.sql` (thêm dòng `ma_khoan = 35`) trước `20261026_remove_legacy_event_bonus_matrix.sql`. Migration không tự chạy khi khởi động.

Mở `/SuKien` hoặc mục **Sự kiện khách sạn** trên menu. Admin và HR tạo sự kiện; Manager chỉ thấy và điều hành sự kiện được giao phụ trách. IT không có quyền quản lý mặc định.

- Bấm tên sự kiện trên lịch hoặc danh sách ngày để mở popup chi tiết. Các nút **Thông tin sự kiện**, **Nhân sự**, **Đánh giá & nghiệm thu** chuyển nội dung trong popup; đóng popup giữ lựa chọn ngày và trả bàn phím về sự kiện vừa mở. Đường dẫn chi tiết trực tiếp vẫn dùng được.
- Trong **Nhân sự**, mỗi phòng ban có nút **Thêm người** mở popup đè lên phần nhân sự đang xem. Tích chọn một hoặc nhiều nhân viên rồi bấm **Tiếp tục**, chọn vai trò riêng cho từng người và thời gian tham gia chung. Sự kiện cũ vẫn có ô phụ cấp dự kiến **mỗi người**. Vai trò được lọc theo loại sự kiện và phòng ban; **Vai trò khác** cần nhập tên và chỉ là bổ sung. Hủy hoặc Escape đóng popup và trả bàn phím về nút thêm người; phần nhân sự bên dưới giữ nguyên. Lưu kiểm tra cả nhóm trong một giao dịch: có người không hợp lệ thì chưa lưu ai và giữ các lựa chọn để sửa. Thành công cập nhật danh sách và lịch ngay trên trang. Nhân viên đã phân công không xuất hiện lại trong bộ chọn. Hợp đồng và trùng lịch được kiểm tra khi lưu.
- Mỗi phòng ban có vai trò chính cần `ceil(số khách / định mức khách mỗi người)`, một vai trò phối hợp bắt buộc cần 1 người, và một vai trò bổ sung. Danh sách hiển thị số cần, đã phân công và còn thiếu theo vai trò. Người dư ở vai trò này không bù được vai trò khác. Nhân viên thay ca chỉ được tính trong khoảng thời gian thực sự được giao.
- Định mức khách/người của vai trò chính: Tiệc cưới — Tiền sảnh 100, F&B 20, Bếp 50, Buồng phòng 100, Kỹ thuật 200, An ninh 100; MICE — Tiền sảnh 75, F&B 40, Bếp 80, Kỹ thuật/An ninh/IT 100; Gala — Tiền sảnh 100, F&B 20, Bếp 50, Kỹ thuật/An ninh 100; Festival — F&B 25, Bếp 30, Buồng phòng 100, Kỹ thuật 150, An ninh 100; VIP — Tiền sảnh 20, F&B 10, Bếp 25, Buồng phòng 15, An ninh 20.
- Chưa có số khách hoặc còn thiếu vai trò ở bất kỳ khoảng thời gian nào thì chưa thể bắt đầu. Khi đang diễn ra, gỡ người phải có người cùng vai trò thay thế đủ thời gian còn lại. Vai trò cũ không khớp danh mục vẫn được hiển thị để quản lý phân công lại; dữ liệu đã nghiệm thu/hủy giữ nguyên, không bị áp định mức mới hồi tố. Vai trò mới dùng cột `vai_tro_du_an` hiện có, nhiệm vụ mới để trống và không thay đổi mức thưởng theo phòng ban.
- Đánh giá theo phòng ban, chọn **Hoàn thành** hoặc **Không tham gia**. Các khoản đã nghiệm thu được hiển thị chỉ đọc; HR/Admin thực hiện nghiệm thu theo điều kiện hiện có.

- Nhấp từng ô để chọn/bỏ chọn nhiều ngày. Nhấp đúp ô ngày hoặc bấm **Tạo sự kiện** để mở popup ngay trên lịch. Popup điền ngày bắt đầu sớm nhất và ngày kết thúc muộn nhất đã chọn; sự kiện kéo dài liên tục giữa hai mốc này và có thể chỉnh lại. Bàn phím dùng mũi tên để di chuyển, Enter/Space để chọn, rồi nút **Tạo sự kiện**. Hủy/Escape giữ lựa chọn và trả lại vị trí bàn phím. Mở popup chưa ghi dữ liệu.
- Thời gian nhập/hiển thị theo Việt Nam (UTC+7), lưu UTC. Lịch hỗ trợ năm 1900–2100. Sự kiện qua đêm/nhiều ngày hiện trên từng ngày liên quan; kết thúc đúng 00:00 không chiếm ngày kế tiếp. Hai sự kiện nối tiếp nhau được dùng cùng địa điểm.
- HR/Admin sửa thông tin ở bước lên kế hoạch. Cần gỡ phân công trước khi đổi thời gian để đối chiếu lại hợp đồng và lịch. Không sửa sự kiện đã hủy hoặc đã nghiệm thu.
- Phân công từ nhiều phòng ban, kiểm tra nhân viên đang làm việc, hợp đồng bao phủ thời gian tham gia và trùng nhân viên/trùng giờ. Với sự kiện cũ còn kiểm tra tổng phụ cấp dự kiến theo ngân sách đã nhập. Lịch ca chỉ để đối chiếu, không tự sinh tăng ca.
- Luồng: **Lên kế hoạch → Đang diễn ra → Chờ nghiệm thu → Hoàn thành**. Bắt đầu/chuyển chờ nghiệm thu khi đã đến giờ tương ứng. Hủy phải ghi lý do.
- Đánh giá sau khi kết thúc và chuyển chờ nghiệm thu. Không tham gia có phụ cấp bằng 0. HR/Admin chốt khi tất cả đã đánh giá; sự kiện cũ còn kiểm tra tổng thực tế không vượt ngân sách đã nhập, còn sự kiện mới tính tổng ngân sách tại bước này. Sau chốt, thông tin và phân công được khóa.
- Thao tác ghi có lịch sử cùng giao dịch. Khóa giao dịch chống xung đột giữa nhiều yêu cầu; phiên bản chống ghi đè dữ liệu mới. Khi báo dữ liệu đã thay đổi, tải lại trang trước khi thao tác tiếp.

## Database và kiểm thử

Migration gốc: `src/OvertimeHotel.HRM.Data/migrations/20261015_hotel_events.sql`. Migration bổ sung vào hai bảng `du_an`, `phan_cong_du_an`, giữ các cột phụ cấp/lịch sử cũ và không chạy tự động khi khởi động web. Quyền truy cập trực tiếp từ `anon`/`authenticated` được đóng; Web dùng kết nối máy chủ. Kết chuyển lương hiện dùng quyết toán theo phần chính sách mới ở trên; không cấp quyền quản lý trực tiếp bảng cho Mobile.

```powershell
dotnet run --project tests/HotelEvents/HotelEvents.csproj
node tests/hotel-events-ui.mjs
dotnet run --project tests/HotelEventsIntegration/HotelEventsIntegration.csproj
```

Kiểm thử tích hợp đọc `EVENT_TEST_CONNECTION`, hoặc cấu hình WebAdmin trong `appsettings.json`/`appsettings.Development.json`. Chỉ chạy với database đã áp dụng migration. Chương trình tạo dữ liệu mang tiền tố ngẫu nhiên, kiểm tra cả lưu đồng thời và dọn đúng dữ liệu của lần chạy trong `finally`.

Kiểm tra popup trên đúng bản web đang sử dụng: `python tests/hotel-events-popup.py http://localhost:5065` (sau khi tạo bộ dữ liệu thử bên dưới, cần Playwright). Khi sửa Controller/ViewModel C#, build và khởi động lại web; Razor runtime compilation chỉ tự cập nhật `.cshtml`, không cập nhật assembly C# đang chạy. Popup báo riêng lỗi máy chủ, phiên đăng nhập, quyền và phiên biểu mẫu.

Kiểm tra popup chi tiết bằng `python tests/hotel-events-details.py http://localhost:5065`, sau đó `python tests/hotel-events-details-lifecycle.py http://localhost:5065` trên cùng bộ preview fixture mới. Hai bài kiểm tra lần lượt kiểm tra danh sách vai trò/ô vai trò khác, phân công kỹ thuật, bổ sung đủ 12 vị trí bắt buộc của hội nghị MICE 40 khách, đánh giá và nghiệm thu sự kiện lịch sử của fixture. Tạo lại fixture trước khi chạy lại cặp bài này. Kiểm tra lịch/ngày bằng `python tests/hotel-events-dates.py http://localhost:5065`.

Để kiểm tra trình duyệt với tài khoản thử, runner có `--preview-fixture` và `--cleanup-preview`; thông tin đăng nhập được ghi vào `.probe/event-preview.json` đã được Git bỏ qua. Dọn bộ dữ liệu thử ngay sau khi kiểm tra. Không dùng tài khoản thật cho các phép thử ghi dữ liệu.
