# Kế hoạch triển khai khung lương và phụ lục điều chỉnh lương

> Trạng thái: đã triển khai mã nguồn và áp dụng schema lên Supabase theo sự đồng ý của người dùng. Kiểm thử cục bộ và đọc lại schema/API đạt; chưa thực hiện nghiệp vụ hợp đồng/lương thật để nghiệm thu trên production.

## Mục tiêu và phạm vi

Kế thừa ASP.NET Core, EF Core và PostgreSQL/Supabase hiện có. Khi lập hợp đồng, tra khung lương theo nhóm chức vụ, trình độ và thâm niên; giữ lương gốc trên hợp đồng, lưu các thay đổi qua phụ lục có hiệu lực theo tháng. Phiếu lương sử dụng mức lương có hiệu lực tại thời gian tương ứng.

Trong phạm vi: khung lương cố định; lựa chọn trình độ trong form hợp đồng; đồng bộ hồ sơ; lương thử việc; phụ lục; nhắc xét thâm niên; quy tắc một Tổng Giám Đốc; tính lương và các nơi đọc lương liên quan. Ngoài phạm vi: màn hình chỉnh sửa khung; tự tăng lương; tự sửa dữ liệu lịch sử; triển khai production khi chưa được duyệt.

## Quy định đã thống nhất

Đơn vị triệu đồng/tháng, hai đầu khoảng được chấp nhận:

| Nhóm | Trình độ | Dưới 1 năm | Đủ 1 đến dưới 3 năm | Đủ 3 năm trở lên |
|---|---|---:|---:|---:|
| Nhân viên | Trung cấp | 6–8 | 7–9 | 8–10 |
| Nhân viên | Cao đẳng | 7–9 | 8–10 | 9–11 |
| Nhân viên | Đại học | 8–10 | 9–11 | 10–12 |
| Quản lý bộ phận | Không xét | 12–16 | 14–18 | 16–22 |
| Tổng Giám Đốc | Không xét | 25–40 | 25–40 | 25–40 |

- Tổng Giám Đốc chỉ có **một dòng**, bảng trên nhắc lại cùng khoảng để dễ đọc: tổng cộng 13 dòng.
- Thâm niên tính từ ngày vào làm tại khách sạn, so sánh đủ ngày kỷ niệm; dưới 12 tháng, từ 12 đến dưới 36 tháng, từ 36 tháng trở lên. Không đặt lại khi gia hạn hay chuyển từ thử việc sang chính thức.
- Nhóm quản lý dự kiến gồm Trưởng Bộ Phận, Bếp Trưởng, Trưởng bộ phận CNTT; Tổng Giám Đốc là nhóm riêng; tám chức vụ còn lại thuộc nhân viên. Kiểm tra lại ID thực tế và từ chối chức vụ chưa phân nhóm, không đoán theo tên hay vai trò đăng nhập.
- Hợp đồng chính thức: HR nhập mức đã thỏa thuận trong khung. Thử việc: tự tính 85% mức tối thiểu của khung, lưu số tiền đó trên hợp đồng; không nhân 85% lần nữa khi tính lương.
- Thử việc dự kiến 2 tháng, bắt đầu từ ngày lập theo yêu cầu; cần xác định chính xác cách tính ngày kết thúc và đối chiếu quy định theo loại công việc trước khi áp dụng bắt buộc. Mức tối thiểu của khung được hiển thị làm căn cứ thỏa thuận thử việc.
- Giao diện thêm trường chọn trình độ, hiện khung ngay gần ô lương, không thêm ô nhập khung; lương thử việc tự hiện và khóa, lương chính thức HR nhập.
- Phụ lục lưu mức lương mới thay thế mức cũ, gắn hợp đồng, hiệu lực ngày 1; không tự tăng hay tự chấm dứt hợp đồng. HR được nhắc khi đến mốc thâm niên.
- Chuyển chức vụ làm đổi nhóm lương: có hiệu lực ngày 1, cùng ngày với phụ lục; lưu lịch sử và áp dụng nguyên tử.
- Giữ hợp đồng và phiếu lương cũ; không tự chuyển đổi trình độ hoặc áp khung ngược cho dữ liệu lịch sử. Chỉ một người đang giữ chức Tổng Giám Đốc tại một thời điểm.

## Các điểm phải xử lý trước khi bật thực tế

