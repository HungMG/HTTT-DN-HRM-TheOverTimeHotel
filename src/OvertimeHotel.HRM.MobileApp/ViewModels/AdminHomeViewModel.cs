using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel cho Trang Chủ Ban Giám Đốc / Tổng Quản Lý (Executive Admin Dashboard trên Mobile).
/// Dữ liệu 100% được truy vấn động từ CSDL Supabase PostgREST (nhan_vien, phan_ca, cham_cong, don_tu, ky_luong, hop_dong, phieu_luong).
/// Tuyệt đối không sử dụng hardcode hay dữ liệu giả định.
/// </summary>
public class AdminHomeViewModel : HomeViewModelBase
{
    private readonly SupabaseRestClient _restClient;

    private string _commandRole = "Tổng Giám Đốc / Ban Quản Trị";
    private string _commandScope = "Toàn hệ thống khách sạn 5 sao";
    private string _permissionLabel = "BAN GIÁM ĐỐC · TOÀN QUYỀN ĐIỀU HÀNH";
    private string _lastUpdatedText = "Đang cập nhật...";
    private bool _isRefreshing;

    // ═══ 1. QUÂN SỐ TOÀN KHÁCH SẠN & TỶ LỆ HIỆN DIỆN HÔM NAY (REAL DATA) ═══
    private int _totalStaffCount;
    private int _presentStaffCount;
    private int _onLeaveStaffCount;
    private string _attendanceRateText = "0%";
    private double _attendanceProgress;
    private string _workforceHealthStatus = "ĐANG TẢI DỮ LIỆU";
    private string _workforceHealthColor = "#64748B";
    private string _workforceHealthBgColor = "#F1F5F9";
    public ObservableCollection<AdminDepartmentWorkforceItem> DepartmentClusters { get; } = new();

    // ═══ 2. TÌNH TRẠNG QUỸ LƯƠNG & KHÓA SỔ KỲ LƯƠNG (PRD 02 & 05 - REAL DATA) ═══
    private string _payrollPeriodName = "Kỳ Lương Hiện Tại";
    private string _payrollStatusText = "Đang kiểm tra";
    private string _payrollStatusBgColor = "#F1F5F9";
    private string _payrollStatusTextColor = "#64748B";
    private string _budgetStatusNote = "Đang kết nối hệ thống tính lương CSDL...";
    private string _payrollActionText = "Chi tiết kỳ lương & Khóa sổ";
    public ObservableCollection<AdminRecentMonthPayrollItem> RecentMonthsPayroll { get; } = new();

    // ═══ 3. SỰ KIỆN TRỌNG ĐIỂM & ĐIỀU ĐỘNG TASK FORCE (PRD 02 Mục 9.0) ═══
    private string _activeEventsCountText = "0 sự kiện";
    private int _totalTaskForceStaffCount;
    private bool _hasHotelEvents;
    private bool _hasNoHotelEvents = true;
    public ObservableCollection<AdminHotelEventItem> HotelEvents { get; } = new();

    // ═══ HOẠT ĐỘNG GẦN ĐÂY ═══
    public ObservableCollection<AdminAuditLogItem> RecentActivities { get; } = new();

    public ICommand OpenLeaveApprovalCommand { get; }
    public ICommand OpenAuditLogsCommand { get; }
    public ICommand OpenDesktopWebCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand PayrollActionCommand { get; }
    public ICommand OpenEventDetailCommand { get; }

    public event EventHandler<string>? ShowNotificationDialog;

    public string CommandRole
    {
        get => _commandRole;
        private set => SetProperty(ref _commandRole, value);
    }

    public string CommandScope
    {
        get => _commandScope;
        private set => SetProperty(ref _commandScope, value);
    }

