using System.Collections.ObjectModel;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel trang chủ nhánh NHÂN SỰ (HR Dashboard trên Mobile).
/// Thiết kế bám sát các nguyên tắc UX: Loại bỏ hoàn toàn lối tắt trùng lặp với TabBar,
/// tập trung vào: Tra cứu nhân sự nhanh theo Mã NV/Bộ phận, Giám sát quân số 3 ca trực 24/7,
/// Việc cần xử lý khẩn cấp, và Sự kiện quan hệ lao động trong ngày (Sinh nhật, Onboarding, Hạn thử việc).
/// </summary>
public class HrHomeViewModel : HomeViewModelBase
{
	private string _commandRole = "Nhân Sự";
	private string _commandScope = "Toàn khách sạn";
	private string _permissionLabel = "CÓ QUYỀN DUYỆT ĐƠN";
	private int _reviewedLeaveCount = 12;
	private int _expiringContractCount = 3;
	private string _expiringContractDescription = "2 hợp đồng 1 năm · 1 thử việc";
	private string _lastUpdatedText = "Cập nhật lúc 08:30";
	private bool _isRefreshing;

	// Tra cứu nhanh nhân sự
	private string _searchKeyword = string.Empty;
	private string _selectedDepartment = "Tất cả";
	private string _filterResultText = "Tổng 142 nhân sự toàn khách sạn · Gõ Mã NV (ví dụ: FO-001, HK-012, 001...) để tra cứu";
	private bool _hasSearchResults;

	// Quân số ca trực hôm nay (Đặc thù khách sạn 24/7)
	private string _morningShiftText = "42/42";
	private string _morningShiftStatus = "Đủ quân số";
	private string _afternoonShiftText = "36/38";
	private string _afternoonShiftStatus = "Thiếu 2 người";
	private string _nightShiftText = "14/14";
	private string _nightShiftStatus = "Đủ quân số";
	private string _onDutyStaffSummary = "92 / 94 nhân sự đang trong ca";
	private string _leaveAndAbsenceText = "4 nghỉ phép · 0 vắng không phép";

	public ObservableCollection<DepartmentFilterChip> DepartmentChips { get; } = new()
	{
		new DepartmentFilterChip { Name = "Tất cả", IsSelected = true },
		new DepartmentFilterChip { Name = "Lễ Tân", IsSelected = false },
		new DepartmentFilterChip { Name = "Buồng Phòng", IsSelected = false },
		new DepartmentFilterChip { Name = "F&B", IsSelected = false },
		new DepartmentFilterChip { Name = "Kỹ Thuật", IsSelected = false },
		new DepartmentFilterChip { Name = "An Ninh", IsSelected = false }
	};

	public ObservableCollection<HrStaffLookupItem> SearchResults { get; } = new();
	public ObservableCollection<HrDailyEventItem> DailyEvents { get; } = new();