1. **Hợp đồng nối tiếp giữa tháng:** người dùng đã chốt tính công từng giai đoạn theo lương hợp đồng tương ứng rồi cộng lại. Code hiện chọn hợp đồng bắt đầu muộn nhất giao với kỳ, nên phải thiết kế chi tiết và kiểm thử các thành phần lương, OT, phụ cấp, bảo hiểm và thuế; chưa mặc định chia các khoản theo cùng công thức khi chưa xác minh nghiệp vụ.
2. **Căn cứ lương thử việc:** 85% của mức tối thiểu khung chỉ là chính sách lựa chọn; cần xác nhận mức dùng làm căn cứ thỏa thuận là mức lương của công việc, xem xét lương tối thiểu và quy định áp dụng tại địa bàn. Nếu mức công việc đã thỏa thuận cao hơn, 85% của mức tối thiểu khung có thể không đủ. Không khẳng định hợp lệ chỉ dựa vào phép tính.
3. **Phụ lục có hiệu lực quá khứ / kỳ đã khóa:** chặn tạo, sửa, hủy phụ lục ảnh hưởng kỳ đã duyệt hoặc khóa, không âm thầm tính lại; quy trình điều chỉnh kỳ cũ để sau.
4. **Ghế Tổng Giám Đốc:** xác định tính duy nhất theo thời gian, nhân viên tạm nghỉ, người có ngày bắt đầu trong tương lai và cạnh tranh đồng thời; kiểm kê dữ liệu live lại, không dựa vào snapshot. Chọn cơ chế khóa/ràng buộc theo thiết kế đã xác nhận, không xóa dữ liệu để làm sạch tự động.
5. **Trình độ cũ:** yêu cầu HR xác nhận giá trị ngoài ba mức trước nghiệp vụ mới của nhóm nhân viên; không ghi đè bằng phỏng đoán. Nhóm quản lý không dùng trình độ để tra khung; giữ nguyên thông tin hồ sơ.

## Hiện trạng và vị trí cần rà soát

- `src/OvertimeHotel.HRM.WebAdmin/Controllers/HopDongController.cs`: tạo, chuyển tiếp, gia hạn, kiểm tra và cập nhật chức vụ.
- `src/OvertimeHotel.HRM.WebAdmin/Models/NhanVienManagementViewModels.cs`, `Views/HopDong/_Form.cshtml`, `Views/HopDong/Index.cshtml`: form, chế độ gia hạn và dữ liệu chọn.
- `src/OvertimeHotel.HRM.WebAdmin/Controllers/NhanVienController.cs`, `Views/NhanVien/_EmployeeForm.cshtml`: lấy hợp đồng để hoàn thiện hồ sơ; cập nhật chức vụ và ngày vào làm.
- `src/OvertimeHotel.HRM.Core/Models/HopDong.cs`, `NhanVien.cs`, `PhieuLuong.cs`; `src/OvertimeHotel.HRM.Data/Context/AppDbContext.cs`: hợp đồng, lương, ánh xạ và lịch sử.
- `src/OvertimeHotel.HRM.Core/Policies/ReportPolicy.cs`: hàm tính tháng tròn có thể tham khảo, kiểm tra ranh giới và năm nhuận.
- `src/OvertimeHotel.HRM.Data/Services/PayrollCalculationService.cs`: tính cả kỳ, tính lại từng người, khóa kỳ; `GiaiTrinhChamCongController.cs`, `BaoCaoReportService.cs`, `Api/AiDataController.cs` và giao diện chi tiết hợp đồng/hồ sơ cần rà soát ý nghĩa lương gốc so với lương hiệu lực.
- `src/OvertimeHotel.HRM.Data/migrations/`: migration tiến và rollback có kiểm kê; kiểm tra cấu trúc thực tế và quyền/RLS trước khi viết.
- Kiểm thử hiện có: `tests/ContractRules/ContractRules.csproj`, `tests/ContractsUiFixture/ContractsUiFixture.csproj`; khảo sát các bộ payroll/report có sẵn trước khi chọn nơi bổ sung test.

## Các bước thực hiện

