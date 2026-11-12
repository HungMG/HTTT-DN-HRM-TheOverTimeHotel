using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.WebAdmin.Services;
using OvertimeHotel.HRM.WebAdmin.Models;
using System.Collections;
using System.Reflection;
using System.IO.Compression;
using System.Text;
using OvertimeHotel.HRM.WebAdmin.Controllers;

var from = new DateOnly(2026, 10, 1);
var to = new DateOnly(2026, 10, 31);
var leave = new DonTu { TrangThai = "DA_DUYET", TuNgay = new(2026, 9, 30), DenNgay = new(2026, 10, 2), LoaiDon = new LoaiDon { LaNghiPhep = true } };
if (!ReportPolicy.ApprovedLeaveFilter(from, to).Compile()(leave)) throw new Exception("Approved overlapping leave must count");
leave.TrangThai = "CHO_DUYET";
if (ReportPolicy.ApprovedLeaveFilter(from, to).Compile()(leave)) throw new Exception("Unapproved leave must not count");
leave.TrangThai = "DA_DUYET";
leave.TuNgay = new(2026, 11, 1);
leave.DenNgay = leave.TuNgay;
if (ReportPolicy.ApprovedLeaveFilter(from, to).Compile()(leave)) throw new Exception("Leave outside the period must not count");
leave.TuNgay = new(2026, 10, 31);
leave.DenNgay = leave.TuNgay;
if (!ReportPolicy.ApprovedLeaveFilter(from, to).Compile()(leave)) throw new Exception("Leave on the last day must count");
leave.LoaiDon!.LaNghiPhep = false;
if (ReportPolicy.ApprovedLeaveFilter(from, to).Compile()(leave)) throw new Exception("Non-leave requests must not count");
var employee = new NhanVien { NgayVaoLam = new(2026, 1, 1), TrangThai = "THOI_VIEC", NgayThoiViec = new(2026, 11, 1) };
if (!ReportPolicy.IsEmployedAt(employee, to)) throw new Exception("Future resignation must not exclude employee");
employee.NgayThoiViec = to;
if (ReportPolicy.IsEmployedAt(employee, to)) throw new Exception("Resignation on reporting date must exclude employee");
employee.NgayThoiViec = null;
if (ReportPolicy.IsEmployedAt(employee, to)) throw new Exception("Undated resignation must not be assumed active");
employee.TrangThai = "DANG_LAM";
employee.NgayVaoLam = new(2026, 11, 1);
if (ReportPolicy.IsEmployedAt(employee, to)) throw new Exception("Future hire must not count");
var requests = new[]
{
    new DonTu { MaNhanVien = 1, TrangThai = "DA_DUYET", TuNgay = new(2026, 10, 2), DenNgay = new(2026, 10, 3), LoaiDon = new LoaiDon { LaNghiPhep = true } },
    new DonTu { MaNhanVien = 1, TrangThai = "DA_DUYET", TuNgay = new(2026, 10, 7), DenNgay = new(2026, 10, 8), LoaiDon = new LoaiDon { LaNghiPhep = true } },
    new DonTu { MaNhanVien = 2, TrangThai = "CHO_DUYET", TuNgay = from, DenNgay = to, LoaiDon = new LoaiDon { LaNghiPhep = true } }
};
if (requests.AsQueryable().Where(ReportPolicy.ApprovedLeaveFilter(from, to)).Select(d => d.MaNhanVien).Distinct().Count() != 1)
    throw new Exception("Count each employee once and exclude pending leave");
if (ReportPolicy.CanViewPayrollFundReport("Manager")) throw new Exception("Manager must not see salary");
if (!ReportPolicy.CanViewPayrollFundReport("HR")) throw new Exception("HR must be permitted to see salary");
if (ReportPolicy.SalaryBucketLabel(20_000_000m) != "Từ 20 triệu trở lên")
    throw new Exception("Twenty million must be clearly included in the highest salary band");
