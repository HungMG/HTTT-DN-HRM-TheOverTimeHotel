namespace OvertimeHotel.HRM.Core.Enums;

public enum RoleType
{
    Admin = 1,
    HR = 2,
    Manager = 3,
    Employee = 4,

    /// <summary>
    /// Bộ phận Công nghệ Thông tin (ma_phong_ban = 9). Tách riêng khỏi
    /// Kỹ thuật (ma_phong_ban = 6, kỹ sư tòa nhà) vì hai việc khác hẳn:
    /// kỹ thuật bảo trì hạ tầng vật lý, còn IT thì quản trị hệ thống và cấp
    /// phát tài khoản đăng nhập — điểm vào của toàn bộ quyền truy cập.
    /// </summary>
    IT = 5
}
