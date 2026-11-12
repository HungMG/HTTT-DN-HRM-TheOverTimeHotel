using OvertimeHotel.HRM.WebAdmin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Policy = PermissionCodes.CalculatePayroll)]
public class CauHinhLuongController(AppDbContext context, IMemoryCache cache,
    ILogger<CauHinhLuongController> logger) : Controller
{
    private readonly AppDbContext _context = context;

    private IQueryable<CauHinhLuongRow> Query(CauHinhLuongCategory category) => category switch
    {
        CauHinhLuongCategory.TangCa => _context.CauHinhTangCas.AsNoTracking()
            .Select(x => new CauHinhLuongRow { Id = x.MaLoaiOt, Name = x.TenLoaiOt, Type = "", Value = x.HeSo,
                Taxable = false, Description = x.MoTa,
                Used = x.ChiTietPhieuLuongs.Any(), UsageCount = x.ChiTietPhieuLuongs.Count() }),
        CauHinhLuongCategory.PhuCap => _context.CauHinhPhuCaps.AsNoTracking()
            .Select(x => new CauHinhLuongRow { Id = x.MaPhuCap, Name = x.TenPhuCap, Type = x.LoaiTinh, Value = x.MucDinhMuc,
                Taxable = x.ChiuThueTncn, Description = x.MoTa,
                Used = x.ChiTietPhieuLuongs.Any(), UsageCount = x.ChiTietPhieuLuongs.Count() }),
        CauHinhLuongCategory.KhauTru => _context.CauHinhKhauTrus.AsNoTracking()
            .Select(x => new CauHinhLuongRow { Id = x.MaKhoanTru, Name = x.TenKhoanTru, Type = x.LoaiKhoanTru, Value = x.TyLeHoacDinhMuc,
                Taxable = false, Description = x.MoTa,
                Used = x.ChiTietPhieuLuongs.Any(), UsageCount = x.ChiTietPhieuLuongs.Count() }),
        CauHinhLuongCategory.Thuong => _context.CauHinhKhoanLuongs.AsNoTracking().Where(x => x.LoaiKhoan == "THUONG")
            .Select(x => new CauHinhLuongRow
            {
                Id = x.MaKhoan, Name = x.TenKhoan,
                // Dòng hệ số sự kiện là tham số tính lương, không phải khoản thưởng chi trả.
                Type = x.MaKhoan == SalaryConstants.EVENT_HOURLY_RATE_CONFIG_ID
                    ? CauHinhLuongLabels.EventRateType : x.LoaiKhoan,
                Value = x.GiaTriMacDinh,
                Taxable = false, Description = x.MoTa,
                IsParameter = x.MaKhoan == SalaryConstants.EVENT_HOURLY_RATE_CONFIG_ID,
                Used = x.ChiTietPhieuLuongs.Any(), UsageCount = x.ChiTietPhieuLuongs.Count()
            }),
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    /// <summary>
    /// Dựng khối "Công thức &amp; tham số" cho từng danh mục.
    /// Giá trị hiển thị lấy từ cấu hình đang có trong CSDL; phần hệ số nền lấy từ
    /// <see cref="SalaryConstants"/> để HR thấy đúng công thức PRD 05 đang áp dụng.
    /// </summary>
    private static CauHinhLuongGuide BuildGuide(CauHinhLuongCategory category, IReadOnlyList<CauHinhLuongRow> items)
    {
        decimal Amount(int id, decimal fallback) => items.FirstOrDefault(x => x.Id == id)?.Value ?? fallback;

        var standardDays = new CauHinhLuongParameter
        {
            Label = "Ngày công chuẩn",
            Value = SalaryConstants.STANDARD_WORKING_DAYS.ToString(),
            Unit = "ngày/tháng",
            Hint = "Mẫu số quy đổi lương ngày và phân bổ phụ cấp cố định tháng"
        };

        return category switch
        {
            CauHinhLuongCategory.TangCa => new CauHinhLuongGuide
            {
                Formula = "Tiền tăng ca = (Lương cơ bản ÷ 26 ÷ 8) × Số giờ OT × Hệ số loại ngày",
                FormulaNote = "Đơn giá giờ dùng chung cho tăng ca, phụ cấp ca đêm và các khoản quy đổi theo giờ.",
                Parameters =
                [
                    standardDays,
                    new()
                    {
                        Label = "Giờ làm chuẩn",
                        Value = SalaryConstants.STANDARD_WORKING_HOURS_PER_DAY.ToString(),
                        Unit = "giờ/ngày",
                        Hint = "Mẫu số quy đổi lương giờ"
                    },
                    .. items.Select(item => new CauHinhLuongParameter
                    {
                        Label = item.Name,
                        Value = item.Value.ToString("N2") + "×",
                        Unit = "hệ số",
                        Hint = item.Description
                    })
                ],
                Rules =
                [
                    $"Hệ số ngày thường {Amount(1, SalaryConstants.OVERTIME_NORMAL_RATE):N2}×, ngày nghỉ tuần {Amount(2, SalaryConstants.OVERTIME_WEEKEND_RATE):N2}×, ngày Lễ Tết {Amount(3, SalaryConstants.OVERTIME_HOLIDAY_RATE):N2}× — tra theo đúng loại OT ghi nhận khi chấm công.",
                    $"Hệ số {Amount(3, SalaryConstants.OVERTIME_HOLIDAY_RATE):N2}× chỉ áp dụng khi nhân viên thực sự đi làm ngày Lễ, Tết; nghỉ lễ hưởng lương theo quy định.",
                    $"Ca đêm cộng thêm {Amount(4, SalaryConstants.NIGHT_SHIFT_ALLOWANCE_RATE):P0} trên đơn giá giờ bình thường, không nhân chồng với hệ số tăng ca.",
                    "Giờ OT lấy từ chấm công đã duyệt; sửa cấu hình không làm thay đổi phiếu lương đã khóa sổ."
                ]
            },
            CauHinhLuongCategory.PhuCap => new CauHinhLuongGuide
            {
                Formula = "Phụ cấp = (Định mức ngày × Ngày công thực tế) + (Định mức tháng × Ngày công thực tế ÷ 26)",
                FormulaNote = "Khoản cố định tháng được phân bổ theo tỷ lệ ngày công thực tế, tối đa bằng 100% định mức.",
                Parameters =
                [
                    standardDays,
                    new()
                    {
                        Label = "Phụ cấp ăn ca",
                        Value = Amount(1, SalaryConstants.MEAL_ALLOWANCE_PER_DAY).ToString("N0") + " đ",
                        Unit = "/ngày công",
                        Hint = "Miễn thuế TNCN, tính trên ngày công thực tế có mặt"
                    },
                    new()
                    {
                        Label = "Phụ cấp trách nhiệm",
                        Value = Amount(2, 1000000m).ToString("N0") + " đ",
                        Unit = "/tháng",
                        Hint = "Chịu thuế TNCN, phân bổ theo ngày công thực tế"
                    },
                    new()
                    {
                        Label = "Phụ cấp độc hại buồng phòng",
                        Value = Amount(4, 20000m).ToString("N0") + " đ",
                        Unit = "/ngày công",
                        Hint = "Miễn thuế TNCN, chỉ áp dụng cho bộ phận buồng phòng"
                    }
                ],
                Rules =
                [
                    $"Phụ cấp ăn ca {Amount(1, SalaryConstants.MEAL_ALLOWANCE_PER_DAY):N0} đ/ngày công và phụ cấp độc hại buồng phòng là khoản không chịu thuế TNCN.",
                    "Phụ cấp trách nhiệm quản lý chịu thuế TNCN và được phân bổ theo tỷ lệ ngày công thực tế / 26.",
                    "Cờ \"Chịu thuế TNCN\" quyết định khoản phụ cấp có nằm trong thu nhập chịu thuế hay không.",
                    "Chính sách khách sạn không áp dụng phụ cấp điện thoại và tạm ứng lương."
                ]
            },
            CauHinhLuongCategory.KhauTru => new CauHinhLuongGuide
            {
                Formula = "Khấu trừ = Lương cơ bản × Tổng tỷ lệ bảo hiểm + Thuế TNCN + Phạt vi phạm + Khấu trừ khác",
                FormulaNote = "Bảo hiểm tính trên lương cơ bản và áp trần theo luật; thuế TNCN tính lũy tiến sau giảm trừ gia cảnh.",
                Parameters =
                [
                    new()
                    {
                        Label = "BHXH",
                        Value = Amount(1, SalaryConstants.BHXH_RATE * 100m).ToString("N2") + "%",
                        Unit = "lương cơ bản",
                        Hint = "Trần đóng BHXH, BHYT: " + SalaryConstants.MAX_INSURANCE_SALARY_BASE.ToString("N0") + " đ"
                    },
                    new()
                    {
                        Label = "BHYT",
                        Value = Amount(2, SalaryConstants.BHYT_RATE * 100m).ToString("N2") + "%",
                        Unit = "lương cơ bản",
                        Hint = "Trích cùng trần với BHXH"
                    },
                    new()
                    {
                        Label = "BHTN",
                        Value = Amount(3, SalaryConstants.BHTN_RATE * 100m).ToString("N2") + "%",
                        Unit = "lương cơ bản",
                        Hint = "Trần đóng BHTN: " + SalaryConstants.MAX_UNEMPLOYMENT_INSURANCE_SALARY_BASE.ToString("N0") + " đ"
                    },
                    new()
                    {
                        Label = "Phạt vi phạm",
                        Value = Amount(5, SalaryConstants.FINE_PER_MINUTE_LATE).ToString("N0") + " đ",
                        Unit = "/phút",
                        Hint = "Đi trễ, về sớm lấy từ dữ liệu chấm công"
                    },
                    new()
                    {
                        Label = "Giảm trừ bản thân",
                        Value = SalaryConstants.PERSONAL_TAX_DEDUCTION.ToString("N0") + " đ",
                        Unit = "/tháng",
                        Hint = "Áp dụng trước khi tính thuế TNCN lũy tiến"
                    },
                    new()
                    {
                        Label = "Giảm trừ người phụ thuộc",
                        Value = SalaryConstants.DEPENDENT_TAX_DEDUCTION.ToString("N0") + " đ",
                        Unit = "/người/tháng",
                        Hint = "Nhân với số người phụ thuộc đã khai báo"
                    }
                ],
                Rules =
                [
                    "Tổng tỷ lệ bảo hiểm bắt buộc người lao động đóng là 10,5% (BHXH 8% + BHYT 1,5% + BHTN 1%).",
                    "Hợp đồng thử việc hoặc tháng có dưới 14 ngày công không trích bảo hiểm bắt buộc.",
                    "Thuế TNCN tính lũy tiến từng phần; khoản phụ cấp ăn ca miễn thuế không vào thu nhập chịu thuế.",
                    "Khách sạn không áp dụng tạm ứng lương; chỉ khấu trừ bảo hiểm, thuế, phạt và khoản HR điều chỉnh."
                ]
            },
            CauHinhLuongCategory.Thuong => new CauHinhLuongGuide
            {
                Formula = "Thưởng = Chuyên cần + Phí phục vụ + (Lương tháng ÷ 26 ÷ 8 × Giờ sự kiện × " + Amount(SalaryConstants.EVENT_HOURLY_RATE_CONFIG_ID, SalaryConstants.EVENT_HOURLY_RATE).ToString("0.00") + ")",
                FormulaNote = "Toàn bộ khoản thưởng cộng vào Tổng thu nhập (Gross) và chịu thuế TNCN 100%.",
                Parameters =
                [
                    new()
                    {
                        Label = "Thưởng chuyên cần",
                        Value = Amount(4, 500000m).ToString("N0") + " đ",
                        Unit = "/tháng",
                        Hint = "Đạt tối thiểu 25 ngày công, không đi trễ / về sớm / bỏ ca"
                    },
                    new()
                    {
                        Label = "Phí phục vụ (Service Charge)",
                        Value = Amount(5, 1000000m).ToString("N0") + " đ",
                        Unit = "/tháng",
                        Hint = "Phân bổ theo ngày công thực tế / 26"
                    },
                    new()
                    {
                        Label = "Hệ số thưởng sự kiện theo giờ",
                        Value = Amount(SalaryConstants.EVENT_HOURLY_RATE_CONFIG_ID, SalaryConstants.EVENT_HOURLY_RATE).ToString("N2") + "×",
                        Unit = "hệ số",
                        Hint = "Sửa trực tiếp tại dòng #" + SalaryConstants.EVENT_HOURLY_RATE_CONFIG_ID + " trong bảng bên dưới"
                    },
                    new()
                    {
                        Label = "Giờ tham gia sự kiện",
                        Value = "Lương tháng ÷ 26 ÷ 8 × " + Amount(SalaryConstants.EVENT_HOURLY_RATE_CONFIG_ID, SalaryConstants.EVENT_HOURLY_RATE).ToString("0.00"),
                        Unit = "đ/giờ",
                        Hint = "Nhân với số giờ sự kiện thực tế đã duyệt; lương ca vẫn tính riêng theo chấm công"
                    }
                ],
                Rules =
                [
                    $"Thưởng chuyên cần {Amount(4, 500000m):N0} đ chỉ chi khi đạt ≥ 25 ngày công thực tế và không có vi phạm giờ giấc.",
                    $"Phí phục vụ {Amount(5, 1000000m):N0} đ được phân bổ theo tỷ lệ ngày công thực tế / 26, tối đa bằng định mức tháng.",
                    $"Tiền sự kiện = Lương tháng ÷ 26 ÷ 8 × Số giờ tham gia × {Amount(SalaryConstants.EVENT_HOURLY_RATE_CONFIG_ID, SalaryConstants.EVENT_HOURLY_RATE).ToString("0.00")}; hệ số đọc từ dòng tham số #{SalaryConstants.EVENT_HOURLY_RATE_CONFIG_ID}.",
                    "Chỉ chi khi phân công đã nghiệm thu (đánh giá HOÀN THÀNH) và chưa kết chuyển kỳ trước; khoản thưởng chịu thuế TNCN.",
                    "Phiếu lương đã khóa sổ giữ nguyên số tiền snapshot; mọi thay đổi chỉ áp dụng cho kỳ chưa khóa."
                ]
            },
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
    }

    [HttpGet]
    public async Task<IActionResult> Index(CauHinhLuongCategory category = CauHinhLuongCategory.TangCa,
        string? keyword = null, string? type = null)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(category)) return BadRequest();
        keyword = keyword?.Trim();
        type = type?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(type) && !CauHinhLuongLabels.Types(category).ContainsKey(type))
            return BadRequest();
        var model = new CauHinhLuongListViewModel { Category = category, Keyword = keyword, Type = type };
        try
        {
            var query = (await this.SearchDataAsync($"payroll-config:{category}",
                () => Query(category).ToListAsync())).AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var term = keyword.ToLowerInvariant();
                query = query.Where(x => x.Name.ToLower().Contains(term) ||
                    (x.Description != null && x.Description.ToLower().Contains(term)));
            }
            if (!string.IsNullOrEmpty(type)) query = query.Where(x => x.Type == type);
            model.Items = query.OrderBy(x => x.Name).ToList();
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Không thể đọc danh mục cấu hình lương {Category}.", category);
            model.Error = "Không thể tải dữ liệu. Vui lòng kiểm tra kết nối và thử lại.";
        }
        model.Guide = BuildGuide(category, model.Items);
        try
        {
            model.SalaryBands = await _context.KhungLuongCapBacs.AsNoTracking()
                .OrderBy(x => x.NhomChucVu).ThenBy(x => x.TrinhDo).ThenBy(x => x.ThamNienTuThang)
                .ToListAsync();
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Không thể đọc khung lương cấp bậc.");
            model.SalaryBandsError = "Không thể tải khung lương. Vui lòng tải lại trang để thử lại.";
        }
        return View(model);
    }

    [HttpGet]
    public IActionResult Create(CauHinhLuongCategory category = CauHinhLuongCategory.TangCa)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(category)) return BadRequest();
        return View("Form", new CauHinhLuongFormViewModel
        {
            Category = category,
            Value = category == CauHinhLuongCategory.TangCa ? 1.50m : 0,
            Type = CauHinhLuongLabels.Types(category).Keys.FirstOrDefault()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CauHinhLuongCategory category)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(category)) return BadRequest();
        var row = await Query(category).FirstOrDefaultAsync(x => x.Id == id);
        if (row == null) return NotFound();
        return View("Form", new CauHinhLuongFormViewModel
        {
            Id = row.Id, Category = category, Name = row.Name, Type = row.Type,
            Value = row.Value, Taxable = row.Taxable, Description = row.Description
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CauHinhLuongFormViewModel model)
    {
        model.Id = 0;
        return await Save(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CauHinhLuongFormViewModel model)
    {
        if (id <= 0 || id != model.Id) return BadRequest();
        return await Save(model);
    }

    private async Task<IActionResult> Save(CauHinhLuongFormViewModel model)
    {
        if (!Enum.IsDefined(model.Category)) return BadRequest();
        model.Name = model.Name?.Trim() ?? "";
        model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        if (model.Category == CauHinhLuongCategory.Thuong
            && model.Id == SalaryConstants.EVENT_HOURLY_RATE_CONFIG_ID
            && (model.Value < 0.01m || model.Value > 99.99m))
            ModelState.AddModelError(nameof(model.Value), "Hệ số thưởng sự kiện phải trong khoảng 0,01 – 99,99.");
        if (!ModelState.IsValid) return View("Form", model);
        try
        {
            var name = model.Name.ToLowerInvariant();
            if (await Query(model.Category).AnyAsync(x => x.Id != model.Id && x.Name.ToLower() == name) ||
                (model.Category == CauHinhLuongCategory.Thuong &&
                 await _context.CauHinhKhoanLuongs.AnyAsync(x => x.MaKhoan != model.Id && x.TenKhoan.ToLower() == name)))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên cấu hình đã tồn tại trong danh mục.");
                return View("Form", model);
            }
            if (!await SetEntity(model)) return NotFound();
            await _context.SaveChangesAsync();
            cache.Remove("HomeDashboardMetrics_V2");
            TempData["Success"] = "Đã lưu cấu hình. Phiếu lương đã chốt giữ nguyên dữ liệu.";
            return RedirectToAction(nameof(Index), new { category = model.Category });
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException)
        {
            logger.LogError(ex, "Không thể lưu cấu hình lương {Category}/{Id}.", model.Category, model.Id);
            ModelState.AddModelError(string.Empty, "Không thể lưu cấu hình. Kiểm tra tên trùng, dữ liệu và kết nối rồi thử lại.");
            return View("Form", model);
        }
    }

    private async Task<bool> SetEntity(CauHinhLuongFormViewModel model)
    {
        switch (model.Category)
        {
            case CauHinhLuongCategory.TangCa:
            {
                var e = model.Id == 0 ? new CauHinhTangCa() : await _context.CauHinhTangCas.FindAsync(model.Id);
                if (e == null) return false;
                e.TenLoaiOt = model.Name;
                e.HeSo = model.Value;
                e.MoTa = model.Description;

                if (model.Id == 0) _context.CauHinhTangCas.Add(e);
                break;
            }
            case CauHinhLuongCategory.PhuCap:
            {
                var e = model.Id == 0 ? new CauHinhPhuCap() : await _context.CauHinhPhuCaps.FindAsync(model.Id);
                if (e == null) return false;
                e.TenPhuCap = model.Name;
                e.MucDinhMuc = model.Value;
                e.MoTa = model.Description;
                e.LoaiTinh = model.Type!; e.ChiuThueTncn = model.Taxable;
                if (model.Id == 0) _context.CauHinhPhuCaps.Add(e);
                break;
            }
            case CauHinhLuongCategory.KhauTru:
            {
                var e = model.Id == 0 ? new CauHinhKhauTru() : await _context.CauHinhKhauTrus.FindAsync(model.Id);
                if (e == null) return false;
                e.TenKhoanTru = model.Name;
                e.TyLeHoacDinhMuc = model.Value;
                e.MoTa = model.Description;
                e.LoaiKhoanTru = model.Type!;
                if (model.Id == 0) _context.CauHinhKhauTrus.Add(e);
                break;
            }
            case CauHinhLuongCategory.Thuong:
            {
                var e = model.Id == 0 ? new CauHinhKhoanLuong() : await _context.CauHinhKhoanLuongs.FindAsync(model.Id);
                if (e == null || (model.Id != 0 && e.LoaiKhoan != "THUONG")) return false;
                // Dòng tham số hệ số sự kiện giữ tên chuẩn để công thức và báo cáo luôn nhận diện được.
                e.TenKhoan = model.Id == SalaryConstants.EVENT_HOURLY_RATE_CONFIG_ID ? EventRateConfig.ConfigName : model.Name;
                e.GiaTriMacDinh = model.Value;
                e.MoTa = model.Description;
                e.LoaiKhoan = "THUONG";
                if (model.Id == 0) _context.CauHinhKhoanLuongs.Add(e);
                break;
            }
            default: return false;
        }
        return true;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CauHinhLuongCategory category)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(category)) return BadRequest();
        try
        {
            var row = await Query(category).FirstOrDefaultAsync(x => x.Id == id);
            if (row == null) return NotFound();
            if (row.IsParameter)
            {
                TempData["Error"] = "Đây là tham số hệ số thưởng sự kiện — chỉ được sửa giá trị, không thể xóa.";
                return RedirectToAction(nameof(Index), new { category });
            }
            if (row.Used)
            {
                TempData["Error"] = "Không thể xóa cấu hình đã được sử dụng trong phiếu lương.";
                return RedirectToAction(nameof(Index), new { category });
            }
            object? entity = category switch
            {
                CauHinhLuongCategory.TangCa => await _context.CauHinhTangCas.FindAsync(id),
                CauHinhLuongCategory.PhuCap => await _context.CauHinhPhuCaps.FindAsync(id),
                CauHinhLuongCategory.KhauTru => await _context.CauHinhKhauTrus.FindAsync(id),
                CauHinhLuongCategory.Thuong => await _context.CauHinhKhoanLuongs.FindAsync(id),
                _ => null
            };
            if (entity == null) return NotFound();
            _context.Remove(entity);
            await _context.SaveChangesAsync();
            cache.Remove("HomeDashboardMetrics_V2");
            TempData["Success"] = "Đã xóa cấu hình.";
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException)
        {
            logger.LogError(ex, "Không thể xóa cấu hình lương {Category}/{Id}.", category, id);
            TempData["Error"] = "Không thể xóa cấu hình. Dữ liệu có thể đang được sử dụng hoặc kết nối bị gián đoạn.";
        }
        return RedirectToAction(nameof(Index), new { category });
    }
}