var contractFilter = ReportPolicy.ContractAtDate(to).Compile();
var contract = new HopDong { TrangThai = "HIEU_LUC", NgayBatDau = to, NgayKetThuc = to };
if (!contractFilter(contract)) throw new Exception("Contract active on boundary must count");
contract.NgayBatDau = to.AddDays(1);
if (contractFilter(contract)) throw new Exception("Future contract must not count");
contract.NgayBatDau = from;
contract.TrangThai = "CHAM_DUT";
contract.NgayKetThuc = null;
if (contractFilter(contract)) throw new Exception("Undated termination must not imply an active contract");
var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=offline_test;Username=offline;Password=offline").Options;
using var database = new AppDbContext(options);
var sql = database.DonTus.Where(ReportPolicy.ApprovedLeaveFilter(from, to)).Select(d => d.MaNhanVien).Distinct().ToQueryString();
if (!sql.Contains("DISTINCT", StringComparison.OrdinalIgnoreCase)) throw new Exception("PostgreSQL query must count each employee once");
var contractExpression = ReportPolicy.ContractAtDate(to);
var salarySql = database.NhanViens.Select(n => n.HopDongs.AsQueryable().Where(contractExpression)
    .OrderByDescending(h => h.NgayBatDau).Select(h => (decimal?)h.LuongCoBan).FirstOrDefault()).ToQueryString();
if (!salarySql.Contains("hop_dong", StringComparison.OrdinalIgnoreCase)) throw new Exception("Salary selection must translate to PostgreSQL");
var rowType = typeof(BaoCaoReportService).Assembly.GetType("OvertimeHotel.HRM.WebAdmin.Services.NhanSuRow")!;
var rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rowType))!;
void AddRow(string status, DateOnly hired, DateOnly? left, decimal salary)
{
    var row = Activator.CreateInstance(rowType)!;
    foreach (var (property, value) in new Dictionary<string, object?>
    {
        ["TrangThai"] = status, ["NgayVaoLam"] = hired, ["NgayThoiViec"] = left,
        ["LuongCoBan"] = salary, ["TrinhDo"] = "Đại học"
    }) rowType.GetProperty(property)!.SetValue(row, value);
    rows.Add(row);
}
AddRow("DANG_LAM", new(2025, 1, 1), null, 10_000_000m);
AddRow("THOI_VIEC", new(2025, 1, 1), new(2026, 10, 15), 30_000_000m);
AddRow("THOI_VIEC", new(2025, 1, 1), new(2026, 11, 1), 20_000_000m);
AddRow("DANG_LAM", new(2026, 11, 1), null, 50_000_000m);
AddRow("DANG_LAM", new(2025, 1, 1), null, 0m);
var build = typeof(BaoCaoReportService).GetMethod("BuildHocVanThamNien", BindingFlags.NonPublic | BindingFlags.Static)!;
var report = (HocVanThamNienReport)build.Invoke(null, [rows, to, true])!;
if (report.SoDaiHocTroLen != 3 || report.SoNhanSuCoLuong != 2 || report.LuongCoBanTrungBinh != 15_000_000m)
    throw new Exception("Education and salary must use the same employees active at the reporting date");
if (report.SoNhanSuThieuLuong != 1) throw new Exception("Missing salary must be reported separately");
var managerReport = (HocVanThamNienReport)build.Invoke(null, [rows, to, false])!;
if (managerReport.CanViewSalaryInfo || managerReport.TheoMucLuong.Count != 0 || managerReport.LuongCoBanTrungBinh != 0)
    throw new Exception("Salary distributions must be absent for unauthorized roles");
var exportModel = new BaoCaoViewModel
{
    Tab = "co-cau", NhanKy = "Tháng 10/2026", CoCau = new() { CoNghiPhepTrongKy = 2 },
    BienDong = new() { CuoiKy = 3, NghiViec = 1 }
};
var csvMethod = typeof(BaoCaoController).GetMethod("BuildCsv", BindingFlags.NonPublic | BindingFlags.Static)!;
var csvExport = ((string, byte[]))csvMethod.Invoke(null, [exportModel])!;
var csv = Encoding.UTF8.GetString(csvExport.Item2);
if (!csv.Contains("Có nghỉ phép trong kỳ") || !csv.Contains("Nhân sự cuối kỳ"))
    throw new Exception("CSV must explain monthly leave and end-of-period headcount");