    public string PermissionLabel
    {
        get => _permissionLabel;
        private set => SetProperty(ref _permissionLabel, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        set => SetProperty(ref _lastUpdatedText, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    // Workforce props
    public int TotalStaffCount
    {
        get => _totalStaffCount;
        set => SetProperty(ref _totalStaffCount, value);
    }

    public int PresentStaffCount
    {
        get => _presentStaffCount;
        set => SetProperty(ref _presentStaffCount, value);
    }

    public int OnLeaveStaffCount
    {
        get => _onLeaveStaffCount;
        set => SetProperty(ref _onLeaveStaffCount, value);
    }

    public string AttendanceRateText
    {
        get => _attendanceRateText;
        set => SetProperty(ref _attendanceRateText, value);
    }

    public double AttendanceProgress
    {
        get => _attendanceProgress;
        set => SetProperty(ref _attendanceProgress, value);
    }

    public string WorkforceHealthStatus
    {
        get => _workforceHealthStatus;
        set => SetProperty(ref _workforceHealthStatus, value);
    }

    public string WorkforceHealthColor
    {
        get => _workforceHealthColor;
        set => SetProperty(ref _workforceHealthColor, value);
    }

    public string WorkforceHealthBgColor
    {
        get => _workforceHealthBgColor;
        set => SetProperty(ref _workforceHealthBgColor, value);
    }

    // Payroll props
    public string PayrollPeriodName
    {
        get => _payrollPeriodName;
        set => SetProperty(ref _payrollPeriodName, value);
    }

    public string PayrollStatusText
    {
        get => _payrollStatusText;
        set => SetProperty(ref _payrollStatusText, value);
    }

    public string PayrollStatusBgColor
    {
        get => _payrollStatusBgColor;
        set => SetProperty(ref _payrollStatusBgColor, value);
    }

    public string PayrollStatusTextColor
    {
        get => _payrollStatusTextColor;
        set => SetProperty(ref _payrollStatusTextColor, value);
    }

    public string BudgetStatusNote
    {
        get => _budgetStatusNote;
        set => SetProperty(ref _budgetStatusNote, value);
    }

    public string PayrollActionText
    {
        get => _payrollActionText;
        set => SetProperty(ref _payrollActionText, value);
    }

    // Event props
    public string ActiveEventsCountText
    {
        get => _activeEventsCountText;
        set => SetProperty(ref _activeEventsCountText, value);
    }

    public int TotalTaskForceStaffCount
    {
        get => _totalTaskForceStaffCount;
        set => SetProperty(ref _totalTaskForceStaffCount, value);
    }

    public bool HasHotelEvents
    {
        get => _hasHotelEvents;
        set
        {
            if (SetProperty(ref _hasHotelEvents, value))
            {
                HasNoHotelEvents = !value;
            }
        }
    }

    public bool HasNoHotelEvents
    {
        get => _hasNoHotelEvents;
        private set => SetProperty(ref _hasNoHotelEvents, value);
    }

    public AdminHomeViewModel(
        IAuthService authService,
        ILeaveApprovalService leaveApprovalService,
        SupabaseRestClient restClient)
        : base(authService, leaveApprovalService)
    {
        _restClient = restClient;

        OpenLeaveApprovalCommand = new Command(() => _ = Shell.Current.GoToAsync("LeaveApprovalPage"));
        OpenAuditLogsCommand = new Command(() => _ = Shell.Current.GoToAsync("AdminAuditLogPage"));

        OpenDesktopWebCommand = new Command(() =>
        {
            ShowNotificationDialog?.Invoke(this, "Cổng Quản Trị Máy Tính: Các nghiệp vụ cấu hình lương, phân quyền và sao lưu dữ liệu cần thực hiện tại: https://hrm.overtime.local");
        });

        PayrollActionCommand = new Command(() =>
        {
            ShowNotificationDialog?.Invoke(this,
                $"Quy trình Khóa sổ Lương (PRD 05 Bước 5):\n" +
                $"• Kỳ lương hiện tại: {PayrollPeriodName}\n" +
                $"• Trạng thái: {PayrollStatusText}\n\n" +
                $"Ban Giám Đốc có thể kiểm tra chi tiết bảng lương từng phòng ban và ký duyệt khóa sổ trên cổng Web Admin máy tính: https://hrm.overtime.local");
        });

        OpenEventDetailCommand = new Command<AdminHotelEventItem>(item =>
        {
            if (item != null)
            {
                ShowNotificationDialog?.Invoke(this,
                    $"Điều động Nhân lực Sự kiện (PRD 02 Mục 9.0):\n" +
                    $"• Sự kiện: {item.EventName}\n" +
                    $"• Loại hình: {item.EventType}\n" +
                    $"• Địa điểm: {item.Location}\n" +
                    $"• Thời gian: {item.TimeRangeText}\n" +
                    $"• Dự kiến: {item.ExpectedGuests} khách\n" +
                    $"• Lực lượng Task Force: {item.TaskForceStaffCount} nhân sự\n" +
                    $"• Chi tiết phân bổ: {item.TaskForceDetail}");
            }
        });

        RefreshCommand = new Command(async () =>
        {
            IsRefreshing = true;
            await LoadAsync();
            IsRefreshing = false;
        });
    }

    public override async Task LoadAsync()
    {
        await base.LoadAsync();
        LastUpdatedText = $"Cập nhật lúc {DateTime.Now:HH:mm}";

        await LoadWorkforceDataAsync();
        await LoadPayrollSummaryAsync();
        await LoadHotelEventsAsync();
        await LoadRecentActivitiesAsync();
    }

    private async Task LoadWorkforceDataAsync()
    {
        try
        {
            var todayStr = DateTime.Today.ToString("yyyy-MM-dd");

            // 1. Truy vấn toàn bộ nhân viên thực tế đang làm việc từ CSDL Supabase
            var staffJson = await _restClient.GetAsync(
                "nhan_vien?select=ma_nhan_vien,ho,ten,ma_phong_ban,chuc_vu(ten_chuc_vu),phong_ban(ma_phong_ban,ten_phong_ban)" +
                "&trang_thai=neq.THOI_VIEC&order=ma_nhan_vien.asc");

            var staffArray = PostgrstJson.ParseArray(staffJson);
            TotalStaffCount = staffArray.Length;

            if (TotalStaffCount == 0)
            {
                PresentStaffCount = 0;
                OnLeaveStaffCount = 0;
                AttendanceProgress = 0;
                AttendanceRateText = "0%";
                WorkforceHealthStatus = "CHƯA CÓ NHÂN SỰ";
                WorkforceHealthColor = "#64748B";
                WorkforceHealthBgColor = "#F1F5F9";
                DepartmentClusters.Clear();
                return;
            }

            // 2. Lấy đơn nghỉ phép hợp lệ đã duyệt cho hôm nay
            HashSet<int> staffOnLeave = new();
            try
            {
                var leaveJson = await _restClient.GetAsync(
                    $"don_tu?select=ma_don,ma_nhan_vien,trang_thai" +
                    $"&trang_thai=eq.DA_DUYET&tu_ngay=lte.{todayStr}&den_ngay=gte.{todayStr}");

                foreach (var leave in PostgrstJson.ParseArray(leaveJson))
                {
                    var nvId = PostgrstJson.ReadNullableInt(leave, "ma_nhan_vien");
                    if (nvId.HasValue) staffOnLeave.Add(nvId.Value);
                }
            }
            catch { }

            OnLeaveStaffCount = staffOnLeave.Count;

            // 3. Lấy phân ca và chấm công thực tế hôm nay
            HashSet<int> scheduledStaffToday = new();
            HashSet<int> clockedInStaffToday = new();
            try
            {
                var shiftJson = await _restClient.GetAsync(
                    $"phan_ca?select=ma_phan_ca,ma_nhan_vien,ngay_lam_viec," +
                    "cham_cong(ma_cham_cong,gio_check_in,gio_check_out)" +
                    $"&ngay_lam_viec=eq.{todayStr}");

                foreach (var shift in PostgrstJson.ParseArray(shiftJson))
                {
                    var nvId = PostgrstJson.ReadNullableInt(shift, "ma_nhan_vien");
                    if (!nvId.HasValue) continue;

                    scheduledStaffToday.Add(nvId.Value);

                    var chamCong = PostgrstJson.ReadEmbedded(shift, "cham_cong");
                    if (chamCong.HasValue)
                    {
                        var checkIn = PostgrstJson.ReadString(chamCong.Value, "gio_check_in");
                        if (!string.IsNullOrWhiteSpace(checkIn))
                        {
                            clockedInStaffToday.Add(nvId.Value);
                        }
                    }
                }
            }
            catch { }

            // Xác định số lượng nhân sự có mặt / trực ca thực tế:
            // Theo yêu cầu: Chỉ lấy đúng số người đã có mặt thực tế (đã quẹt thẻ check-in hôm nay).
            // Nếu không ai check-in thì không tính (bằng 0) vì đây là đang trực ca mà không có mặt thì không tính.
            PresentStaffCount = clockedInStaffToday.Count;

            AttendanceProgress = TotalStaffCount > 0 ? (double)PresentStaffCount / TotalStaffCount : 0.0;
            AttendanceRateText = $"{AttendanceProgress * 100:0.#}%";

            if (AttendanceProgress >= 0.85)
            {
                WorkforceHealthStatus = "VẬN HÀNH ỔN ĐỊNH";
                WorkforceHealthColor = "#15803D";
                WorkforceHealthBgColor = "#DCFCE7";
            }
            else if (AttendanceProgress >= 0.50)
            {
                WorkforceHealthStatus = "CẦN THEO DÕI ĐỊNH BIÊN";
                WorkforceHealthColor = "#B45309";
                WorkforceHealthBgColor = "#FEF3C7";
            }
            else if (PresentStaffCount > 0)
            {
                WorkforceHealthStatus = "TỶ LỆ TRỰC CA THẤP";
                WorkforceHealthColor = "#B91C1C";
                WorkforceHealthBgColor = "#FEE2E2";
            }
            else
            {
                WorkforceHealthStatus = "CHƯA CÓ LƯỢT CHECK-IN";
                WorkforceHealthColor = "#64748B";
                WorkforceHealthBgColor = "#F1F5F9";
            }

            // 4. Phân bổ thực tế vào 3 khối vận hành khách sạn dựa trên danh mục phòng ban thật
            int c1Total = 0, c1Present = 0;
            int c2Total = 0, c2Present = 0;
            int c3Total = 0, c3Present = 0;

            foreach (var staff in staffArray)
            {
                var id = PostgrstJson.ReadNullableInt(staff, "ma_nhan_vien") ?? 0;
                var pbName = PostgrstJson.ReadEmbeddedString(staff, "phong_ban", "ten_phong_ban").ToLowerInvariant();
                var maPb = PostgrstJson.ReadNullableInt(staff, "ma_phong_ban") ?? 0;

                // Chỉ tính có mặt nếu nhân sự này đã quẹt thẻ check-in hôm nay
                bool isPresent = clockedInStaffToday.Contains(id);

                // Khối 1: Tuyến đầu & Vận hành (Tiền sảnh, Buồng phòng, F&B, Bếp)
                if (maPb is 2 or 3 or 4 or 5 ||
                    pbName.Contains("tiền sảnh") || pbName.Contains("lễ tân") || pbName.Contains("buồng") ||
                    pbName.Contains("ẩm thực") || pbName.Contains("bếp") || pbName.Contains("f&b"))
                {
                    c1Total++;
                    if (isPresent) c1Present++;
                }
                // Khối 2: Kỹ thuật & An ninh
                else if (maPb is 6 or 7 ||
                    pbName.Contains("kỹ thuật") || pbName.Contains("bảo trì") ||
                    pbName.Contains("an ninh") || pbName.Contains("bảo vệ") || pbName.Contains("pccc"))
                {
                    c2Total++;
                    if (isPresent) c2Present++;
                }
                // Khối 3: Hậu cần & Quản trị (Giám đốc, Nhân sự, Kế toán, Sales, IT)
                else
                {
                    c3Total++;
                    if (isPresent) c3Present++;
                }
            }

            var rate1 = c1Total > 0 ? (double)c1Present / c1Total : 0.0;
            var rate2 = c2Total > 0 ? (double)c2Present / c2Total : 0.0;
            var rate3 = c3Total > 0 ? (double)c3Present / c3Total : 0.0;

            DepartmentClusters.Clear();
            DepartmentClusters.Add(new AdminDepartmentWorkforceItem
            {
                ClusterName = "Khối Tuyến Đầu & Vận Hành",
                DepartmentScope = "Tiền Sảnh · Buồng Phòng · Ẩm Thực · Bếp",
                PresentCount = c1Present,
                TotalCount = c1Total,
                AttendanceRate = rate1,
                Icon = "🛎️",
                StatusColor = rate1 >= 0.8 ? "#15803D" : (rate1 > 0 ? "#B45309" : "#64748B"),
                ProgressColor = "#1E3A8A"
            });
            DepartmentClusters.Add(new AdminDepartmentWorkforceItem
            {
                ClusterName = "Khối Kỹ Thuật & An Ninh",
                DepartmentScope = "Kỹ Thuật Công Trình · An Ninh & PCCC",
                PresentCount = c2Present,
                TotalCount = c2Total,
                AttendanceRate = rate2,
                Icon = "⚡",
                StatusColor = rate2 >= 0.8 ? "#15803D" : (rate2 > 0 ? "#B45309" : "#64748B"),
                ProgressColor = "#0284C7"
            });
            DepartmentClusters.Add(new AdminDepartmentWorkforceItem
            {
                ClusterName = "Khối Hậu Cần & Quản Trị",
                DepartmentScope = "Ban Giám Đốc · Hành Chính Nhân Sự · Kế Toán",
                PresentCount = c3Present,
                TotalCount = c3Total,
                AttendanceRate = rate3,
                Icon = "🏢",
                StatusColor = rate3 >= 0.8 ? "#15803D" : (rate3 > 0 ? "#B45309" : "#64748B"),
                ProgressColor = "#D4AF37"
            });
        }
        catch (Exception ex)
        {
            WorkforceHealthStatus = "LỖI TẢI DỮ LIỆU CSDL";
            WorkforceHealthColor = "#B91C1C";
            WorkforceHealthBgColor = "#FEE2E2";
            BudgetStatusNote = $"Lỗi kết nối PostgREST: {ex.Message}";
        }
    }

    private async Task LoadPayrollSummaryAsync()
    {
        try
        {
            var viVn = new CultureInfo("vi-VN");
            var now = DateTime.Today;

            // 1. Gọi RPC SECURITY DEFINER để lấy tổng hợp quỹ lương toàn khách sạn cho Ban Giám Đốc
            //    (chỉ tính các phiếu đã khóa/thanh toán, vượt qua giới hạn đọc phiếu cá nhân của nhân viên thường)
            var summaryJson = await _restClient.PostAsync(
                "rpc/hrm_admin_payroll_summary",
                "{\"p_limit\":12}");
            var summaryRows = PostgrstJson.ParseArray(summaryJson);

            // 2. Tạo danh sách 5 tháng gần đây tính từ tháng hiện tại lùi về
            var months = new List<(AdminRecentMonthPayrollItem Item, decimal Amount)>(5);
            decimal maxAmount = 0m;

            for (int i = 0; i < 5; i++)
            {
                var targetDate = now.AddMonths(-i);
                int m = targetDate.Month;
                int y = targetDate.Year;

                JsonElement? matchedRow = null;
                foreach (var row in summaryRows)
                {
                    int rowThang = PostgrstJson.ReadNullableInt(row, "thang") ?? 0;
                    int rowNam = PostgrstJson.ReadNullableInt(row, "nam") ?? 0;
                    if (rowThang == m && rowNam == y)
                    {
                        matchedRow = row;
                        break;
                    }
                }

                var item = new AdminRecentMonthPayrollItem
                {
                    MonthName = $"Tháng {m:D2}/{y}",
                    IsCurrentPeriod = (i == 0)
                };

                decimal amountForBar = 0m;

                if (matchedRow.HasValue)
                {
                    var status = PostgrstJson.ReadString(matchedRow.Value, "trang_thai");
                    var soPhieu = PostgrstJson.ReadNullableInt(matchedRow.Value, "so_phieu") ?? 0;
                    var tongThucNhan = PostgrstJson.ReadDecimal(matchedRow.Value, "tong_thuc_nhan");

                    if (soPhieu > 0)
                    {
                        amountForBar = tongThucNhan;
                        if (tongThucNhan > maxAmount) maxAmount = tongThucNhan;

                        item.TotalPayoutText = tongThucNhan.ToString("N0", viVn) + " đ";
                        item.SubtitleText = $"{soPhieu} phiếu lương đã khóa";

                        (item.StatusText, item.StatusBgColor, item.StatusTextColor, item.BarColor) = status switch
                        {
                            "KHOA_SO" or "DA_KHOA_SO" or "DA_THANH_TOAN" => ("Đã Khóa Sổ", "#DCFCE7", "#15803D", "#15803D"),
                            "DA_DUYET" => ("Đã Duyệt", "#DCFCE7", "#15803D", "#15803D"),
                            _ => ("Đang Mở", "#EFF6FF", "#1D4ED8", "#1E3A8A")
                        };
                    }
                    else
                    {
                        // Kỳ đã tạo nhưng chưa khóa sổ hoặc chưa có phiếu đã thanh toán
                        item.TotalPayoutText = i == 0 ? "Đang tính (Dự thảo)" : "0 đ";
                        item.SubtitleText = i == 0 ? "Kỳ đang mở · Chưa khóa sổ" : "Chưa khóa sổ · 0 phiếu";
                        item.StatusText = i == 0 ? "Dự Thảo" : "Chưa Khóa Sổ";
                        item.StatusBgColor = "#FEF3C7";
                        item.StatusTextColor = "#B45309";
                        item.BarColor = "#D4AF37";
                    }
                }
                else
                {
                    item.TotalPayoutText = "—";
                    item.SubtitleText = "Chưa phát sinh dữ liệu";
                    item.StatusText = "Chưa Tạo Kỳ";
                    item.StatusBgColor = "#F1F5F9";
                    item.StatusTextColor = "#94A3B8";
                    item.BarColor = "#CBD5E1";
                }

                months.Add((item, amountForBar));
            }

            RecentMonthsPayroll.Clear();
            foreach (var (item, amount) in months)
            {
                item.BarProgress = maxAmount > 0 && amount > 0
                    ? Math.Min(1.0, Math.Max(0.08, (double)amount / (double)maxAmount))
                    : 0.04;
                RecentMonthsPayroll.Add(item);
            }

            PayrollPeriodName = $"Kỳ Lương Tháng {now.Month:D2}/{now.Year}";
            PayrollStatusText = "Thẩm quyền Ban Giám Đốc";
            PayrollStatusBgColor = "#EFF6FF";
            PayrollStatusTextColor = "#1D4ED8";
            BudgetStatusNote = "Tổng hợp quỹ lương toàn khách sạn qua RPC bảo mật (chỉ tính phiếu đã khóa sổ).";
            PayrollActionText = "Chi tiết kỳ lương & Khóa sổ";
        }
        catch (Exception ex)
        {
            BudgetStatusNote = $"Lỗi kết nối CSDL kỳ lương: {ex.Message}";
        }
    }

    private async Task LoadHotelEventsAsync()
    {
        // Gọi RPC SECURITY DEFINER lấy sự kiện trọng điểm đang diễn ra/sắp tới kèm số nhân sự Task Force thật từ phan_cong_du_an
        try
        {
            var eventJson = await _restClient.PostAsync(
                "rpc/hrm_admin_hotel_events",
                "{\"p_limit\":4}");
            var list = PostgrstJson.ParseArray(eventJson);

            if (list.Length > 0)
            {
                HotelEvents.Clear();
                int totalTaskForce = 0;

                foreach (var row in list)
                {
                    var id = PostgrstJson.ReadNullableInt(row, "ma_du_an") ?? 0;
                    var name = PostgrstJson.ReadString(row, "ten_du_an");
                    var type = PostgrstJson.ReadString(row, "loai_du_an");
                    var loc = PostgrstJson.ReadString(row, "dia_diem");
                    var guests = PostgrstJson.ReadNullableInt(row, "so_khach_du_kien") ?? 0;
                    var status = PostgrstJson.ReadString(row, "trang_thai");
                    var tfStaff = PostgrstJson.ReadNullableInt(row, "task_force_count") ?? 0;
                    var tfDetail = PostgrstJson.ReadString(row, "task_force_detail");

                    var startUtc = PostgrstJson.ReadNullableTimestamp(row, "ngay_bat_dau");
                    var endUtc = PostgrstJson.ReadNullableTimestamp(row, "ngay_ket_thuc");
                    var timeRange = FormatEventTimeRange(startUtc, endUtc);

                    var (typeLabel, typeBg, typeText) = FormatEventTypeStyle(type);
                    totalTaskForce += tfStaff;

                    HotelEvents.Add(new AdminHotelEventItem
                    {
                        EventId = id,
                        EventName = !string.IsNullOrWhiteSpace(name) ? name : $"Sự kiện #{id}",
                        EventType = typeLabel,
                        Location = !string.IsNullOrWhiteSpace(loc) ? loc : "Sảnh Khách Sạn",
                        TimeRangeText = timeRange,
                        ExpectedGuests = guests,
                        TaskForceStaffCount = tfStaff,
                        TaskForceDetail = !string.IsNullOrWhiteSpace(tfDetail)
                            ? tfDetail
                            : "Chưa phân công nhân sự Task Force",
                        StatusText = status == "DANG_DIEN_RA" ? "Đang diễn ra" : "Đã lên kế hoạch",
                        StatusBgColor = status == "DANG_DIEN_RA" ? "#DCFCE7" : "#FEF3C7",
                        StatusTextColor = status == "DANG_DIEN_RA" ? "#15803D" : "#B45309",
                        TypeBgColor = typeBg,
                        TypeTextColor = typeText
                    });
                }

                TotalTaskForceStaffCount = totalTaskForce;
                ActiveEventsCountText = $"{HotelEvents.Count} sự kiện";
                HasHotelEvents = true;
                return;
            }
        }
        catch
        {
            // Giữ trạng thái an toàn theo Phương án A nếu kết nối lỗi
        }

        // Trung thực theo Phương án A: Không có sự kiện thật trong DB
        HotelEvents.Clear();
        TotalTaskForceStaffCount = 0;
        ActiveEventsCountText = "0 sự kiện";
        HasHotelEvents = false;
    }

    private async Task LoadRecentActivitiesAsync()
    {
        // Đọc lịch sử thao tác và kiểm toán hệ thống thực tế trong 7 ngày gần đây từ bảng nhat_ky_quan_tri
        RecentActivities.Clear();
        bool loadedSuccessfully = false;

        try
        {
            var logs = await AdminAuditLogPostgrstMapper.FetchRecentAsync(_restClient, withinDays: 7, limit: 5);
            loadedSuccessfully = true;

            foreach (var log in logs)
            {
                RecentActivities.Add(log);
            }
        }
        catch (Exception ex)
        {
            RecentActivities.Add(new AdminAuditLogItem
            {
                AuditLogId = 0,
                ActionCode = "REJECT",
                ActionName = "Lỗi tải",
                Description = $"Không thể tải nhật ký kiểm toán: {ex.Message}",
                ActorName = "System",
                TargetUsername = "BOD",
                OccurredAt = DateTimeOffset.Now
            });
        }

        // Nếu truy vấn thành công nhưng không có thao tác kiểm toán nào phát sinh trong 7 ngày qua
        if (loadedSuccessfully && RecentActivities.Count == 0)
        {
            RecentActivities.Add(new AdminAuditLogItem
            {
                AuditLogId = 1,
                ActionCode = "SYS_AUDIT",
                ActionName = "Hệ thống",
                Description = "Không có thao tác kiểm toán nào phát sinh trong 7 ngày gần đây",
                ActorName = "System",
                TargetUsername = "BOD",
                OccurredAt = DateTimeOffset.Now
            });
        }
    }

    private static string FormatEventTimeRange(DateTimeOffset? startUtc, DateTimeOffset? endUtc)
    {
        if (!startUtc.HasValue) return "Chưa rõ lịch";

        var vnOffset = TimeSpan.FromHours(7);
        var startLocal = startUtc.Value.ToOffset(vnOffset);
        if (!endUtc.HasValue) return startLocal.ToString("dd/MM · HH:mm");

        var endLocal = endUtc.Value.ToOffset(vnOffset);
        return startLocal.Date == endLocal.Date
            ? $"{startLocal:dd/MM} ({startLocal:HH:mm}–{endLocal:HH:mm})"
            : $"{startLocal:dd/MM HH:mm} – {endLocal:dd/MM HH:mm}";
    }

    private static (string Label, string BgColor, string TextColor) FormatEventTypeStyle(string? rawType)
    {
        return rawType switch
        {
            "TIEC_CUOI" => ("TIỆC CƯỚI", "#FCE7F3", "#BE185D"),
            "HOI_NGHI_MICE" => ("HỘI NGHỊ MICE", "#EFF6FF", "#1D4ED8"),
            "GALA_DINNER" => ("GALA DINNER", "#FEF3C7", "#B45309"),
            "FESTIVAL_AM_THUC" => ("FESTIVAL ẨM THỰC", "#ECFDF5", "#047857"),
            "HOP_BAO" => ("HỌP BÁO", "#F3E8FF", "#7E22CE"),
            "DOAN_VIP" => ("ĐOÀN VIP", "#FEF3C7", "#B45309"),
            _ => ("SỰ KIỆN KHÁCH SẠN", "#EFF6FF", "#1D4ED8")
        };
    }

    protected override async Task LoadBranchKpiAsync(UserSession user)
    {
        PendingLeaveSub = "Thẩm quyền Admin / Ban Giám Đốc";

        if (!user.CanApproveLeave) return;

        try
        {
            PendingLeaveCount = await LeaveApprovalService.GetPendingCountAsync(user.TenVaiTro, null);
        }
        catch
        {
            PendingLeaveCount = 0;
        }
    }
}
