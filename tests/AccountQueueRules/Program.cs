using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;


var today = new DateOnly(2026, 10, 15);
var tests = new (string Name, Action Run)[]
{
    ("1.1 Phân quyền gửi yêu cầu: Admin và HR được phép gửi, IT/Manager/Employee bị từ chối", () =>
    {
        Assert(LeaveApprovalPolicy.CanSubmitAccountRequest("Admin"), "Admin must be able to submit");
        Assert(LeaveApprovalPolicy.CanSubmitAccountRequest("HR"), "HR must be able to submit");
        Assert(!LeaveApprovalPolicy.CanSubmitAccountRequest("IT"), "IT must NOT be able to submit");
        Assert(!LeaveApprovalPolicy.CanSubmitAccountRequest("Manager"), "Manager must NOT be able to submit");
        Assert(!LeaveApprovalPolicy.CanSubmitAccountRequest("Employee"), "Employee must NOT be able to submit");
        Assert(!LeaveApprovalPolicy.CanSubmitAccountRequest(null), "Null role must NOT be able to submit");
    }),

    ("1.2 Phân quyền xử lý yêu cầu: Admin và IT được phép xử lý, HR/Manager/Employee bị từ chối", () =>
    {
        Assert(LeaveApprovalPolicy.CanProcessAccountRequest("Admin"), "Admin must be able to process");
        Assert(LeaveApprovalPolicy.CanProcessAccountRequest("IT"), "IT must be able to process");
        Assert(!LeaveApprovalPolicy.CanProcessAccountRequest("HR"), "HR must NOT be able to process");
        Assert(!LeaveApprovalPolicy.CanProcessAccountRequest("Manager"), "Manager must NOT be able to process");
        Assert(!LeaveApprovalPolicy.CanProcessAccountRequest("Employee"), "Employee must NOT be able to process");
        Assert(!LeaveApprovalPolicy.CanProcessAccountRequest(null), "Null role must NOT be able to process");
    }),

    ("1.3 Phân quyền đề xuất vai trò: Chỉ Admin được đề xuất cấp vai trò Admin", () =>
    {
        Assert(LeaveApprovalPolicy.CanProposeRole("Admin", "Admin"), "Admin can propose Admin");
        Assert(!LeaveApprovalPolicy.CanProposeRole("HR", "Admin"), "HR cannot propose Admin");
        Assert(!LeaveApprovalPolicy.CanProposeRole("IT", "Admin"), "IT cannot propose Admin");

        Assert(LeaveApprovalPolicy.CanProposeRole("Admin", "Employee"), "Admin can propose Employee");
        Assert(LeaveApprovalPolicy.CanProposeRole("HR", "Employee"), "HR can propose Employee");
        Assert(LeaveApprovalPolicy.CanProposeRole("Admin", "HR"), "Admin can propose HR");
        Assert(LeaveApprovalPolicy.CanProposeRole("HR", "IT"), "HR can propose IT");
    }),

    ("2.1 Hợp đồng hợp lệ: Hiệu lực, đã bắt đầu và chưa hết hạn", () =>
    {
        var isValid = LeaveApprovalPolicy.IsValidContractForAccountRequest(
            trangThai: "HIEU_LUC",
            ngayBatDau: new DateOnly(2026, 1, 1),
            ngayKetThuc: new DateOnly(2027, 1, 1),
            today: today);
        Assert(isValid, "Standard active contract must be valid");
    }),

    ("2.2 Hợp đồng không xác định thời hạn (ngayKetThuc == null) được chấp nhận", () =>
    {
        var isValid = LeaveApprovalPolicy.IsValidContractForAccountRequest(
            trangThai: "HIEU_LUC",
            ngayBatDau: new DateOnly(2026, 1, 1),
            ngayKetThuc: null,
            today: today);
        Assert(isValid, "Indefinite contract must be valid");
    }),

    ("2.3 Hợp đồng hết hạn trong hôm nay được chấp nhận", () =>
    {
        var isValid = LeaveApprovalPolicy.IsValidContractForAccountRequest(
            trangThai: "HIEU_LUC",
            ngayBatDau: new DateOnly(2026, 1, 1),
            ngayKetThuc: today, // expires today
            today: today);
        Assert(isValid, "Contract expiring today must be accepted");
    }),

    ("2.4 Chặn hợp đồng đã hết hạn trong quá khứ (hôm qua)", () =>
    {
        var isValid = LeaveApprovalPolicy.IsValidContractForAccountRequest(
            trangThai: "HIEU_LUC",
            ngayBatDau: new DateOnly(2026, 1, 1),
            ngayKetThuc: today.AddDays(-1), // expired yesterday
            today: today);
        Assert(!isValid, "Expired contract must be rejected");
    }),

    ("2.5 Chặn hợp đồng chưa bắt đầu (bắt đầu trong tương lai)", () =>
    {
        var isValid = LeaveApprovalPolicy.IsValidContractForAccountRequest(
            trangThai: "HIEU_LUC",
            ngayBatDau: today.AddDays(1), // starts tomorrow
            ngayKetThuc: today.AddYears(1),
            today: today);
        Assert(!isValid, "Future starting contract must be rejected");
    }),

    ("2.6 Chặn hợp đồng có trạng thái khác HIEU_LUC (DA_CHAM_DUT, HET_HAN, TAM_HOAN)", () =>
    {
        Assert(!LeaveApprovalPolicy.IsValidContractForAccountRequest("DA_CHAM_DUT", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), today), "Terminated contract rejected");
        Assert(!LeaveApprovalPolicy.IsValidContractForAccountRequest("HET_HAN", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), today), "Expired status contract rejected");
        Assert(!LeaveApprovalPolicy.IsValidContractForAccountRequest("TAM_HOAN", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), today), "Suspended contract rejected");
        Assert(!LeaveApprovalPolicy.IsValidContractForAccountRequest(null, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), today), "Null status contract rejected");
    })
};

int passed = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        passed++;
        Console.WriteLine($"[PASS] {name}");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {name}: {ex.Message}");
        Console.ResetColor();
        Environment.ExitCode = 1;
    }
}

Console.WriteLine($"\nCompleted {passed}/{tests.Length} policy and business rule checks.");

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
}
