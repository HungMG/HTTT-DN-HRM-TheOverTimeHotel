using OvertimeHotel.HRM.WebAdmin.Services;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Policy = PermissionCodes.ManageContracts)]
public partial class HopDongController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<HopDongController> _logger;

    public HopDongController(AppDbContext context, ILogger<HopDongController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? keyword, string? contractFilter, int? departmentId = null, int page = 1, int pageSize = 10, bool isPartial = false)
    {
        keyword = keyword?.Trim();
        contractFilter = string.IsNullOrWhiteSpace(contractFilter) ? null : contractFilter;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 50);

        var today = DateOnly.FromDateTime(DateTime.Today);
        try
        {
            // Thực thi tuần tự trên cùng DbContext instance để đảm bảo an toàn đơn luồng (Thread-safe)
            var allContracts = await this.SearchDataAsync("contracts:list", () => _context.HopDongs.AsNoTracking()
                .Include(c => c.NhanVien)
                    .ThenInclude(nv => nv!.PhongBan)
                .Include(c => c.NhanVien)
                    .ThenInclude(nv => nv!.ChucVu)
                .ToListAsync());
            var allEmployees = await this.SearchDataAsync("contracts:employees", () => _context.NhanViens.AsNoTracking()
                .Include(nv => nv.PhongBan)
                .Include(nv => nv.ChucVu)
                .OrderBy(item => item.Ten).ThenBy(item => item.Ho)
                .ToListAsync());
            var allDepartments = await this.SearchDataAsync("contracts:departments", () => _context.PhongBans.AsNoTracking()
                .OrderBy(p => p.TenPhongBan)
                .ToListAsync());

            // Lọc trên bộ nhớ máy chủ (0ms)
            var filtered = allContracts.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var value = keyword.ToLower();
                // Đường dự phòng đọc trên bộ nhớ — vẫn cần vì dữ liệu cũ có thể
                // còn câu tiếng Việt trong cột thô.
                var maLoaiKhop = ContractTypes.MatchingCodes(keyword);
                filtered = filtered.Where(contract =>
                    contract.SoHopDong.ToLower().Contains(value) ||
                    maLoaiKhop.Contains(contract.LoaiHopDong) ||
                    contract.LoaiHopDong.ToLower().Contains(value) ||
                    (contract.NhanVien != null && (contract.NhanVien.Ho.ToLower().Contains(value) || contract.NhanVien.Ten.ToLower().Contains(value))));
            }

            if (departmentId is > 0)
            {
                filtered = filtered.Where(contract => contract.NhanVien != null && contract.NhanVien.MaPhongBan == departmentId.Value);
            }

            bool IsRenewed(HopDong contract) => allContracts.Any(other => other.MaNhanVien == contract.MaNhanVien &&
                                                          other.MaHopDong != contract.MaHopDong &&
                                                          other.NgayBatDau >= (contract.NgayKetThuc ?? contract.NgayBatDau) &&
                                                          (other.TrangThai == "HIEU_LUC" || !other.NgayKetThuc.HasValue || other.NgayKetThuc.Value >= today));

            var items = filtered.Select(contract =>
            {
                var days = contract.NgayKetThuc is DateOnly endDate ? endDate.DayNumber - today.DayNumber : (int?)null;
                var isRenewed = IsRenewed(contract);
                return new HopDongListItemViewModel
                {
                    Contract = contract,
                    EmployeeName = contract.NhanVien?.HoTen ?? "Nhân viên không tồn tại",
                    DepartmentName = contract.NhanVien?.PhongBan?.TenPhongBan ?? "Khách sạn",
                    PositionName = contract.NhanVien?.ChucVu?.TenChucVu ?? "Chưa phân bổ",
                    PersonalEmail = contract.NhanVien?.EmailCaNhan,
                    DaysRemaining = days,
                    State = ContractState.Classify(contract, today),
                    IsRenewed = isRenewed,
                    EmployeeHasOtherContract = allContracts.Any(other => other.MaNhanVien == contract.MaNhanVien && other.MaHopDong != contract.MaHopDong
                        && other.TrangThai != "DA_CHAM_DUT" && other.TrangThai != "CHAM_DUT" && (!other.NgayKetThuc.HasValue || other.NgayKetThuc.Value >= today))
                };
            });

            // Một bộ lọc trạng thái duy nhất; mặc định chỉ hiện hợp đồng đang làm việc.
            items = contractFilter switch
            {
                "SAP_HET_HAN" => items.Where(item => item.State == "SAP_HET_HAN"),
                "CAN_XU_LY" => items.Where(item => item.State == "DA_HET_HAN" && !item.IsRenewed),
                "CHUA_HIEU_LUC" => items.Where(item => item.State == "CHUA_HIEU_LUC"),
                "DA_CHAM_DUT" => items.Where(item => item.State == "DA_CHAM_DUT"),
                "TAT_CA" => items,
                _ => items.Where(item => item.State is "HIEU_LUC" or "SAP_HET_HAN" or "CHUA_HIEU_LUC")
            };

            var employeeOptions = allEmployees.Select(item => new SelectListItem($"{item.Ho} {item.Ten} - NV-{item.MaNhanVien:D3}", item.MaNhanVien.ToString())).ToList();
            var departmentOptions = allDepartments.Select(d => new SelectListItem(d.TenPhongBan, d.MaPhongBan.ToString(), d.MaPhongBan == departmentId)).ToList();
            var positionOptions = new List<SelectListItem>();

            ViewBag.EmployeeOptions = employeeOptions;
            ViewBag.DepartmentOptions = departmentOptions;
            ViewBag.PositionOptions = positionOptions;
            var employeeItems = allEmployees.Select(e => new HopDongEmployeeOptionItem
            {
                Id = e.MaNhanVien,
                Code = $"NV-{e.MaNhanVien:D3}",
                FullName = e.HoTen,
                DepartmentId = e.MaPhongBan,
                DepartmentName = e.PhongBan?.TenPhongBan ?? "Chưa phân bổ",
                PositionId = e.MaChucVu,
                PositionName = e.ChucVu?.TenChucVu ?? "Chưa phân bổ",
                PersonalEmail = e.EmailCaNhan ?? "Chưa cập nhật email cá nhân",
                TrinhDo = e.TrinhDo
            }).ToList();
            ViewBag.EmployeeList = employeeItems;

            var orderedContracts = items.OrderByDescending(i => i.Contract.NgayBatDau).ToList();
            var totalFiltered = orderedContracts.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltered / (double)pageSize));
            var pageNumber = Math.Clamp(page, 1, totalPages);
            var pagedContracts = orderedContracts.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            var initialSoHopDong = await this.SearchDataAsync($"contracts:next-number:{today:yyyyMMdd}", () => GenerateUniqueContractNumberAsync("CHINH_THUC"));
            var model = new HopDongIndexViewModel
            {
                CreateContractForm = new HopDongFormViewModel
                {
                    Contract = new HopDong
                    {
                        SoHopDong = initialSoHopDong,
                        NgayBatDau = today,
                        NgayKetThuc = today.AddYears(1),
                        TrangThai = "HIEU_LUC",
                        LoaiHopDong = "XAC_DINH_THOI_HAN_1_NAM",
                        HinhThucHopDong = "CHINH_THUC"
                    },
                    DepartmentOptions = departmentOptions,
                    PositionOptions = positionOptions,
                    EmployeeOptions = employeeOptions,
                    EmployeeList = employeeItems
                },
                Keyword = keyword,
                ContractFilter = contractFilter,
                DepartmentId = departmentId,
                DepartmentOptions = departmentOptions,
                Contracts = pagedContracts,
                EmployeeOptions = employeeOptions,
                TotalContracts = allContracts.Count,
                TotalFilteredContracts = totalFiltered,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                ActiveContracts = allContracts.Count(contract => ContractState.Classify(contract, today) is "HIEU_LUC" or "SAP_HET_HAN" or "CHUA_HIEU_LUC"),
                ExpiringContracts = allContracts.Count(contract => ContractState.Classify(contract, today) == "SAP_HET_HAN"),
                // "Cần xử lý": hết hạn nhưng chưa có hợp đồng nối tiếp (cùng điều kiện với bộ lọc CAN_XU_LY).
                ExpiredContracts = allContracts.Count(contract => ContractState.Classify(contract, today) == "DA_HET_HAN" && !IsRenewed(contract))
            };

            if (isPartial || Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                return PartialView("_ContractTablePartial", model);
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi truy vấn danh sách hợp đồng từ CSDL Supabase.");
            TempData["Error"] = "Lỗi kết nối CSDL Supabase: " + ex.Message;
            ViewBag.EmployeeOptions = new List<SelectListItem>();
            ViewBag.DepartmentOptions = new List<SelectListItem>();
            ViewBag.EmployeeList = new List<object>();
            var errorModel = new HopDongIndexViewModel
            {
                CreateContractForm = new HopDongFormViewModel
                {
                    Contract = new HopDong
                    {
                        SoHopDong = $"HDLD-{today.Year}/01/OTH",
                        NgayBatDau = today,
                        NgayKetThuc = today.AddYears(1),
                        TrangThai = "HIEU_LUC",
                        LoaiHopDong = "XAC_DINH_THOI_HAN_1_NAM",
                        HinhThucHopDong = "CHINH_THUC"
                    }
                },
                Keyword = keyword,
                ContractFilter = contractFilter,
                DepartmentId = departmentId,
                DepartmentOptions = new List<SelectListItem>(),
                Contracts = new List<HopDongListItemViewModel>(),
                EmployeeOptions = new List<SelectListItem>()
            };

            if (isPartial || Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                return PartialView("_ContractTablePartial", errorModel);
            }

            return View(errorModel);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(HopDongFormViewModel model)
    {
        ViewBag.EmployeeOptions = await GetEmployeeOptionsAsync();
        if (model.EmployeeId == 0) model.EmployeeId = null;
        ModelState.Remove("Contract.MaNhanVien");
        model.Contract.MaHopDong = 0;

        if (Request.HasFormContentType && Request.Form.TryGetValue("Contract.LuongCoBan", out var rawSalary))
        {
            var cleanSalary = rawSalary.ToString().Replace(",", "").Trim();
            if (decimal.TryParse(cleanSalary, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedSalary))
            {
                model.Contract.LuongCoBan = parsedSalary;
                ModelState.Remove("Contract.LuongCoBan");
            }
        }

        if (string.IsNullOrWhiteSpace(model.Contract.HinhThucHopDong))
        {
            model.Contract.HinhThucHopDong = "CHINH_THUC";
        }

        NhanVien? employee = null;
        if (model.EmployeeId.HasValue)
        {
            employee = await _context.NhanViens.FindAsync(model.EmployeeId.Value);
            if (employee == null) ModelState.AddModelError("EmployeeId", "Nhân viên đã chọn không tồn tại.");
        }
        else if (string.IsNullOrWhiteSpace(model.EmployeeName))
        {
            ModelState.AddModelError("EmployeeName", "Vui lòng nhập họ và tên nhân viên.");
        }

        await ValidateOrganizationAsync(model);
        if (employee == null && !model.EmployeeId.HasValue && !string.IsNullOrWhiteSpace(model.EmployeeName))
        {
            var parts = model.EmployeeName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var ho = parts.Length > 1 ? string.Join(" ", parts.Take(parts.Length - 1)) : string.Empty;
            var ten = parts.Last();
            employee = new NhanVien
            {
                Ho = ho, Ten = ten, IsProfileComplete = false,
                EmailCongVu = await GenerateUniqueWorkEmailAsync(ho, ten),
                NgayVaoLam = model.Contract.NgayBatDau,
                TrangThai = "DANG_LAM"
            };
        }
        model.Contract.MaNhanVien = employee?.MaNhanVien ?? 0;

        // Tự động sinh mã hợp đồng nếu để trống theo hình thức hợp đồng
        if (string.IsNullOrWhiteSpace(model.Contract.SoHopDong))
        {
            model.Contract.SoHopDong = await GenerateUniqueContractNumberAsync(model.Contract.HinhThucHopDong);
        }

        // Lập mới chỉ tạo hợp đồng đã ký. Hết hạn được suy ra từ ngày;
        // chấm dứt/thanh lý được xử lý sau khi hợp đồng đã tồn tại.
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (string.IsNullOrWhiteSpace(model.Contract.TrangThai))
            model.Contract.TrangThai = "HIEU_LUC";
        if (model.Contract.TrangThai != "HIEU_LUC")
            ModelState.AddModelError("Contract.TrangThai", "Hợp đồng mới không được ở trạng thái hết hạn, thanh lý hoặc chấm dứt.");
        HopDong? sameDayPromotionContract = null;
        if (model.ClosePreviousContract && employee?.MaNhanVien > 0 && model.PositionId.HasValue)
        {
            var position = await _context.ChucVus.FindAsync(model.PositionId);
            if (IsManagerPosition(position?.TenChucVu) && await _context.TaiKhoans.AnyAsync(t =>
                    t.MaNhanVien == employee.MaNhanVien && t.VaiTro!.TenVaiTro == "Employee"))
            {
                var matches = await _context.HopDongs.Where(h => h.MaNhanVien == employee.MaNhanVien &&
                    h.TrangThai == "HIEU_LUC" && h.NgayBatDau == model.Contract.NgayBatDau).Take(2).ToListAsync();
                if (matches.Count == 1)
                {
                    sameDayPromotionContract = matches[0];
                    model.Contract.SoHopDong = sameDayPromotionContract.SoHopDong;
                }
            }
        }
        await ValidateSalaryAsync(model, employee);
        await ValidateContractAsync(model.Contract, model.ClosePreviousContract, sameDayPromotionContract?.MaHopDong);
        if (model.Contract.NgayKetThuc < today)
            ModelState.AddModelError("Contract.NgayKetThuc", "Hợp đồng mới không được có ngày kết thúc trong quá khứ.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(item => item.Errors).FirstOrDefault()?.ErrorMessage ?? "Vui lòng kiểm tra lại thông tin hợp đồng.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var oldContractValues = sameDayPromotionContract == null ? null : System.Text.Json.JsonSerializer.Serialize(new
            {
                sameDayPromotionContract.LoaiHopDong, sameDayPromotionContract.HinhThucHopDong,
                sameDayPromotionContract.NgayBatDau, sameDayPromotionContract.NgayKetThuc, sameDayPromotionContract.LuongCoBan,
                employee!.MaPhongBan, employee.MaChucVu
            });
            if (employee != null)
            {
                employee.TrinhDo = model.TrinhDo;
                employee.MaPhongBan = model.DepartmentId;
                employee.MaChucVu = model.PositionId;
                if (!string.IsNullOrWhiteSpace(model.PersonalEmail)) employee.EmailCaNhan = model.PersonalEmail.Trim();
                if (sameDayPromotionContract == null) model.Contract.NhanVien = employee;

                if (model.ClosePreviousContract)
                {
                    var olderContracts = await _context.HopDongs
                        .Where(item => item.MaNhanVien == employee.MaNhanVien && item.TrangThai != "DA_CHAM_DUT" && item.NgayBatDau < model.Contract.NgayBatDau && (item.NgayKetThuc == null || item.NgayKetThuc >= model.Contract.NgayBatDau))
                        .ToListAsync();

                    foreach (var old in olderContracts)
                    {
                        old.NgayKetThuc = model.Contract.NgayBatDau.AddDays(-1);
                        old.TrangThai = "DA_CHAM_DUT";
                        old.LoaiChamDut = ContractTerminationTypes.KHAC;
                        old.LyDoChamDut = "Chuyển tiếp ký hợp đồng mới / thăng chức";
                    }
                }
            }
            var targetPosition = await _context.ChucVus.FindAsync(model.PositionId);
            var employeeAccount = employee != null && employee.MaNhanVien > 0
                ? await _context.TaiKhoans.Include(t => t.VaiTro).FirstOrDefaultAsync(t => t.MaNhanVien == employee.MaNhanVien)
                : null;
            var isManagerPromotion = employeeAccount?.VaiTro?.TenVaiTro == "Employee" &&
                IsManagerPosition(targetPosition?.TenChucVu);

            if (sameDayPromotionContract == null)
                _context.HopDongs.Add(model.Contract);
            else
            {
                sameDayPromotionContract.LoaiHopDong = model.Contract.LoaiHopDong;
                sameDayPromotionContract.HinhThucHopDong = model.Contract.HinhThucHopDong;
                sameDayPromotionContract.NgayKetThuc = model.Contract.NgayKetThuc;
                sameDayPromotionContract.LuongCoBan = model.Contract.LuongCoBan;
                model.Contract = sameDayPromotionContract;
                int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var actorId);
                _context.NhatKyQuanTris.Add(new NhatKyQuanTri
                {
                    MaTaiKhoanThucHien = actorId > 0 ? actorId : null,
                    MaTaiKhoanBiTacDong = employeeAccount?.MaTaiKhoan,
                    TenNguoiThucHien = User.Identity?.Name ?? "Không xác định",
                    TenTaiKhoanBiTacDong = employeeAccount?.TenDangNhap ?? string.Empty,
                    HanhDong = "UPDATE_CONTRACT_PROMOTION",
                    NoiDung = $"Cập nhật hợp đồng {sameDayPromotionContract.SoHopDong} khi thăng chức cùng ngày.",
                    GiaTriCu = oldContractValues,
                    GiaTriMoi = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        sameDayPromotionContract.LoaiHopDong, sameDayPromotionContract.HinhThucHopDong,
                        sameDayPromotionContract.NgayBatDau, sameDayPromotionContract.NgayKetThuc, sameDayPromotionContract.LuongCoBan,
                        employee!.MaPhongBan, employee.MaChucVu
                    }),
                    ThoiGian = DateTimeOffset.UtcNow
                });
            }
            await _context.SaveChangesAsync();

            if (isManagerPromotion)
            {
                TempData["PromotionPrompt_EmployeeId"] = employee!.MaNhanVien;
                TempData["PromotionPrompt_EmployeeName"] = employee.HoTen;
                TempData["PromotionPrompt_ContractId"] = model.Contract.MaHopDong;
                TempData["PromotionPrompt_ContractNo"] = model.Contract.SoHopDong;
                TempData["PromotionPrompt_PositionName"] = targetPosition!.TenChucVu;
            }

            string emailNotice = "";
            var finalEmail = !string.IsNullOrWhiteSpace(model.PersonalEmail) ? model.PersonalEmail.Trim() : employee?.EmailCaNhan;
            if (model.EmailNotificationOption == "GUI_EMAIL")
            {
                if (!string.IsNullOrWhiteSpace(finalEmail))
                {
                    _logger.LogInformation("Đã gửi bản hợp đồng '{ContractNumber}' tới email cá nhân: {Email}", model.Contract.SoHopDong, finalEmail);
                    emailNotice = $" (Đã gửi văn bản hợp đồng tới email cá nhân: {finalEmail})";
                }
                else
                {
                    emailNotice = " (Chưa có email cá nhân để gửi thông báo hợp đồng)";
                }
            }

            TempData["Success"] = sameDayPromotionContract == null
                ? $"Đã lập hợp đồng “{model.Contract.SoHopDong}” cho {employee!.HoTen}.{emailNotice}"
                : $"Đã cập nhật hợp đồng “{model.Contract.SoHopDong}” của {employee!.HoTen} theo thông tin thăng chức.{emailNotice}";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is DbUpdateException or Npgsql.PostgresException)
        {
            var postgres = ex as Npgsql.PostgresException ?? ex.InnerException as Npgsql.PostgresException;
            if (postgres is { SqlState: "P0001" } && postgres.MessageText.Contains("General director appointments overlap", StringComparison.Ordinal))
            {
                _logger.LogWarning("Từ chối hợp đồng {ContractNumber}: trùng thời gian giữ chức Tổng Giám Đốc.", model.Contract.SoHopDong);
                TempData["Error"] = "Hệ thống đã có Tổng Giám Đốc trong thời gian này. Không thể tạo thêm Tổng Giám Đốc; hãy chọn chức vụ khác hoặc kiểm tra lịch điều chuyển.";
            }
            else
            {
                _logger.LogError(ex, "Không thể tạo hợp đồng {ContractNumber}.", model.Contract.SoHopDong);
                TempData["Error"] = postgres?.SqlState == "23505"
                    ? "Không thể lập hợp đồng vì số hợp đồng hoặc định danh nhân viên đã tồn tại. Vui lòng kiểm tra lại."
                    : "Không thể lưu hợp đồng vào cơ sở dữ liệu. Vui lòng kiểm tra kết nối hoặc liên hệ quản trị viên.";
            }
            // The implicit transaction has rolled back; discard generated IDs and rejected entities.
            _context.ChangeTracker.Clear();
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Terminate(int id, HopDongTerminateViewModel model)
    {
        var contract = await _context.HopDongs
            .Include(item => item.NhanVien)
            .FirstOrDefaultAsync(item => item.MaHopDong == id);
        if (contract == null)
            return NotFound();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var state = ContractState.Classify(contract, today);

        string? error = null;
        if (state == "DA_CHAM_DUT")
            error = "Hợp đồng này đã được chấm dứt trước đó.";
        else if (!ContractTerminationTypes.IsValid(model.LoaiChamDut))
            error = "Vui lòng chọn loại chấm dứt.";
        else if (model.LoaiChamDut == ContractTerminationTypes.KHAC && string.IsNullOrWhiteSpace(model.LyDoChamDut))
            error = "Vui lòng nhập ghi chú khi chọn loại chấm dứt \"Khác\".";

        var otherContracts = await _context.HopDongs
            .AsNoTracking()
            .Where(item => item.MaNhanVien == contract.MaNhanVien && item.MaHopDong != contract.MaHopDong
                           && item.TrangThai != "DA_CHAM_DUT" && item.TrangThai != "CHAM_DUT")
            .ToListAsync();

        // Đã có hợp đồng nối tiếp thì hợp đồng này đã được gia hạn, không còn gì để chấm dứt.
        if (error == null && otherContracts.Any(other => other.NgayBatDau >= (contract.NgayKetThuc ?? contract.NgayBatDau)))
            error = "Hợp đồng này đã được gia hạn bằng hợp đồng mới nên không thể chấm dứt.";

        // Ngày chấm dứt: hợp đồng chưa bắt đầu hoặc đã hết hạn thì cố định, còn lại do người dùng chọn
        // nhưng phải nằm trong thời hạn hợp đồng và không ở tương lai.
        DateOnly endDate = default;
        if (error == null)
        {
            if (state == "CHUA_HIEU_LUC") endDate = contract.NgayBatDau;
            else if (state == "DA_HET_HAN") endDate = contract.NgayKetThuc!.Value;
            else
            {
                endDate = model.NgayChamDut ?? today;
                if (endDate < contract.NgayBatDau || endDate > today || (contract.NgayKetThuc.HasValue && endDate > contract.NgayKetThuc.Value))
                    error = "Ngày chấm dứt phải nằm trong thời hạn hợp đồng và không được ở tương lai.";
            }
        }

        if (error != null)
            return TerminateResult(false, error, StatusCodes.Status422UnprocessableEntity);

        contract.TrangThai = "DA_CHAM_DUT";
        contract.NgayKetThuc = endDate;
        contract.LoaiChamDut = model.LoaiChamDut;
        contract.LyDoChamDut = string.IsNullOrWhiteSpace(model.LyDoChamDut) ? null : model.LyDoChamDut.Trim();

        // Nhân viên chỉ nghỉ việc khi không còn hợp đồng nào khác còn hiệu lực hoặc sắp tới.
        var hasOtherActive = otherContracts.Any(other => !other.NgayKetThuc.HasValue || other.NgayKetThuc.Value >= today);
        var employee = contract.NhanVien;
        var employeeLeft = false;
        if (employee != null && !hasOtherActive)
        {
            employee.TrangThai = "DA_THOI_VIEC";
            employee.NgayThoiViec = endDate;
            employeeLeft = true;
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể chấm dứt hợp đồng {ContractId}.", id);
            return TerminateResult(false, "Không thể chấm dứt hợp đồng. Vui lòng kiểm tra lại dữ liệu.", StatusCodes.Status409Conflict);
        }

        var message = $"Đã chấm dứt hợp đồng “{contract.SoHopDong}”." +
                      (employeeLeft ? " Nhân viên đã chuyển sang trạng thái nghỉ việc." : " Nhân viên vẫn còn hợp đồng khác nên giữ trạng thái đang làm.");
        return TerminateResult(true, message);
    }

    private IActionResult TerminateResult(bool success, string message, int statusCode = StatusCodes.Status200OK)
    {
        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            return StatusCode(statusCode, new { success, message });
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GenerateContractNumber(string? hinhThuc)
    {
        var contractNumber = await GenerateUniqueContractNumberAsync(hinhThuc);
        return Json(new { success = true, contractNumber });
    }

    [HttpGet]
    public async Task<IActionResult> DetailsModal(int id)
    {
        var contract = await _context.HopDongs
            .AsNoTracking()
            .Include(c => c.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .Include(c => c.NhanVien)
                .ThenInclude(nv => nv!.ChucVu)
            .FirstOrDefaultAsync(c => c.MaHopDong == id);

        if (contract == null || contract.NhanVien == null)
            return NotFound("Không tìm thấy thông tin hợp đồng.");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var daysRemaining = contract.NgayKetThuc.HasValue
            ? (contract.NgayKetThuc.Value.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days
            : (int?)null;

        var state = ContractState.Classify(contract, today);

        var isRenewed = await _context.HopDongs
            .AsNoTracking()
            .AnyAsync(c => c.MaNhanVien == contract.MaNhanVien && c.MaHopDong != contract.MaHopDong && c.NgayBatDau >= (contract.NgayKetThuc ?? contract.NgayBatDau));

        var previousContract = await _context.HopDongs
            .AsNoTracking()
            .Where(c => c.MaNhanVien == contract.MaNhanVien && c.MaHopDong != contract.MaHopDong && c.NgayBatDau < contract.NgayBatDau)
            .OrderByDescending(c => c.NgayBatDau)
            .FirstOrDefaultAsync();

        var nextContract = await _context.HopDongs
            .AsNoTracking()
            .Where(c => c.MaNhanVien == contract.MaNhanVien && c.MaHopDong != contract.MaHopDong && c.NgayBatDau > contract.NgayBatDau)
            .OrderBy(c => c.NgayBatDau)
            .FirstOrDefaultAsync();

        var model = new HopDongDetailsModalViewModel
        {
            Contract = contract,
            Employee = contract.NhanVien,
            DepartmentName = contract.NhanVien.PhongBan?.TenPhongBan ?? "Chưa phân bổ",
            PositionName = contract.NhanVien.ChucVu?.TenChucVu ?? "Chưa phân bổ",
            DaysRemaining = daysRemaining,
            State = state,
            IsRenewed = isRenewed,
            PreviousContract = previousContract,
            NextContract = nextContract
        };

        return PartialView("_ContractDetailsModalContent", model);
    }

    [HttpGet]
    public async Task<IActionResult> Print(int id)
    {
        var contract = await _context.HopDongs
            .AsNoTracking()
            .Include(c => c.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .Include(c => c.NhanVien)
                .ThenInclude(nv => nv!.ChucVu)
            .FirstOrDefaultAsync(c => c.MaHopDong == id);

        if (contract == null || contract.NhanVien == null)
            return NotFound("Không tìm thấy thông tin hợp đồng để in.");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var daysRemaining = contract.NgayKetThuc.HasValue
            ? (contract.NgayKetThuc.Value.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days
            : (int?)null;

        var model = new HopDongDetailsModalViewModel
        {
            Contract = contract,
            Employee = contract.NhanVien,
            DepartmentName = contract.NhanVien.PhongBan?.TenPhongBan ?? "Khách sạn The OverTime",
            PositionName = contract.NhanVien.ChucVu?.TenChucVu ?? "Nhân viên",
            DaysRemaining = daysRemaining
        };

        return View("Print", model);
    }


    private async Task ValidateContractAsync(HopDong model, bool closePreviousContract = false, int? currentId = null)
    {
        model.LoaiHopDong = model.LoaiHopDong?.Trim() ?? string.Empty;
        model.HinhThucHopDong = model.HinhThucHopDong?.Trim() ?? "CHINH_THUC";

        // Tự động sinh mã hợp đồng nếu để trống
        if (string.IsNullOrWhiteSpace(model.SoHopDong))
        {
            model.SoHopDong = await GenerateUniqueContractNumberAsync(model.HinhThucHopDong);
        }
        else
        {
            model.SoHopDong = model.SoHopDong.Trim().ToUpper();
        }

        var duration = ContractTypes.Normalize(model.LoaiHopDong);
        if (model.HinhThucHopDong is not ("CHINH_THUC" or "THU_VIEC"))
            ModelState.AddModelError("Contract.HinhThucHopDong", "Vui lòng chọn giai đoạn hợp đồng hợp lệ.");
        if (model.HinhThucHopDong == "THU_VIEC")
        {
            if (duration != ContractTypes.THU_VIEC)
                ModelState.AddModelError("Contract.LoaiHopDong", "Thử việc phải dùng thời hạn theo ngày bắt đầu và ngày kết thúc.");
            if (!model.NgayKetThuc.HasValue)
                ModelState.AddModelError("Contract.NgayKetThuc", "Vui lòng nhập ngày kết thúc thử việc.");
        }
        else if (duration is null or ContractTypes.THU_VIEC)
            ModelState.AddModelError("Contract.LoaiHopDong", "Vui lòng chọn thời hạn hợp đồng chính thức: 1 năm, 3 năm hoặc không xác định thời hạn.");
        if (duration != null) model.LoaiHopDong = duration;
        if (model.NgayBatDau == default)
            ModelState.AddModelError("Contract.NgayBatDau", "Vui lòng nhập ngày bắt đầu hợp đồng.");
        if (model.HinhThucHopDong == "CHINH_THUC" && !model.NgayKetThuc.HasValue && model.NgayBatDau != default)
        {
            var years = duration switch
            {
                ContractTypes.XAC_DINH_THOI_HAN_1_NAM => 1,
                ContractTypes.XAC_DINH_THOI_HAN_3_NAM => 3,
                _ => 0
            };
            if (years > 0)
            {
                try { model.NgayKetThuc = model.NgayBatDau.AddYears(years); }
                catch (ArgumentOutOfRangeException)
                {
                    ModelState.AddModelError("Contract.NgayBatDau", "Ngày bắt đầu không phù hợp với thời hạn hợp đồng đã chọn.");
                }
            }
        }
        if (model.NgayKetThuc.HasValue && model.NgayKetThuc < model.NgayBatDau) ModelState.AddModelError("Contract.NgayKetThuc", "Ngày kết thúc phải sau ngày bắt đầu.");
        if (model.LuongCoBan < 0) ModelState.AddModelError("Contract.LuongCoBan", "Lương cơ bản không được âm.");
        if (await _context.HopDongs.AnyAsync(item => item.MaHopDong != currentId && item.SoHopDong.ToLower() == model.SoHopDong.ToLower()))
        {
            ModelState.AddModelError("Contract.SoHopDong", $"Mã / Số hợp đồng '{model.SoHopDong}' đã tồn tại trong hệ thống. Vui lòng bấm 'Tạo mã mới' để nhận mã ngẫu nhiên duy nhất.");
        }

        // Chặn trùng lặp / chồng chéo thời gian hợp đồng cho cùng 1 nhân sự
        if (model.MaNhanVien > 0)
        {
            var otherContracts = await _context.HopDongs
                .AsNoTracking()
                .Where(item => item.MaNhanVien == model.MaNhanVien && item.MaHopDong != currentId && item.TrangThai != "DA_CHAM_DUT")
                .ToListAsync();

            foreach (var other in otherContracts)
            {
                bool overlap = false;
                var mStart = model.NgayBatDau;
                var mEnd = model.NgayKetThuc;
                var oStart = other.NgayBatDau;
                var oEnd = other.NgayKetThuc;

                if (!mEnd.HasValue && !oEnd.HasValue)
                {
                    overlap = true;
                }
                else if (!mEnd.HasValue)
                {
                    overlap = oEnd >= mStart;
                }
                else if (!oEnd.HasValue)
                {
                    overlap = mEnd >= oStart;
                }
                else
                {
                    overlap = (mStart <= oEnd.Value) && (oStart <= mEnd.Value);
                }

                if (overlap)
                {
                    // Nếu người dùng đồng ý khép HĐ cũ và HĐ mới bắt đầu sau ngày bắt đầu của HĐ cũ -> Hợp lệ chuyển tiếp/thăng chức
                    if (closePreviousContract && mStart > oStart)
                    {
                        continue;
                    }

                    var oEndStr = other.NgayKetThuc.HasValue ? other.NgayKetThuc.Value.ToString("dd/MM/yyyy") : "Không thời hạn";
                    ModelState.AddModelError("Contract.NgayBatDau",
                        $"Nhân sự này đã có hợp đồng '{other.SoHopDong}' ({other.NgayBatDau:dd/MM/yyyy} - {oEndStr}) đang có hiệu lực. Nếu đây là HĐ thăng chức/chuyển tiếp, vui lòng tích chọn 'Chuyển tiếp / Thăng chức' ở bên dưới.");
                    break;
                }
            }
        }
    }

    internal static bool IsManagerPosition(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var t = title.ToLowerInvariant();
        if (t.Contains("giám đốc") || t.Contains("general manager") || t.Contains("director")) return false;
        return t.Contains("trưởng") || t.Contains("quản lý") ||
               t.Contains("manager") || t.Contains("chef");
    }

    private async Task<string> GenerateUniqueContractNumberAsync(string? hinhThucHopDong)
    {
        var prefix = hinhThucHopDong == "THU_VIEC" ? "HDTV" : "HDLD";
        var year = DateTime.Now.Year;
        var random = new Random();

        // 1. Thử sinh số ngẫu nhiên 2 chữ số (01-99) có kiểm tra tồn tại
        for (int i = 0; i < 50; i++)
        {
            var randomNum = random.Next(1, 100);
            var candidate = $"{prefix}-{year}/{randomNum:D2}/OTH";
            var exists = await _context.HopDongs.AnyAsync(h => h.SoHopDong.ToLower() == candidate.ToLower());
            if (!exists)
            {
                return candidate;
            }
        }

        // 2. Thử sinh số ngẫu nhiên 3 chữ số (100-999) có kiểm tra tồn tại
        for (int i = 0; i < 50; i++)
        {
            var randomNum = random.Next(100, 1000);
            var candidate = $"{prefix}-{year}/{randomNum:D3}/OTH";
            var exists = await _context.HopDongs.AnyAsync(h => h.SoHopDong.ToLower() == candidate.ToLower());
            if (!exists)
            {
                return candidate;
            }
        }

        // 3. Fallback an toàn tuyệt đối: quét tuần tự đảm bảo luôn tìm được mã duy nhất không trùng lặp
        int seq = 1;
        while (true)
        {
            var candidate = $"{prefix}-{year}/{seq:D3}/OTH";
            var exists = await _context.HopDongs.AnyAsync(h => h.SoHopDong.ToLower() == candidate.ToLower());
            if (!exists)
            {
                return candidate;
            }
            seq++;
        }
    }

    private async Task<IReadOnlyList<SelectListItem>> GetEmployeeOptionsAsync() =>
        await _context.NhanViens.AsNoTracking().OrderBy(item => item.Ten).ThenBy(item => item.Ho).Select(item => new SelectListItem($"{item.Ho} {item.Ten} - NV-{item.MaNhanVien:D3}", item.MaNhanVien.ToString())).ToListAsync();

    private async Task ValidateOrganizationAsync(HopDongFormViewModel model)
    {
        var department = await _context.PhongBans.AsNoTracking().FirstOrDefaultAsync(x => x.MaPhongBan == model.DepartmentId);
        if (department == null)
            ModelState.AddModelError("DepartmentId", "Vui lòng chọn phòng ban hợp lệ.");
        if (!model.PositionId.HasValue || !await _context.ChucVus.Where(OrganizationPositionPolicy.ForDepartment(department)).AnyAsync(x => x.MaChucVu == model.PositionId))
            ModelState.AddModelError("PositionId", "Vui lòng chọn chức vụ thuộc phòng ban đã chọn.");
        if (!string.IsNullOrWhiteSpace(model.PersonalEmail) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(model.PersonalEmail))
            ModelState.AddModelError("PersonalEmail", "Email cá nhân không đúng định dạng.");
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
    }

    private async Task<string> GenerateUniqueWorkEmailAsync(string ho, string ten)
    {
        var cleanTen = RemoveDiacritics(ten).Trim().ToLowerInvariant();
        var cleanHo = RemoveDiacritics(ho).Trim().ToLowerInvariant().Replace(" ", "");
        cleanTen = Regex.Replace(cleanTen, @"[^a-z0-9]", "");
        cleanHo = Regex.Replace(cleanHo, @"[^a-z0-9]", "");
        if (string.IsNullOrEmpty(cleanTen)) cleanTen = "nhanvien";

        var baseEmail = string.IsNullOrEmpty(cleanHo)
            ? $"{cleanTen}@overtimehotel.com"
            : $"{cleanTen}.{cleanHo}@overtimehotel.com";

        if (!await _context.NhanViens.AnyAsync(e => e.EmailCongVu.ToLower() == baseEmail.ToLower()))
        {
            return baseEmail;
        }

        int counter = 1;
        while (true)
        {
            var candidate = string.IsNullOrEmpty(cleanHo)
                ? $"{cleanTen}{counter}@overtimehotel.com"
                : $"{cleanTen}.{cleanHo}{counter}@overtimehotel.com";
            if (!await _context.NhanViens.AnyAsync(e => e.EmailCongVu.ToLower() == candidate.ToLower()))
            {
                return candidate;
            }
            counter++;
        }
    }
}