- [x] **1. Kiểm kê chỉ đọc và chốt điểm mở.** Kiểm tra schema, FK, RLS, dữ liệu chức vụ/trình độ/hợp đồng, trạng thái kỳ lương và tác động đến các màn hình; xuất thống kê không chứa bí mật. Tài liệu hóa điểm mở 1–4; không bắt đầu phần phụ thuộc cho tới khi có quyết định.
- [x] **2. Thiết kế dữ liệu và migration cục bộ.** Định nghĩa 13 dòng khung, phân nhóm chức vụ rõ ràng, phụ lục với hợp đồng, lương mới, ngày hiệu lực, lý do, tác giả và thời gian; định nghĩa lịch sử đổi chức vụ theo ngày. Ràng buộc khoảng hợp lệ, không trùng phụ lục cùng ngày, ngày 1, quyền truy cập và đồng thời. Viết rollback chỉ gỡ đúng đối tượng đã tạo sau khi kiểm tra tên và dữ liệu phát sinh; dừng nếu không khớp. Không tự chạy migration production.
- [x] **3. Xây dựng chính sách thuần.** Tra cứu 13 khung theo nhóm/trình độ/thâm niên; quy tắc chặn thiếu dữ liệu; kiểm tra hai đầu khoảng, thử việc 85% và ngày kỷ niệm 1/3 năm. Tạo kiểm thử thất bại trước, chạy để xác nhận thất bại, rồi hiện thực và chạy lại.
- [x] **4. Nối form hợp đồng và hồ sơ.** Thêm chọn trình độ; hiển thị khung, tự tính lương thử việc và khóa ô đó; hợp đồng chính thức cho nhập trong khung. Máy chủ tính và kiểm tra lại, không tin dữ liệu browser. Khi tạo nhân viên chưa hoàn chỉnh lưu trình độ để khi chọn hợp đồng hoàn thiện hồ sơ tự hiện; gia hạn lấy thông tin hiện tại mà không thay ngày vào làm ban đầu. Kiểm thử tạo mới, gia hạn, dữ liệu thiếu và giả mạo request.
- [x] **5. Phụ lục và nhắc thâm niên.** Thêm danh sách và tạo phụ lục trong chi tiết hợp đồng; kiểm tra quyền HR/Admin theo chính sách dự án, hợp đồng và khung hợp lệ, ngày 1, một phụ lục/ngày và kỳ liên quan; không tự sửa hợp đồng gốc. Tạo danh sách nhắc xét dựa trên mốc, không tự áp lương. Kiểm thử bảo mật, lịch sử và đồng thời.
- [x] **6. Đồng bộ thay đổi chức vụ.** Rà soát tạo hợp đồng và sửa hồ sơ để không lách chính sách. Khi đổi nhóm, ghi lịch sử chức vụ và phụ lục cùng giao dịch, cùng ngày 1; bảo đảm ghế Tổng Giám Đốc không bị trùng kể cả yêu cầu đồng thời. Không áp hồi tố lên hồ sơ đã tồn tại. Kiểm thử lỗi giữa giao dịch và cạnh tranh.
- [x] **7. Chọn lương có hiệu lực.** Dùng một quy tắc chung cho hợp đồng/phụ lục theo ngày để tính lương cả kỳ và tính lại, giữ snapshot trên phiếu lương; rà soát truy lĩnh chấm công, báo cáo, API và phân biệt lương gốc với lương hiệu lực. Xử lý hợp đồng giữa tháng theo quyết định tại bước 1; không thay đổi kỳ đã khóa. Kiểm thử trước/sau phụ lục, nhiều phụ lục, tái tính và kỳ khóa.
- [x] **8. Kiểm thử tích hợp và giao diện.** Chạy `dotnet run --project tests/ContractRules/ContractRules.csproj`, các bộ payroll/report và fixture phù hợp đã xác định; build WebAdmin, kiểm tra UI lập hợp đồng, hoàn thiện hồ sơ, phụ lục, gia hạn. Test trên dữ liệu giả lập riêng, không thực hiện giao dịch nhân sự thật. Ghi rõ kiểm thử nào chạy/không chạy.
- [ ] **9. Chuẩn bị triển khai có kiểm soát — còn thiếu xác nhận bản sao lưu.** Đối chiếu migration với bản CSDL đích, sao lưu, lập báo cáo dữ liệu xung đột và phương án khôi phục; kiểm thử migration/rollback trên bản sao hoặc môi trường riêng. Trình người dùng xem và duyệt cụ thể trước khi thay đổi Supabase thật; kiểm tra lại sau khi áp dụng nếu được duyệt.

