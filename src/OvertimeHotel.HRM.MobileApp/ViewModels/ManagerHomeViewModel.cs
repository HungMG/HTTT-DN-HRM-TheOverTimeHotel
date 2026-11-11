using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel trang chủ nhánh QUẢN LÝ BỘ PHẬN (Executive Manager Dashboard).
/// Thiết kế chuẩn Hospitality 5 sao: Bảng điều hành bộ phận Navy &amp; Gold,
/// cảnh báo duyệt đơn khẩn cấp, giám sát quân số ca trực hôm nay (ai đang làm việc,
/// ai đi trễ, ai nghỉ phép), và phím tắt nghiệp vụ điều hành nhanh.
/// </summary>
public class ManagerHomeViewModel : HomeViewModelBase
{
    private readonly IAttendanceService _attendanceService;
    private readonly SupabaseRestClient _restClient;

    private string _shiftTimeText = "--:-- – --:--";
    private string _shiftTaskText = "Đang tải ca trực...";
    private string _nightBadgeText = string.Empty;
    private bool _hasShiftToday;
    private int _reviewedLeaveCount;
    private bool _isRefreshing;

    // Chỉ số giám sát quân số bộ phận trong ngày
    private string _departmentCode = "MGR";
    private string _departmentStaffCountText = "5 nhân sự trực thuộc";
    private string _workingStaffCountText = "4/5";
    private string _workingStatusSubText = "đang trong ca";
    private string _lateStaffCountText = "0";
    private string _lateStatusSubText = "đi trễ hôm nay";
    private string _onLeaveStaffCountText = "1";
    private string _onLeaveStatusSubText = "nghỉ phép hôm nay";

    // Danh sách và tra cứu nhân sự trực thuộc bộ phận
    private string _searchKeyword = string.Empty;
    private string _staffFilterResultText = "Danh sách nhân sự trực thuộc bộ phận hôm nay";
    private bool _hasStaffResults = true;

    private readonly List<HrStaffLookupItem> _allDepartmentStaff = new();

    public ObservableCollection<HrStaffLookupItem> FilteredDepartmentStaff { get; } = new();

    public ICommand ApproveCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SearchStaffCommand { get; }
    public ICommand ContactStaffCommand { get; }

    public event EventHandler<string>? ShowNotificationDialog;

    public ManagerHomeViewModel(
        IAuthService authService,
        ILeaveApprovalService leaveApprovalService,
        IAttendanceService attendanceService,
        SupabaseRestClient restClient)
        : base(authService, leaveApprovalService)
    {
        _attendanceService = attendanceService ?? throw new ArgumentNullException(nameof(attendanceService));
        _restClient = restClient ?? throw new ArgumentNullException(nameof(restClient));

        ApproveCommand = new Command(() => _ = Shell.Current.GoToAsync("LeaveApprovalPage"));

        RefreshCommand = new Command(async () =>
        {
            IsRefreshing = true;
            await LoadAsync();
            IsRefreshing = false;
        });

        SearchStaffCommand = new Command(() => FilterStaff());

        ContactStaffCommand = new Command<HrStaffLookupItem>(staff =>
        {
            if (staff is null) return;
            ShowNotificationDialog?.Invoke(this,
                $"THÔNG TIN NHÂN VIÊN:\n• {staff.FullName} ({staff.StaffCode})\n• Vị trí: {staff.Position}\n• Trạng thái: {staff.WorkStatusDetail}\n• Máy lẻ nội bộ: {staff.InternalPhone}");
        });

        RefreshManagerScope();
    }

    /// <summary>Nhãn vai trò hiển thị trên thẻ phạm vi quản lý.</summary>
    public string CommandRole { get; private set; } = "Quản lý bộ phận";

    /// <summary>Phạm vi duyệt đơn: phòng ban của quản lý, hoặc toàn khách sạn với HR/Quản trị viên.</summary>
    public string CommandScope { get; private set; } = "Bộ phận Tiền sảnh";