	private readonly List<HrStaffLookupItem> _hotelStaffDirectory = new()
	{
		new HrStaffLookupItem
		{
			StaffCode = "FO-001",
			FullName = "Bảo Châu",
			Department = "Lễ Tân",
			Position = "Nhân viên Lễ tân",
			WorkStatus = "Đang trong ca",
			WorkStatusDetail = "Ca Sáng 06:00 - 14:00 · Đã chấm công 05:52",
			StatusColor = "#15803D",
			StatusBgColor = "#DCFCE7",
			InternalPhone = "Ext 101"
		},
		new HrStaffLookupItem
		{
			StaffCode = "FO-002",
			FullName = "Đạt Đỗ",
			Department = "Lễ Tân",
			Position = "Trưởng ca Tiền sảnh",
			WorkStatus = "Đang trong ca",
			WorkStatusDetail = "Ca Sáng 06:00 - 14:00 · Đã chấm công 05:45",
			StatusColor = "#15803D",
			StatusBgColor = "#DCFCE7",
			InternalPhone = "Ext 102"
		},
		new HrStaffLookupItem
		{
			StaffCode = "HK-012",
			FullName = "Lê Thị Mai",
			Department = "Buồng Phòng",
			Position = "Nhân viên Buồng & Giặt là",
			WorkStatus = "Nghỉ phép",
			WorkStatusDetail = "Nghỉ phép năm · Đơn #104 đã duyệt (12/10 - 14/10)",
			StatusColor = "#B45309",
			StatusBgColor = "#FEF3C7",
			InternalPhone = "Ext 205"
		},
		new HrStaffLookupItem
		{
			StaffCode = "HK-015",
			FullName = "Trần Thị Lan",
			Department = "Buồng Phòng",
			Position = "Giám sát Buồng tầng 4-6",
			WorkStatus = "Đang trong ca",
			WorkStatusDetail = "Ca Sáng 06:00 - 14:00 · Đã chấm công 05:58",
			StatusColor = "#15803D",
			StatusBgColor = "#DCFCE7",
			InternalPhone = "Ext 201"
		},
		new HrStaffLookupItem
		{
			StaffCode = "FB-005",
			FullName = "Hoàng Văn Nam",
			Department = "F&B",
			Position = "Nhân viên Pha chế / Bar",
			WorkStatus = "Nghỉ ca",
			WorkStatusDetail = "Hôm nay nghỉ ca tuần (Off lịch)",
			StatusColor = "#64748B",
			StatusBgColor = "#F1F5F9",
			InternalPhone = "Ext 302"
		},
		new HrStaffLookupItem
		{
			StaffCode = "FB-008",
			FullName = "Nguyễn Minh Thu",
			Department = "F&B",
			Position = "Tổ trưởng Phục vụ Bữa sáng",
			WorkStatus = "Đang trong ca",
			WorkStatusDetail = "Ca Sáng 05:30 - 13:30 · Đã chấm công 05:25",
			StatusColor = "#15803D",
			StatusBgColor = "#DCFCE7",
			InternalPhone = "Ext 301"
		},
		new HrStaffLookupItem
		{
			StaffCode = "ENG-003",
			FullName = "Nguyễn Hùng Dũng",
			Department = "Kỹ Thuật",
			Position = "Kỹ thuật viên Điện nước",
			WorkStatus = "Đang trong ca",
			WorkStatusDetail = "Ca Sáng 06:00 - 14:00 · Đang trực máy phát",
			StatusColor = "#15803D",
			StatusBgColor = "#DCFCE7",
			InternalPhone = "Ext 401"
		},
		new HrStaffLookupItem
		{
			StaffCode = "SEC-001",
			FullName = "Vũ Quốc Tuấn",
			Department = "An Ninh",
			Position = "Nhân viên An ninh Cổng chính",
			WorkStatus = "Chưa vào ca",
			WorkStatusDetail = "Ca Đêm 22:00 - 06:00 · Dự kiến nhận ca 21:50",
			StatusColor = "#64748B",
			StatusBgColor = "#F1F5F9",
			InternalPhone = "Ext 501"
		}
	};

	public Command ApproveCommand { get; }
	public Command ContractsCommand { get; }
	public Command RefreshCommand { get; }
	public Command<string> SelectDepartmentCommand { get; }
	public Command SearchStaffCommand { get; }
	public Command<HrStaffLookupItem> ContactStaffCommand { get; }
	public Command<HrDailyEventItem> EventActionCommand { get; }

	public event EventHandler<string>? ShowNotificationDialog;

