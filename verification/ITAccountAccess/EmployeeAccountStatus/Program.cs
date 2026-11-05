using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.WebAdmin.Models;
void Check(NhanVien employee, string expected) { var actual = new EmployeeListItemViewModel { Employee=employee }.AccountStatus; if (actual != expected) throw new Exception($"Expected {expected}, got {actual}"); }
var employee = new NhanVien();
Check(employee, "CHUA_CAP");
employee.YeuCauTaiKhoanCuaNhanVien.Add(new YeuCauTaiKhoan { MaYeuCau=1, TrangThai="TU_CHOI", NgayGui=DateTimeOffset.UtcNow.AddDays(-1) });
Check(employee, "TU_CHOI");
employee.YeuCauTaiKhoanCuaNhanVien.Add(new YeuCauTaiKhoan { MaYeuCau=2, TrangThai="CHO_IT", NgayGui=DateTimeOffset.UtcNow });
Check(employee, "CHO_CAP");
var latest = employee.YeuCauTaiKhoanCuaNhanVien.Single(request => request.MaYeuCau == 2);
employee.YeuCauTaiKhoanCuaNhanVien.Add(new YeuCauTaiKhoan { MaYeuCau=3, TrangThai="TU_CHOI", NgayGui=latest.NgayGui });
Check(employee, "TU_CHOI");
employee.TaiKhoan = new TaiKhoan();
Check(employee, "DA_CAP");
Console.WriteLine("PASS missing, rejected, resent and existing account statuses");