    /// <summary>Mã viết tắt của bộ phận (FO, HK, FB, KIT, ENG, SEC).</summary>
    public string DepartmentCode
    {
        get => _departmentCode;
        private set => SetProperty(ref _departmentCode, value);
    }

    /// <summary>Tổng số nhân viên trong bộ phận.</summary>
    public string DepartmentStaffCountText
    {
        get => _departmentStaffCountText;
        private set => SetProperty(ref _departmentStaffCountText, value);
    }

    public string WorkingStaffCountText
    {
        get => _workingStaffCountText;
        private set => SetProperty(ref _workingStaffCountText, value);
    }

    public string WorkingStatusSubText
    {
        get => _workingStatusSubText;
        private set => SetProperty(ref _workingStatusSubText, value);
    }

    public string LateStaffCountText
    {
        get => _lateStaffCountText;
        private set => SetProperty(ref _lateStaffCountText, value);
    }

    public string LateStatusSubText
    {
        get => _lateStatusSubText;
        private set => SetProperty(ref _lateStatusSubText, value);
    }

    public string OnLeaveStaffCountText
    {
        get => _onLeaveStaffCountText;
        private set => SetProperty(ref _onLeaveStaffCountText, value);
    }

    public string OnLeaveStatusSubText
    {
        get => _onLeaveStatusSubText;
        private set => SetProperty(ref _onLeaveStatusSubText, value);
    }

    public string SearchKeyword
    {
        get => _searchKeyword;
        set
        {
            if (SetProperty(ref _searchKeyword, value))
            {
                FilterStaff();
            }
        }
    }

    public string StaffFilterResultText
    {
        get => _staffFilterResultText;
        private set => SetProperty(ref _staffFilterResultText, value);
    }

