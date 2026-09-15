# PRD 02: Phân Tích Yêu Cầu & Sơ Đồ Phân Rã Chức Năng (BFD)

## 1. Danh Mục 8 Nhóm Chức Năng Cốt Lõi

Hệ thống HRM Khách sạn The OverTime Hotel bao gồm 8 nhóm chức năng chính được phân rã thành các chức năng thành phần:

| Mã | Nhóm Chức Năng Cấp 1 | Các Chức Năng Thành Phần Chi Tiết (Cấp 2) |
|:---:|:---|:---|
| **1.0** | **Quản lý Nhân viên & HĐLĐ** | 1.1 Thêm hồ sơ nhân viên mới<br>1.2 Sửa, cập nhật lý lịch nhân viên<br>1.3 Quản lý Hợp đồng lao động (Ký mới, gia hạn, cảnh báo hết hạn HĐ)<br>1.4 Tìm kiếm, lọc nhân viên đa tiêu chí<br>1.5 Bổ nhiệm, điều chuyển phòng ban và chức vụ |
| **2.0** | **Tài khoản & Phân quyền** | 2.1 Đăng nhập / Đăng xuất hệ thống<br>2.2 Quản lý tài khoản người dùng (Khóa/mở account, cấp lại mật khẩu)<br>2.3 Phân 4 vai trò chuẩn (Admin / HR / Manager / Employee)<br>2.4 Quản lý danh mục quyền truy cập chức năng (RBAC) |
| **3.0** | **Ca làm & Chấm công 24/7** | 3.1 Quản lý danh mục mẫu ca (Ca sáng, Ca chiều, Ca đêm)<br>3.2 Lập lịch xếp ca tuần/tháng cho nhân sự từng bộ phận<br>3.3 Ghi nhận Check-in / Check-out thực tế trên điện thoại di động<br>3.4 Tự động theo dõi số phút đi trễ, về sớm theo khung giờ ca<br>3.5 Tính toán số giờ làm thêm tăng ca (Overtime - OT) |
| **4.0** | **Quản lý Đơn từ điện tử** | 4.1 Tạo và nộp Đơn xin nghỉ phép năm<br>4.2 Tạo và nộp Đơn nghỉ ốm / bệnh (hưởng BHXH)<br>4.3 Tạo và nộp Đơn nghỉ chế độ thai sản<br>4.4 Tạo và nộp Đơn xin thôi việc / chấm dứt HĐLĐ<br>4.5 Quy trình xét duyệt / từ chối đơn từ kèm lý do trực tuyến |
| **5.0** | **Tính lương tự động (Gross→Net)** | 5.1 Tính lương theo ngày công thực tế từ bảng chấm công<br>5.2 Tính tiền làm thêm giờ (OT ngày thường 1.5, ngày lễ 2.0)<br>5.3 Tính phụ cấp ca đêm (cộng thêm 30% lương theo Luật Lao động)<br>5.4 Tính các khoản phụ cấp ăn ca, trách nhiệm và tiền thưởng<br>5.5 Tự động tính khấu trừ bảo hiểm bắt buộc 10.5% (BHXH 8%, BHYT 1.5%, BHTN 1%)<br>5.6 Tính tiền phạt vi phạm và tính Lương thực nhận (Net) |
| **6.0** | **Phiếu lương & Xuất PDF** | 6.1 Xem chi tiết phiếu lương tháng trên Web và Mobile App<br>6.2 Tra cứu lịch sử thu nhập qua các tháng<br>6.3 Xuất và in Phiếu lương tháng định dạng PDF (QuestPDF)<br>6.4 In Bảng tổng hợp thu nhập cả năm phục vụ quyết toán thuế |
| **7.0** | **Báo cáo & Thống kê Quản trị** | 7.1 Báo cáo tổng số lượng và biến động nhân sự (tuyển mới, thôi việc)<br>7.2 Thống kê cơ cấu nhân sự theo phòng ban và chức vụ<br>7.3 Báo cáo phân tích trình độ học vấn và thâm niên công tác<br>7.4 Báo cáo tổng quỹ lương thực tế so với hạn mức ngân sách<br>7.5 Báo cáo tỷ lệ chuyên cần và vi phạm chấm công |
| **8.0** | **Quản trị Hệ thống** | 8.1 Quản lý danh mục phòng ban khách sạn<br>8.2 Quản lý danh mục chức vụ và mô tả công việc<br>8.3 Quản lý danh mục ca làm việc và khung giờ quy định<br>8.4 Cấu hình định mức phụ cấp, tiền thưởng và các mức phạt vi phạm<br>8.5 Sao lưu (Backup) và phục hồi (Restore) cơ sở dữ liệu Supabase |

---

## 2. Ma Trận Phân Quyền Vai Trò Người Dùng (RBAC Matrix)

Hệ thống phân định rõ quyền hạn của 4 vai trò chuẩn:

| Chức Năng Hệ Thống | Quản Trị Viên (Admin) | Phòng Nhân Sự (HR) | Quản Lý Bộ Phận (Manager) | Nhân Viên (Employee) |
|:---|:---:|:---:|:---:|:---:|
| **Quản trị tài khoản, RBAC** | Toàn quyền (CRUD) | Chỉ xem | Không | Không |
| **Cấu hình danh mục hệ thống** | Toàn quyền (CRUD) | Chỉ xem | Không | Không |
| **Sao lưu & Phục hồi CSDL** | Toàn quyền | Không | Không | Không |
| **Hồ sơ nhân viên & HĐLĐ** | Chỉ xem | Toàn quyền (CRUD) | Xem bộ phận mình | Chỉ xem hồ sơ cá nhân |
| **Xếp lịch ca làm việc** | Chỉ xem | Xếp ca toàn KS | Xếp ca bộ phận mình | Xem lịch ca cá nhân |
| **Chấm công Check-in/out** | Chỉ xem log | Giám sát toàn KS | Giám sát bộ phận mình | Quẹt thẻ trên Mobile App |
| **Nộp 4 loại đơn từ** | Không | Nộp đơn cá nhân | Nộp đơn cá nhân | Nộp đơn trực tuyến |
| **Duyệt đơn từ** | Không | Duyệt thôi việc/thai sản | Duyệt nghỉ phép/đổi ca | Không |
| **Khóa sổ & Tính lương** | Không | Thực thi tính lương | Không | Không |
| **Tra cứu phiếu lương PDF** | Không | Tra cứu toàn KS | Tra cứu cá nhân | Tra cứu & tải PDF cá nhân |
| **Báo cáo quản trị** | Báo cáo an toàn | Báo cáo chi tiết NS | Báo cáo chấm công ca | Không |