## Tiêu chí nghiệm thu

- Đủ 13 khung đúng số tiền; 6, 8 triệu nằm trong khung 6–8; 5,99 và 8,01 triệu không nằm trong khung đó.
- Trung cấp dưới 1 năm tối thiểu 6 triệu, thử việc tự tính 5,1 triệu; Cao đẳng dưới 1 năm tối thiểu 7 triệu, thử việc 5,95 triệu. Đó là kết quả công thức, không thay thế xác nhận pháp lý ở trên.
- Trước ngày đủ năm thuộc nhóm cũ, ngày đủ năm thuộc nhóm mới; không tính lại ngày vào làm khi gia hạn.
- Phụ lục 01/02 chuyển 8 thành 10 triệu: tháng 1 dùng 8 triệu, tháng 2 dùng 10 triệu; xem và tính lại đúng lịch sử, kỳ đã khóa được bảo vệ.
- Chặn tạo phụ lục ngày 15, lương ngoài khung, chức vụ chưa phân nhóm, yêu cầu trái quyền và việc bổ nhiệm Tổng Giám Đốc trùng thời gian; hai yêu cầu đồng thời không tạo hai ghế.
- Hồ sơ cũ không bị tự đổi; trình độ chưa rõ được báo cần xác minh; không làm mất email công vụ hoặc quyền tài khoản.

## Cổng bàn giao

Plan được duyệt không đồng nghĩa phê duyệt migration hay thao tác nghiệp vụ trên dữ liệu thật. Báo cáo riêng kết quả đọc mã, test, build, kiểm tra UI và bất kỳ kiểm tra staging/production nào; không suy diễn kiểm thử cục bộ là đã sẵn sàng production.


## Kết quả triển khai và kiểm chứng

- WebAdmin build: 0 lỗi, 0 cảnh báo.
- ContractRules: 31 tình huống đạt; cập nhật các kỳ vọng thử việc theo quy tắc mới và bổ sung kiểm tra không kéo dài hợp đồng cũ đã hết hạn.
- EffectivePayroll: 38/38 tình huống đạt, bao gồm các khoản theo ngày, khoản theo tháng, lịch sử phụ lục, báo cáo và API.
- SalaryContracts: đạt trên InMemory và PostgreSQL riêng bật EnableRetryOnFailure; kiểm tra khung từ DB, thử việc tự tính, phụ lục, kỳ khóa, chống ghi đè chức vụ bởi phụ lục quá khứ.
- Browser fixture desktop/mobile: đạt cho tra khung, tranh chấp phản hồi, lương thử việc readonly, giới hạn lương, ngày cuối tháng, phụ lục lỗi/lưu và không trùng ID. Các lời gọi ghi trong fixture là dữ liệu giả lập.
- PostgreSQL riêng: migration, 13 seed, RLS, null/unique constraints, chuyển giao GM, cạnh tranh ở ReadCommitted/RepeatableRead, khóa kỳ, rollback có kiểm kê đều đạt.
- Supabase: migration và rollback thử trong giao dịch đạt trước khi áp dụng; đã áp dụng thành công trong một giao dịch. Bước áp dụng so sánh trước/sau số nhân viên, số hợp đồng và tổng lương cơ bản hợp đồng; không có thay đổi các giá trị đó.
- Đọc lại Supabase: 13 khung, 0 phụ lục, 12 chức vụ đã phân nhóm; 4 bảng mới có RLS. Đọc qua PostgREST bằng cấu hình ServiceRole hiện có xác nhận 13 khung/0 phụ lục; không in khóa.

### Các lựa chọn kỹ thuật đã thực hiện

