using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Core.Policies;

/// <summary>
/// Nguồn luật duy nhất (Single Source of Truth) định nghĩa các ràng buộc
/// giữa Hợp đồng lao động và nghiệp vụ Phân ca / Chấm công:
/// 1. Nhân viên còn hợp đồng lao động đang có hiệu lực mới được phân ca và chấm công.
/// 2. Hợp đồng hết hạn (hoặc chưa ký) thì nhân viên không xuất hiện trong danh sách phân công.
/// 3. Khi nhân viên được gia hạn hợp đồng (hoặc ký hợp đồng mới có hiệu lực),
///    nhân viên sẽ xuất hiện trở lại và được phép phân ca, chấm công bình thường.
/// </summary>
public static class ContractAttendancePolicy
{
    public const string MSG_NO_CONTRACT = "Bạn chưa có hợp đồng lao động có hiệu lực. Hệ thống tạm khóa tính năng chấm công cho đến khi hợp đồng được ký kết.";
    public const string MSG_EXPIRED_CONTRACT = "Hợp đồng lao động của bạn đã hết hạn vào ngày {0:dd/MM/yyyy}. Hệ thống tạm khóa tính năng chấm công cho đến khi hợp đồng mới được kích hoạt / gia hạn.";
    public const string MSG_FUTURE_CONTRACT = "Hợp đồng lao động của bạn có hiệu lực từ ngày {0:dd/MM/yyyy}. Bạn chưa thể thực hiện chấm công trước ngày này.";

    public const string STATUS_HIEU_LUC = "HIEU_LUC";
    public const string STATUS_HET_HAN = "HET_HAN";
    public const string STATUS_DA_CHAM_DUT = "DA_CHAM_DUT";

    /// <summary>
    /// Kiểm tra một hợp đồng có đang có hiệu lực tại ngày date hay không.
    /// Điều kiện: TrangThai == "HIEU_LUC", NgayBatDau &lt;= date, và (NgayKetThuc == null hoặc NgayKetThuc &gt;= date).
    /// </summary>
    public static bool IsValidOn(HopDong? contract, DateOnly date)
    {
        if (contract == null) return false;
        if (contract.TrangThai != STATUS_HIEU_LUC) return false;
        if (contract.NgayBatDau > date) return false;
        if (contract.NgayKetThuc.HasValue && contract.NgayKetThuc.Value < date) return false;
        return true;
    }

    /// <summary>
    /// Kiểm tra nhân viên có hợp đồng nào đang có hiệu lực vào ngày date hay không.
    /// Nhân viên phải ở trạng thái đang làm việc (DANG_LAM) và có hợp đồng bao phủ ngày làm việc.
    /// </summary>
    public static bool HasValidContractOn(NhanVien? employee, DateOnly date)
    {
        if (employee == null || employee.TrangThai != "DANG_LAM") return false;
        if (employee.HopDongs == null || employee.HopDongs.Count == 0) return false;
        return employee.HopDongs.Any(h => IsValidOn(h, date));
    }

    /// <summary>
    /// Kiểm tra nhân viên có hợp đồng có hiệu lực bao phủ khoảng thời gian (ví dụ tuần làm việc [start, end]) hay không.
    /// Dùng để lọc danh sách nhân sự sẵn sàng phân ca trong tuần.
    /// </summary>
    public static bool HasValidContractDuring(NhanVien? employee, DateOnly start, DateOnly end)
    {
        if (employee == null || employee.TrangThai != "DANG_LAM") return false;
        if (employee.HopDongs == null || employee.HopDongs.Count == 0) return false;
        return employee.HopDongs.Any(h =>
            h.TrangThai == STATUS_HIEU_LUC &&
            h.NgayBatDau <= end &&
            (!h.NgayKetThuc.HasValue || h.NgayKetThuc.Value >= start));
    }