var excelMethod = typeof(BaoCaoController).GetMethod("BuildExcelFile", BindingFlags.NonPublic | BindingFlags.Static)!;
var excelExport = ((string, byte[]))excelMethod.Invoke(null, [exportModel])!;
using var archive = new ZipArchive(new MemoryStream(excelExport.Item2));
using var sheet = new StreamReader(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
var sheetXml = sheet.ReadToEnd();
if (!sheetXml.Contains("Có nghỉ phép trong kỳ") || !sheetXml.Contains("Nhân sự cuối kỳ"))
    throw new Exception("Excel must include the same monthly indicators as CSV");
Console.WriteLine("Monthly personnel report rules passed.");

// A malformed salary record must stay explicit without masquerading as a database outage.
using var reportCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
var salaryReportService = new BaoCaoReportService(database, reportCache, Microsoft.Extensions.Logging.Abstractions.NullLogger<BaoCaoReportService>.Instance);
var salarySegmentsMethod = typeof(BaoCaoReportService).GetMethod("SalarySegmentsForReport", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Reports need an explicit missing-salary path");
IReadOnlyList<SalarySegment>? ReportSegments(params HopDong[] contracts) =>
    (IReadOnlyList<SalarySegment>?)salarySegmentsMethod.Invoke(salaryReportService, [contracts, from, to]);
var zeroContract = new HopDong { MaHopDong = 2319, NgayBatDau = from, NgayKetThuc = to, TrangThai = "DA_CHAM_DUT", LuongCoBan = 0 };
if (ReportSegments(zeroContract) != null) throw new Exception("Zero salary must be an unknown reference, never a valid zero budget");
var validContract = new HopDong { MaHopDong = 2, NgayBatDau = from, NgayKetThuc = to, LuongCoBan = 10_000_000m };
validContract.PhuLucLuongs.Add(new PhuLucLuong { NgayHieuLuc = from, LuongCoBanMoi = 11_000_000m });
if (ReportSegments(validContract) is not { Count: 1 } validSegments || validSegments[0].Salary.MonthlySalary != 11_000_000m)
    throw new Exception("Valid report references must use the effective appendix");
if (ReportSegments(validContract, zeroContract) != null) throw new Exception("Overlapping history must require reconciliation");
if (ReportSegments() is not { Count: 0 }) throw new Exception("No contract must remain distinct from invalid history");
try { EffectiveSalaryResolver.Resolve([zeroContract], from); throw new Exception("Payroll must still reject zero salary"); }
catch (InvalidOperationException) { }
Console.WriteLine("Report salary regression cases passed: missing salary, overlaps, valid appendix, no contract and strict payroll.");
var incompleteReference = new BaoCaoViewModel
{
    Tab = "quy-luong", NhanKy = "Tháng 11/2026", CanViewPayrollFund = true,
    QuyLuong = new QuyLuongNganSachReport { SoNhanSuThieuCanCuLuong = 1, GhiChuPhuongPhap = "Có 1 hồ sơ lương chưa hợp lệ; định mức chưa đầy đủ." }
};
var incompleteCsv = ((string, byte[]))csvMethod.Invoke(null, [incompleteReference])!;
if (!Encoding.UTF8.GetString(incompleteCsv.Item2).Contains("Định mức (hồ sơ hợp lệ)") || !Encoding.UTF8.GetString(incompleteCsv.Item2).Contains("định mức chưa đầy đủ"))
    throw new Exception("CSV must disclose incomplete salary references");
var incompleteExcel = ((string, byte[]))excelMethod.Invoke(null, [incompleteReference])!;
using var incompleteArchive = new ZipArchive(new MemoryStream(incompleteExcel.Item2));
using var incompleteSheet = new StreamReader(incompleteArchive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
var incompleteXml = incompleteSheet.ReadToEnd();
if (!incompleteXml.Contains("Định mức (hồ sơ hợp lệ)") || !incompleteXml.Contains("định mức chưa đầy đủ"))
    throw new Exception("Excel must disclose incomplete salary references");
Console.WriteLine("CSV and XLSX preserve incomplete-reference warnings.");
