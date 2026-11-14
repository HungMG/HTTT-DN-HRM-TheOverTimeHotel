using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public static class FormInputMessages
{
    public static void LocalizeBindingErrors(ModelStateDictionary state)
    {
        foreach (var (key, entry) in state)
        {
            if (entry.Errors.Count == 0) continue;
            var message = key switch
            {
                "Contract.NgayBatDau" or "Contract.NgayKetThuc" or "Employee.NgaySinh" or "Employee.NgayVaoLam" or "Employee.NgayThoiViec"
                    => "Vui lòng chọn ngày hợp lệ bằng ô chọn ngày.",
                "Contract.LuongCoBan" => "Nhập mức lương bằng số, ví dụ 10,000,000; mức lương phải nằm trong khung được hiển thị.",
                "TrinhDo" or "Employee.TrinhDo" => "Vui lòng chọn trình độ chuyên môn.",
                "EmployeeName" => "Vui lòng nhập đầy đủ họ và tên, ví dụ Nguyễn Văn An.",
                "Employee.SoNguoiPhuThuoc" => "Số người phụ thuộc phải là số nguyên từ 0 đến 20.",
                "EmployeeId" or "DepartmentId" or "PositionId" or "ContractId" or "Employee.MaPhongBan" or "Employee.MaChucVu"
                    => "Vui lòng chọn một giá trị hợp lệ trong danh sách.",
                _ => "Vui lòng nhập thông tin hợp lệ theo hướng dẫn của trường này."
            };
            entry.Errors.Clear();
            entry.Errors.Add(message);
        }
    }
}