    /// <summary>
    /// Đối soát hợp đồng để cho phép hoặc từ chối chấm công vào ngày date.
    /// Trả về IsAllowed = true nếu được phép chấm công; ngược lại trả về mã lỗi và thông báo thân thiện.
    /// </summary>
    public static (bool IsAllowed, string? ErrorCode, string? ErrorMessage) ValidateAttendanceContract(
        IEnumerable<HopDong>? contracts,
        DateOnly date)
    {
        if (contracts == null)
        {
            return (false, "NO_CONTRACT", MSG_NO_CONTRACT);
        }

        var contractList = contracts.ToList();
        if (contractList.Count == 0)
        {
            return (false, "NO_CONTRACT", MSG_NO_CONTRACT);
        }

        // Tìm hợp đồng có hiệu lực bao phủ ngày date
        var validContract = contractList
            .Where(h => IsValidOn(h, date))
            .OrderByDescending(h => h.NgayBatDau)
            .FirstOrDefault();

        if (validContract != null)
        {
            return (true, null, null);
        }

        // Nếu không có hợp đồng bao phủ ngày date, tìm hợp đồng gần nhất để giải thích lý do
        var latestContract = contractList.OrderByDescending(h => h.NgayBatDau).First();

        if (latestContract.NgayKetThuc.HasValue && latestContract.NgayKetThuc.Value < date)
        {
            return (false, "CONTRACT_EXPIRED",
                string.Format(MSG_EXPIRED_CONTRACT, latestContract.NgayKetThuc.Value));
        }

        if (latestContract.NgayBatDau > date)
        {
            return (false, "CONTRACT_NOT_YET_ACTIVE",
                string.Format(MSG_FUTURE_CONTRACT, latestContract.NgayBatDau));
        }

        return (false, "NO_ACTIVE_CONTRACT", MSG_NO_CONTRACT);
    }

    /// <summary>
    /// Đối soát hợp đồng để cho phép hoặc từ chối phân ca vào ngày date.
    /// </summary>
    public static (bool IsAllowed, string? ErrorCode, string? ErrorMessage) ValidateSchedulingContract(
        NhanVien? employee,
        DateOnly date)
    {
        if (employee == null)
        {
            return (false, "EMPLOYEE_NOT_FOUND", "Không tìm thấy thông tin nhân viên.");
        }

        if (employee.TrangThai != "DANG_LAM")
        {
            return (false, "EMPLOYEE_INACTIVE", $"Nhân viên {employee.HoTen} hiện không ở trạng thái đang làm việc ({employee.TrangThai}).");
        }

        if (employee.HopDongs == null || employee.HopDongs.Count == 0)
        {
            return (false, "NO_CONTRACT", $"Nhân viên {employee.HoTen} chưa ký kết hợp đồng lao động. Vui lòng tạo hợp đồng trước khi phân ca.");
        }

        var validContract = employee.HopDongs
            .Where(h => IsValidOn(h, date))
            .OrderByDescending(h => h.NgayBatDau)
            .FirstOrDefault();

        if (validContract != null)
        {
            return (true, null, null);
        }

        var latestContract = employee.HopDongs.OrderByDescending(h => h.NgayBatDau).First();
        if (latestContract.NgayKetThuc.HasValue && latestContract.NgayKetThuc.Value < date)
        {
            return (false, "CONTRACT_EXPIRED",
                $"Nhân viên {employee.HoTen} không thể phân ca vào ngày {date:dd/MM/yyyy} do hợp đồng lao động đã hết hạn vào ngày {latestContract.NgayKetThuc.Value:dd/MM/yyyy}. Vui lòng gia hạn hợp đồng trước khi phân ca.");
        }

        if (latestContract.NgayBatDau > date)
        {
            return (false, "CONTRACT_NOT_YET_ACTIVE",
                $"Nhân viên {employee.HoTen} có hợp đồng bắt đầu từ ngày {latestContract.NgayBatDau:dd/MM/yyyy}, không thể phân ca trước ngày này.");
        }

        return (false, "NO_ACTIVE_CONTRACT",
            $"Nhân viên {employee.HoTen} không có hợp đồng lao động có hiệu lực vào ngày {date:dd/MM/yyyy}. Vui lòng gia hạn hợp đồng.");
    }
}