- Có 2 bảng nghiệp vụ (`khung_luong_cap_bac`, `phu_luc_luong`) và 2 bảng hỗ trợ nội bộ (`salary_write_guard`, `salary_position_baseline`). Bảng hỗ trợ dùng để chống tranh chấp ghế và giữ mốc chức vụ quan sát tại ngày triển khai; không khẳng định chức vụ đó là chức vụ tại ngày vào làm trong quá khứ.
- Hợp đồng mới lưu snapshot chức vụ/phòng ban/trình độ. Hợp đồng cũ giữ nguyên dữ liệu; phụ lục chỉ đổi lương không cần tự điền snapshot. Nếu điều chuyển hợp đồng cũ, HR phải đối chiếu và xác nhận chức vụ/phòng ban gốc trước khi lưu snapshot.
- Phụ lục chỉ đổi chức vụ khi HR chọn rõ chức năng điều chuyển. Điều chỉnh riêng lương kế thừa lịch sử đã lên lịch tại ngày hiệu lực, tránh vô tình hủy một lần bổ nhiệm trong tương lai.
- Đồng bộ chức vụ hiện tại theo lịch sử có hiệu lực khi web đang chạy, mỗi phút và khi khởi động; tính lương dùng trực tiếp lịch sử theo ngày, không chờ worker.
- Thử việc được tính theo hai tháng lịch: ngày kết thúc = ngày bắt đầu cộng 2 tháng trừ 1 ngày; máy chủ tính lương 85% mức tối thiểu và chỉ chấp nhận bắt đầu hôm nay. Đây là cấu hình nghiệp vụ người dùng chọn, không phải kết luận pháp lý cho mọi loại công việc.
- Bảo hiểm/thuế và thưởng tháng được tính một lần sau khi ghép công các giai đoạn; bảo hiểm giữ quy ước hiện có, đếm công chính thức và dùng mức chính thức có hiệu lực tương ứng. Định mức báo cáo quỹ lương chia theo ngày lịch để làm tham chiếu; tiền lương thực tế vẫn theo công/26.
- Không thực hiện giao dịch nhân sự thật để thử chức năng. Khi khởi chạy lại WebAdmin, cần nghiệm thu bằng quy trình vận hành được cho phép; build/test/schema không thay thế chứng cứ E2E production.


## Việc còn lại trước khi xác nhận nghiệm thu toàn bộ

- [ ] Xác nhận bản sao lưu/khả năng khôi phục của CSDL đích có bằng chứng cụ thể. Migration/rollback đã thử và schema đã áp dụng, nhưng không đồng nghĩa có bản sao lưu đầy đủ.
- [ ] Khởi động lại WebAdmin với mã mới và nghiệm thu luồng có đăng nhập trên môi trường vận hành: hợp đồng → hoàn thiện hồ sơ → phụ lục → bảng lương. Mọi thao tác ghi nghiệp vụ thật cần được cho phép cụ thể; kiểm thử fixture không thay thế bước này.

Không dùng toàn bộ dấu tích ở phần triển khai để khẳng định đã nghiệm thu production 100%.


## Sửa lỗi khi thử tạo Tổng Giám Đốc thứ hai

- Tái hiện trên database PostgreSQL cục bộ riêng có migration/trigger thật; không tạo hợp đồng thật trên Supabase.
- Create trước đây gộp lỗi trùng ghế thành lỗi chung về số hợp đồng. Đã phân biệt P0001 của guard ghế Tổng Giám Đốc, xử lý cả PostgresException và DbUpdateException, xóa tracked entity bị từ chối sau rollback.
- Index có thông báo lỗi nhìn thấy trực tiếp, không chỉ phụ thuộc toast biến mất sau vài giây.
- Regression GeneralDirectorCreation đạt: thông báo đúng, không có nhân viên/hợp đồng phát sinh, danh sách đọc được sau lỗi. ContractRules 31/31 và browser fixture đạt; kiểm tra trình duyệt có thông báo rõ và không có modal backdrop che trang khi hiển thị lỗi.
- Dòng lỗi đọc CSDL cụ thể ở phiên người dùng chưa được cung cấp để đối chiếu; không khẳng định mọi lỗi kết nối CSDL có cùng nguyên nhân.


## Sửa lỗi trang Xem xét lương khi dữ liệu chưa hợp lệ

- Đã tái hiện đúng exception thiếu lương ở hợp đồng #2333 bằng dữ liệu giả lập. Đọc Supabase chỉ để đối chiếu: hợp đồng #2333 đang có lương cơ bản 0 và chưa có phụ lục; không sửa dữ liệu thật.
- Danh sách xem xét chuyển các lỗi lương/lịch sử thành dòng cần xác minh có lý do; một hồ sơ lỗi không làm dừng danh sách. Các hợp đồng chồng lấn được liệt kê để đối soát.
- Giữ EffectiveSalaryResolver nghiêm ngặt cho tính lương; không dùng 0 hoặc mức mặc định để tính thay dữ liệu thiếu.
- Đổi tên lối vào thành Xem xét lương và bổ sung giải thích. Tổng Giám Đốc có lương hợp lệ trong khung cố định không bị nhắc chỉ vì đến mốc thâm niên.
- Bộ SalaryReviews kiểm tra lương 0/âm, phụ lục sai ngày, hợp đồng chồng lấn, hồ sơ hợp lệ đến mốc, nhóm Tổng Giám Đốc và dữ liệu giữ nguyên.