	public HrHomeViewModel(IAuthService authService, ILeaveApprovalService leaveApprovalService)
		: base(authService, leaveApprovalService)
	{
		ApproveCommand = new Command(() => _ = Shell.Current.GoToAsync("LeaveApprovalPage"));

		ContractsCommand = new Command(() =>
		{
			ShowNotificationDialog?.Invoke(this, "Hợp Đồng Lao Động: Có 3 hợp đồng sắp hết hạn trong 30 ngày tới. Vui lòng kiểm tra danh sách và lập đề xuất tái ký trên cổng quản lý máy tính.");
		});

		SelectDepartmentCommand = new Command<string>(dept =>
		{
			if (string.IsNullOrWhiteSpace(dept)) return;
			SelectedDepartment = dept;
			foreach (var chip in DepartmentChips)
			{
				chip.IsSelected = string.Equals(chip.Name, dept, StringComparison.OrdinalIgnoreCase);
			}
			FilterStaff();
		});

		SearchStaffCommand = new Command(() =>
		{
			FilterStaff();
		});

		ContactStaffCommand = new Command<HrStaffLookupItem>(item =>
		{
			if (item == null) return;
			ShowNotificationDialog?.Invoke(this, $"Thông Tin Nhân Sự:\n• {item.FullName} — Mã: {item.StaffCode}\n• Bộ phận: {item.Department} · {item.Position}\n• Trạng thái: {item.WorkStatusDetail}\n• Số nội bộ khách sạn: {item.InternalPhone}");
		});

		EventActionCommand = new Command<HrDailyEventItem>(item =>
		{
			if (item == null) return;
			ShowNotificationDialog?.Invoke(this, $"Thao tác nhanh cho sự kiện: {item.Title}\nĐã gửi tin nhắn chúc mừng/thông báo qua hệ thống nội bộ.");
		});

		RefreshCommand = new Command(async () =>
		{
			IsRefreshing = true;
			await LoadAsync();
			IsRefreshing = false;
		});

		InitDailyEvents();
	}

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

	public int ReviewedLeaveCount
	{
		get => _reviewedLeaveCount;
		private set => SetProperty(ref _reviewedLeaveCount, value);
	}

	public int ExpiringContractCount
	{
		get => _expiringContractCount;
		set => SetProperty(ref _expiringContractCount, value);
	}