    public bool HasStaffResults
    {
        get => _hasStaffResults;
        private set => SetProperty(ref _hasStaffResults, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    /// <summary>Người dùng hiện tại có thẩm quyền phê duyệt đơn hay không.</summary>
    public bool CanAssignShifts => AuthService.CurrentUser?.HasPermission(OvertimeHotel.HRM.Core.Policies.MobilePermissionPolicy.AssignShifts) == true;
    public bool CanViewDepartmentAttendance => AuthService.CurrentUser?.HasPermission(OvertimeHotel.HRM.Core.Policies.MobilePermissionPolicy.DepartmentAttendance) == true;
    public bool CanApproveLeave => AuthService.CurrentUser?.CanApproveLeave == true;

    /// <summary>
    /// Quyền hạn hiển thị dưới dòng phạm vi trên thẻ navy.
    /// </summary>
    public string PermissionLabel => CanApproveLeave ? "Duyệt Cấp 1" : "Chỉ Xem";

    /// <summary>Khung giờ ca trực hoặc điều hành hôm nay.</summary>
    public string ShiftTimeText
    {
        get => _shiftTimeText;
        private set => SetProperty(ref _shiftTimeText, value);
    }

    /// <summary>Tên nhiệm vụ / ca điều hành hôm nay.</summary>
    public string ShiftTaskText
    {
        get => _shiftTaskText;
        private set => SetProperty(ref _shiftTaskText, value);
    }

    /// <summary>Huy hiệu ca trực hoặc quản lý trực ca.</summary>
    public string NightBadgeText
    {
        get => _nightBadgeText;
        private set => SetProperty(ref _nightBadgeText, value);
    }

    /// <summary>Hôm nay có ca trực trong phân ca hay không.</summary>
    public bool HasShiftToday
    {
        get => _hasShiftToday;
        private set => SetProperty(ref _hasShiftToday, value);
    }

    /// <summary>Số đơn đã xử lý. Được nạp cùng lúc với đơn chờ duyệt.</summary>
    public int ReviewedLeaveCount
    {
        get => _reviewedLeaveCount;
        private set => SetProperty(ref _reviewedLeaveCount, value);
    }

    /// <summary>
    /// Nạp lại vai trò, phạm vi quản lý và danh sách nhân sự sau mỗi lần trang hiện ra.
    /// </summary>
    public override async Task LoadAsync()
    {
        RefreshManagerScope();
        await LoadDepartmentStaffDirectoryAsync();
        await base.LoadAsync();
    }

    /// <summary>
    /// Nhánh Quản lý nạp thêm ca trực hôm nay và số đơn đã xử lý.
    /// </summary>
    protected override async Task LoadBranchKpiAsync(UserSession user)
    {
        await LoadTodayShiftAsync();

        if (!user.CanApproveLeave)
        {
            ReviewedLeaveCount = 0;
            return;
        }

        try
        {
            var reviewed = await LeaveApprovalService.GetReviewedRequestsAsync(user.TenVaiTro, user.MaPhongBan);
            ReviewedLeaveCount = reviewed.Count;
        }
        catch
        {
            ReviewedLeaveCount = 0;
        }
    }

    private async Task LoadTodayShiftAsync()
    {
        try
        {
            var shift = await _attendanceService.GetTodayShiftAsync();
            HasShiftToday = shift.MaPhanCa > 0;

            if (!HasShiftToday)
            {
                ShiftTimeText = "08:00 – 17:00";
                ShiftTaskText = "Ca điều hành tiêu chuẩn · Quản lý vận hành khách sạn";
                NightBadgeText = "Trưởng Bộ Phận";
                return;
            }

            ShiftTimeText = $"{FormatTime(shift.GioBatDau)} – {FormatTime(shift.GioKetThuc)}";
            ShiftTaskText = string.IsNullOrWhiteSpace(shift.TenSuKien)
                ? (string.IsNullOrWhiteSpace(shift.TenCa) ? "Ca điều hành & quản lý" : shift.TenCa)
                : $"{shift.TenCa} • {shift.TenSuKien}";
            NightBadgeText = IsNightShift(shift.GioBatDau, shift.GioKetThuc)
                ? "Ca Đêm · +30%"
                : "Quản Lý Trực Ca";
        }
        catch
        {
            HasShiftToday = false;
            ShiftTimeText = "08:00 – 17:00";
            ShiftTaskText = "Ca điều hành tiêu chuẩn · Quản lý vận hành khách sạn";
            NightBadgeText = "Trưởng Bộ Phận";
        }
    }

    private static bool IsNightShift(DateTimeOffset start, DateTimeOffset end) =>
        end.TimeOfDay <= start.TimeOfDay;

    private static string FormatTime(DateTimeOffset value) =>
        value == default ? "--:--" : value.ToLocalTime().ToString("HH:mm");

    private void RefreshManagerScope()
    {
        var user = AuthService.CurrentUser;
        if (user is null)
        {
            return;
        }

        CommandRole = user.IsAdmin
            ? "Quản trị viên"
            : user.IsHR
                ? "Nhân sự (HR)"
                : "Quản lý bộ phận";

        CommandScope = user.IsManager && !user.IsHR && !user.IsAdmin
            ? (string.IsNullOrWhiteSpace(user.TenPhongBan) ? "Bộ phận Tiền sảnh" : user.TenPhongBan)
            : "Toàn khách sạn";

        DepartmentCode = ResolveDepartmentCode(CommandScope);

        OnPropertyChanged(nameof(CommandRole));
        OnPropertyChanged(nameof(CommandScope));
        OnPropertyChanged(nameof(DepartmentCode));
        OnPropertyChanged(nameof(PermissionLabel));
        OnPropertyChanged(nameof(CanApproveLeave));
    }

    private static string ResolveDepartmentCode(string deptName)
    {
        var upper = deptName.ToUpperInvariant();
        if (upper.Contains("TIỀN SẢNH") || upper.Contains("FRONT OFFICE") || upper.Contains("FO")) return "FO";
        if (upper.Contains("BUỒNG PHÒNG") || upper.Contains("HOUSEKEEPING") || upper.Contains("HK")) return "HK";
        if (upper.Contains("ẨM THỰC") || upper.Contains("F&B") || upper.Contains("FOOD")) return "FB";
        if (upper.Contains("BẾP") || upper.Contains("KITCHEN") || upper.Contains("CULINARY")) return "KIT";
        if (upper.Contains("KỸ THUẬT") || upper.Contains("ENGINEERING") || upper.Contains("ENG")) return "ENG";
        if (upper.Contains("AN NINH") || upper.Contains("SECURITY") || upper.Contains("SEC")) return "SEC";
        if (upper.Contains("NHÂN SỰ") || upper.Contains("HR")) return "HR";
        return "MGR";
    }

    /// <summary>
    /// Nạp danh sách nhân sự thực tế của bộ phận từ CSDL Supabase và tính toán chỉ số quân số trực ca hôm nay.
    /// </summary>
    private async Task LoadDepartmentStaffDirectoryAsync()
    {
        _allDepartmentStaff.Clear();

        var user = AuthService.CurrentUser;
        if (user is null)
        {
            return;
        }

        try
        {
            var deptCode = ResolveDepartmentCode(CommandScope);
            var today = DateOnly.FromDateTime(DateTime.Now);
            var todayStr = today.ToString("yyyy-MM-dd");

            // 1. Lấy danh sách nhân viên thực tế trực thuộc bộ phận từ CSDL Supabase
            var staffFilter = user.MaPhongBan.HasValue && user.MaPhongBan.Value > 0
                ? $"&ma_phong_ban=eq.{user.MaPhongBan.Value}"
                : string.Empty;

            var staffJson = await _restClient.GetAsync(
                $"nhan_vien?select=ma_nhan_vien,ho,ten,dien_thoai,ma_phong_ban,chuc_vu(ten_chuc_vu),phong_ban(ten_phong_ban)" +
                staffFilter +
                "&trang_thai=neq.THOI_VIEC&order=ma_nhan_vien.asc");

            var staffRows = PostgrstJson.ParseArray(staffJson);

            // 2. Lấy phân ca và chấm công thực tế hôm nay
            Dictionary<int, JsonElement> shiftsByStaff = new();
            try
            {
                var shiftJson = await _restClient.GetAsync(
                    $"phan_ca?select=ma_phan_ca,ma_nhan_vien,ma_ca,ngay_lam_viec,bat_dau_du_kien,ket_thuc_du_kien," +
                    "ca_lam_viec(ten_ca,gio_bat_dau,gio_ket_thuc)," +
                    "cham_cong(ma_cham_cong,gio_check_in,gio_check_out,phut_di_tre,trang_thai)" +
                    $"&ngay_lam_viec=eq.{todayStr}");

                foreach (var shift in PostgrstJson.ParseArray(shiftJson))
                {
                    var nvId = PostgrstJson.ReadNullableInt(shift, "ma_nhan_vien");
                    if (nvId.HasValue && !shiftsByStaff.ContainsKey(nvId.Value))
                    {
                        shiftsByStaff[nvId.Value] = shift;
                    }
                }
            }
            catch
            {
                // Bỏ qua nếu chưa có phân ca ngày hôm nay
            }

            // 3. Lấy đơn nghỉ phép đã duyệt cho hôm nay
            HashSet<int> staffOnLeave = new();
            try
            {
                var leaveJson = await _restClient.GetAsync(
                    $"don_tu?select=ma_don,ma_nhan_vien,trang_thai" +
                    $"&trang_thai=eq.DA_DUYET&tu_ngay=lte.{todayStr}&den_ngay=gte.{todayStr}");

                foreach (var leave in PostgrstJson.ParseArray(leaveJson))
                {
                    var nvId = PostgrstJson.ReadNullableInt(leave, "ma_nhan_vien");
                    if (nvId.HasValue)
                    {
                        staffOnLeave.Add(nvId.Value);
                    }
                }
            }
            catch
            {
                // Bỏ qua nếu bảng đơn từ không truy vấn được
            }

            var now = DateTimeOffset.Now;

            foreach (var row in staffRows)
            {
                var id = PostgrstJson.ReadNullableInt(row, "ma_nhan_vien") ?? 0;
                if (id == 0) continue;

                // Nếu là Quản lý bộ phận thì chỉ giám sát cấp dưới, không liệt kê chính mình
                if (user.IsManager && !user.IsAdmin && !user.IsHR && id == user.MaNhanVien)
                {
                    continue;
                }

                var ho = PostgrstJson.ReadString(row, "ho");
                var ten = PostgrstJson.ReadString(row, "ten");
                var fullName = string.Join(" ", new[] { ho, ten }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = $"Nhân viên #{id}";
                }

                var phone = PostgrstJson.ReadString(row, "dien_thoai");
                var deptName = PostgrstJson.ReadEmbeddedString(row, "phong_ban", "ten_phong_ban");
                if (string.IsNullOrWhiteSpace(deptName)) deptName = CommandScope;

                var posName = PostgrstJson.ReadEmbeddedString(row, "chuc_vu", "ten_chuc_vu");
                if (string.IsNullOrWhiteSpace(posName)) posName = "Nhân viên";

                var staffCode = $"{deptCode}-{id:D3}";
                var internalPhone = !string.IsNullOrWhiteSpace(phone) ? phone : $"Ext {100 + (id % 900)}";

                string workStatus;
                string workStatusDetail;
                string statusColor;
                string statusBgColor;

                if (staffOnLeave.Contains(id))
                {
                    workStatus = "Nghỉ phép";
                    workStatusDetail = "Nghỉ phép năm · Đã duyệt";
                    statusColor = "#B45309";
                    statusBgColor = "#FEF3C7";
                }
                else if (shiftsByStaff.TryGetValue(id, out var shift))
                {
                    var tenCa = PostgrstJson.ReadEmbeddedString(shift, "ca_lam_viec", "ten_ca");
                    if (string.IsNullOrWhiteSpace(tenCa)) tenCa = "Ca làm việc";

                    var startExpected = PostgrstJson.ReadNullableTimestamp(shift, "bat_dau_du_kien") ?? DateTimeOffset.MinValue;
                    var endExpected = PostgrstJson.ReadNullableTimestamp(shift, "ket_thuc_du_kien") ?? DateTimeOffset.MinValue;

                    // Đọc thông tin chấm công (nếu có)
                    var chamCong = PostgrstJson.ReadEmbedded(shift, "cham_cong");
                    var checkInTime = chamCong.HasValue ? PostgrstJson.ReadNullableTimestamp(chamCong.Value, "gio_check_in") : null;
                    var checkOutTime = chamCong.HasValue ? PostgrstJson.ReadNullableTimestamp(chamCong.Value, "gio_check_out") : null;
                    var phutDiTre = chamCong.HasValue ? (PostgrstJson.ReadNullableInt(chamCong.Value, "phut_di_tre") ?? 0) : 0;
                    var trangThaiChamCong = chamCong.HasValue ? PostgrstJson.ReadString(chamCong.Value, "trang_thai") : string.Empty;

                    if (checkInTime.HasValue)
                    {
                        if (checkOutTime.HasValue)
                        {
                            workStatus = "Hoàn thành ca";
                            workStatusDetail = $"{tenCa} · Đã ra ca {checkOutTime.Value.ToLocalTime():HH:mm}";
                            statusColor = "#0284C7";
                            statusBgColor = "#E0F2FE";
                        }
                        else if (phutDiTre > 0 || trangThaiChamCong == "DI_TRE")
                        {
                            workStatus = "Đi trễ";
                            workStatusDetail = $"{tenCa} · Chấm công {checkInTime.Value.ToLocalTime():HH:mm} (+{phutDiTre}p)";
                            statusColor = "#DC2626";
                            statusBgColor = "#FEE2E2";
                        }
                        else
                        {
                            workStatus = "Đang trong ca";
                            workStatusDetail = $"{tenCa} · Đã chấm công {checkInTime.Value.ToLocalTime():HH:mm}";
                            statusColor = "#15803D";
                            statusBgColor = "#DCFCE7";
                        }
                    }
                    else
                    {
                        if (startExpected != DateTimeOffset.MinValue && now > startExpected.AddMinutes(15))
                        {
                            workStatus = "Chưa vào ca";
                            workStatusDetail = $"{tenCa} · Chưa chấm công vào ca";
                            statusColor = "#D97706";
                            statusBgColor = "#FEF3C7";
                        }
                        else
                        {
                            var timeStr = startExpected != DateTimeOffset.MinValue ? startExpected.ToLocalTime().ToString("HH:mm") : "--:--";
                            workStatus = "Sắp vào ca";
                            workStatusDetail = $"{tenCa} · Dự kiến {timeStr}";
                            statusColor = "#475569";
                            statusBgColor = "#F1F5F9";
                        }
                    }
                }
                else
                {
                    workStatus = "Nghỉ ca";
                    workStatusDetail = "Hôm nay không có ca trực";
                    statusColor = "#64748B";
                    statusBgColor = "#F8FAFC";
                }

                _allDepartmentStaff.Add(new HrStaffLookupItem
                {
                    StaffCode = staffCode,
                    FullName = fullName,
                    Department = deptName,
                    Position = posName,
                    WorkStatus = workStatus,
                    WorkStatusDetail = workStatusDetail,
                    StatusColor = statusColor,
                    StatusBgColor = statusBgColor,
                    InternalPhone = internalPhone
                });
            }
        }
        catch
        {
            // Dự phòng an toàn nếu mất mạng
        }

        // Cập nhật số liệu thống kê quân số bộ phận
        int total = _allDepartmentStaff.Count;
        int working = _allDepartmentStaff.Count(s => s.WorkStatus == "Đang trong ca");
        int late = _allDepartmentStaff.Count(s => s.WorkStatus == "Đi trễ" || s.WorkStatus == "Chưa vào ca");
        int onLeave = _allDepartmentStaff.Count(s => s.WorkStatus.Contains("Nghỉ"));

        DepartmentStaffCountText = $"{total} nhân sự trực thuộc";
        WorkingStaffCountText = $"{working}/{total}";
        WorkingStatusSubText = "đang trong ca";
        LateStaffCountText = late.ToString();
        LateStatusSubText = late > 0 ? "cần đôn đốc" : "không vi phạm";
        OnLeaveStaffCountText = onLeave.ToString();
        OnLeaveStatusSubText = onLeave > 0 ? "nghỉ có phép" : "đủ quân số";

        OnPropertyChanged(nameof(DepartmentStaffCountText));
        OnPropertyChanged(nameof(WorkingStaffCountText));
        OnPropertyChanged(nameof(WorkingStatusSubText));
        OnPropertyChanged(nameof(LateStaffCountText));
        OnPropertyChanged(nameof(LateStatusSubText));
        OnPropertyChanged(nameof(OnLeaveStaffCountText));
        OnPropertyChanged(nameof(OnLeaveStatusSubText));

        FilterStaff();
    }


    private void FilterStaff()
    {
        FilteredDepartmentStaff.Clear();

        var query = _allDepartmentStaff.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            var kw = SearchKeyword.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.FullName.ToLowerInvariant().Contains(kw) ||
                s.StaffCode.ToLowerInvariant().Contains(kw) ||
                s.Position.ToLowerInvariant().Contains(kw));
        }

        var results = query.ToList();
        foreach (var item in results)
        {
            FilteredDepartmentStaff.Add(item);
        }

        HasStaffResults = results.Count > 0;
        StaffFilterResultText = string.IsNullOrWhiteSpace(SearchKeyword)
            ? $"Hiển thị {results.Count} nhân sự · {CommandScope}"
            : $"Tìm thấy {results.Count} nhân sự khớp với \"{SearchKeyword}\"";

        OnPropertyChanged(nameof(HasStaffResults));
        OnPropertyChanged(nameof(StaffFilterResultText));
    }
}