## Điều chỉnh giao diện Xem xét lương thành popup

- Nút Xem xét lương dùng biểu tượng và màu xanh đồng bộ thanh thao tác; mở Bootstrap modal ngay trên trang Hợp đồng, giữ nguyên URL và bộ lọc.
- Modal tải danh sách qua GET SalaryReviews với X-Requested-With=XMLHttpRequest; endpoint trả partial riêng, giữ fallback trang đầy đủ cho đường dẫn trực tiếp.
- Các dòng hiển thị nhân viên, thâm niên, lương hiện tại, khung và lý do; bố cục hai cột trên điện thoại, định dạng tiền Việt Nam. Không thêm thao tác ghi dữ liệu.
- Có trạng thái tải/rỗng/lỗi/thử lại; hủy request khi đóng, không chèn trang đăng nhập/lỗi đầy đủ vào popup; hỗ trợ Escape, trả focus về nút mở.
- Kiểm thử bằng fixture cục bộ với dữ liệu giả lập và backend SalaryReviews; không tạo/sửa hợp đồng thật.


## Kiểm tra popup không tải danh sách và trạng thái rỗng

- Phát hiện WebAdmin trên cổng 5065 chạy từ 23:05 ngày 11/11, trước các thay đổi C# phục vụ danh sách partial. Tệp JS/CSS được tải mới trong khi tiến trình máy chủ vẫn giữ assembly cũ.
- Đã gọi controller bản hiện tại bằng Npgsql trong giao dịch READ ONLY trên Supabase: trả đúng partial, 35 hồ sơ, trong đó 1 hồ sơ cần xác minh lương. Không ghi dữ liệu trong bước kiểm tra này.
- Đã khởi động lại WebAdmin tại 5065 để nạp bản hiện tại, kiểm tra ứng dụng phản hồi HTTP 200. Chưa dùng cookie người dùng để gọi màn hình có đăng nhập.
- Trạng thái danh sách rỗng: Chưa có nhân viên nào cần xét lương. Kiểm thử backend không có hồ sơ phù hợp trả danh sách rỗng; lỗi kết nối thật vẫn được báo lỗi và có nút thử lại.


## Tối ưu tải danh sách và đối chiếu bằng cấp chi tiết

- Đối chiếu chỉ đọc hợp đồng #2: hồ sơ có bằng Đại học Quản trị Nhân sự, chức vụ Chuyên viên Nhân sự & Tiền lương thuộc nhóm NHAN_VIEN, ngày vào làm 01/03/2024. Thiếu khung vì code cũ so khớp nguyên chuỗi với nhãn Đại học.
- SalaryBandLookup nhận diện tiền tố bằng cấp rõ ràng Trung cấp/Cao đẳng/Đại học, không phân biệt hoa thường và chuẩn hóa Unicode khi tra cứu; giữ nguyên văn bản hồ sơ. Không suy ra Đại học từ chứng chỉ hoặc tự gộp Sau Đại học vào khung chưa cấu hình.
- Tra khung trong báo giá, lập hợp đồng, phụ lục và danh sách dùng cùng quy tắc. Với 2 năm 8 tháng, trường hợp #2 trả đúng 9–11 triệu; lương cũ 18 triệu không bị sửa.
- Danh sách tải danh mục chức vụ và 13 khung một lần, thay hai truy vấn cho từng người. Lần đo READ ONLY trên Supabase: trước 99 truy vấn/10,24 giây, sau 3 truy vấn/0,82 giây; đây là thời gian truy vấn kiểm tra, không phải cam kết độ trễ giao diện mọi lúc.
- Kiểm thử thêm tên bằng cấp có chuyên ngành/hoa thường/Unicode, giữ dữ liệu gốc, không nâng chứng chỉ hoặc bằng ngoài phạm vi, và giới hạn số truy vấn. Đã nạp bản sửa vào WebAdmin cổng 5065.