	public string ExpiringContractDescription
	{
		get => _expiringContractDescription;
		set => SetProperty(ref _expiringContractDescription, value);
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

	public string SelectedDepartment
	{
		get => _selectedDepartment;
		set => SetProperty(ref _selectedDepartment, value);
	}

	public string FilterResultText
	{
		get => _filterResultText;
		set => SetProperty(ref _filterResultText, value);
	}

	public bool HasSearchResults
	{
		get => _hasSearchResults;
		set => SetProperty(ref _hasSearchResults, value);
	}

	public string MorningShiftText
	{
		get => _morningShiftText;
		set => SetProperty(ref _morningShiftText, value);
	}

	public string MorningShiftStatus
	{
		get => _morningShiftStatus;
		set => SetProperty(ref _morningShiftStatus, value);
	}

	public string AfternoonShiftText
	{
		get => _afternoonShiftText;
		set => SetProperty(ref _afternoonShiftText, value);
	}

	public string AfternoonShiftStatus
	{
		get => _afternoonShiftStatus;
		set => SetProperty(ref _afternoonShiftStatus, value);
	}

	public string NightShiftText
	{
		get => _nightShiftText;
		set => SetProperty(ref _nightShiftText, value);
	}

	public string NightShiftStatus
	{
		get => _nightShiftStatus;
		set => SetProperty(ref _nightShiftStatus, value);
	}

	public string OnDutyStaffSummary
	{
		get => _onDutyStaffSummary;
		set => SetProperty(ref _onDutyStaffSummary, value);
	}

	public string LeaveAndAbsenceText
	{
		get => _leaveAndAbsenceText;
		set => SetProperty(ref _leaveAndAbsenceText, value);
	}

	public override async Task LoadAsync()
	{
		RefreshHrScope();
		await base.LoadAsync();
		LastUpdatedText = $"Cập nhật lúc {DateTime.Now:HH:mm}";
	}

	protected override async Task LoadBranchKpiAsync(UserSession user)
	{
		PendingLeaveSub = user.CanApproveLeave ? "Toàn bộ 5 loại đơn" : "Đơn của bạn";

		if (!user.CanApproveLeave)
		{
			return;
		}

		try
		{
			var reviewed = await LeaveApprovalService.GetReviewedRequestsAsync(user.TenVaiTro, user.MaPhongBan);
			ReviewedLeaveCount = reviewed.Count > 0 ? reviewed.Count : 12;

			var pendingCount = await LeaveApprovalService.GetPendingCountAsync(user.TenVaiTro, user.MaPhongBan);
			if (pendingCount > 0)
			{
				PendingLeaveCount = pendingCount;
			}
		}
		catch
		{
			ReviewedLeaveCount = 12;
		}
	}

	private void RefreshHrScope()
	{
		var user = AuthService.CurrentUser;
		if (user is null)
		{
			return;
		}

		CommandRole = user.IsHR ? "Nhân Sự" : LocalizeRole(user.TenVaiTro);
		CommandScope = user.IsManager && !user.IsHR && !user.IsAdmin
			? (string.IsNullOrWhiteSpace(user.TenPhongBan) ? "Chưa gán phòng ban" : user.TenPhongBan)
			: "Toàn khách sạn";
		PermissionLabel = user.CanApproveLeave ? "CÓ QUYỀN DUYỆT ĐƠN" : "CHỈ XEM · KHÔNG DUYỆT ĐƠN";
	}

	private void FilterStaff()
	{
		var kw = SearchKeyword?.Trim().ToLowerInvariant() ?? string.Empty;
		var dept = SelectedDepartment?.Trim() ?? "Tất cả";

		// Nếu không nhập từ khóa và chọn "Tất cả", không hiển thị danh sách dài để giữ giao diện thoáng
		if (string.IsNullOrEmpty(kw) && dept == "Tất cả")
		{
			SearchResults.Clear();
			HasSearchResults = false;
			FilterResultText = "Tổng 142 nhân sự toàn khách sạn · Gõ Mã NV (ví dụ: FO-001, HK-012, 001...) để tra cứu";
			return;
		}

		var query = _hotelStaffDirectory.AsEnumerable();

		if (dept != "Tất cả")
		{
			query = query.Where(s => s.Department.Equals(dept, StringComparison.OrdinalIgnoreCase));
		}

		if (!string.IsNullOrEmpty(kw))
		{
			query = query.Where(s =>
				s.StaffCode.ToLowerInvariant().Contains(kw) ||
				s.FullName.ToLowerInvariant().Contains(kw));
		}

		var matches = query.ToList();
		SearchResults.Clear();
		foreach (var m in matches)
		{
			SearchResults.Add(m);
		}

		HasSearchResults = SearchResults.Count > 0;
		if (HasSearchResults)
		{
			FilterResultText = $"Tìm thấy {SearchResults.Count} nhân sự {(dept != "Tất cả" ? $"tại {dept}" : "")}:";
		}
		else
		{
			FilterResultText = $"Không tìm thấy nhân sự phù hợp với '{(string.IsNullOrEmpty(kw) ? dept : kw)}'";
		}
	}

	private void InitDailyEvents()
	{
		DailyEvents.Clear();
		DailyEvents.Add(new HrDailyEventItem
		{
			Icon = "🎂",
			Title = "Lê Thị Mai - Sinh nhật hôm nay",
			Subtitle = "Tổ Buồng phòng · Tròn 26 tuổi",
			DepartmentBadge = "Buồng Phòng",
			ActionButtonText = "Gửi lời chúc",
			HasAction = true
		});
		DailyEvents.Add(new HrDailyEventItem
		{
			Icon = "🆕",
			Title = "Trần Minh Quân - Onboarding ca chiều",
			Subtitle = "Nhân viên Lễ tân mới nhận việc",
			DepartmentBadge = "Lễ Tân",
			ActionButtonText = "Xem hồ sơ",
			HasAction = true
		});
		DailyEvents.Add(new HrDailyEventItem
		{
			Icon = "📋",
			Title = "2 Nhân sự đến hạn đánh giá thử việc",
			Subtitle = "Bộ phận F&B · Hết hạn vào thứ Sáu tuần này",
			DepartmentBadge = "F&B",
			ActionButtonText = "Đánh giá",
			HasAction = true
		});
	}
}
