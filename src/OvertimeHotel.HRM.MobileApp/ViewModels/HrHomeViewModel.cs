using System.Collections.ObjectModel;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.Core.DTOs;
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
	private int _reviewedLeaveCount;
	private int _expiringContractCount;
	private string _expiringContractDescription = "Chưa tải dữ liệu";
	private string _lastUpdatedText = "Chưa tải dữ liệu";
	private bool _isRefreshing;

	// Tra cứu nhanh nhân sự
	private string _searchKeyword = string.Empty;
	private string _selectedDepartment = "Tất cả";
	private string _filterResultText = "Chưa tải dữ liệu";
	private bool _hasSearchResults;

	// Quân số ca trực hôm nay (Đặc thù khách sạn 24/7)
	private string _morningShiftText = "Chưa tải dữ liệu";
	private string _morningShiftStatus = "Chưa tải dữ liệu";
	private string _afternoonShiftText = "Chưa tải dữ liệu";
	private string _afternoonShiftStatus = "Chưa tải dữ liệu";
	private string _nightShiftText = "Chưa tải dữ liệu";
	private string _nightShiftStatus = "Chưa tải dữ liệu";
	private string _onDutyStaffSummary = "Chưa tải dữ liệu";
	private string _leaveAndAbsenceText = "Chưa tải dữ liệu";

	public ObservableCollection<DepartmentFilterChip> DepartmentChips { get; } = new();

	public ObservableCollection<HrStaffLookupItem> SearchResults { get; } = new();
	public ObservableCollection<HrDailyEventItem> DailyEvents { get; } = new();

	private readonly HrSupabaseService _api;
	private readonly List<HrStaffLookupItem> _hotelStaffDirectory = new();
	private bool _loading;
	private string _loadError = "";
	public string LoadError { get => _loadError; private set => SetProperty(ref _loadError, value); }

	public Command ApproveCommand { get; }
	public Command ContractsCommand { get; }
	public Command RefreshCommand { get; }
	public Command<string> SelectDepartmentCommand { get; }
	public Command SearchStaffCommand { get; }
	public Command<HrStaffLookupItem> ContactStaffCommand { get; }
	public Command<HrDailyEventItem> EventActionCommand { get; }

	public event EventHandler<string>? ShowNotificationDialog;

	public HrHomeViewModel(IAuthService authService, ILeaveApprovalService leaveApprovalService, HrSupabaseService api)
		: base(authService, leaveApprovalService)
	{
		_api = api;
		ApproveCommand = new Command(() => _ = Shell.Current.GoToAsync("LeaveApprovalPage"));

		ContractsCommand = new Command(() =>
		{
			ShowNotificationDialog?.Invoke(this, $"{ExpiringContractDescription}\nVui lòng kiểm tra chi tiết trên cổng quản lý máy tính.");
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
			ShowNotificationDialog?.Invoke(this, $"Thông Tin Nhân Sự:\n• {item.FullName} — Mã: {item.StaffCode}\n• Bộ phận: {item.Department} · {item.Position}\n• Trạng thái: {item.WorkStatusDetail}\n• Điện thoại: {item.InternalPhone}");
		});

		EventActionCommand = new Command<HrDailyEventItem>(item =>
		{
			if (item == null) return;
			ShowNotificationDialog?.Invoke(this, $"{item.Title}\n{item.Subtitle}");
		});

		RefreshCommand = new Command(async () =>
		{
			IsRefreshing = true;
			await LoadAsync();
			IsRefreshing = false;
		});

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
		if (_loading) return;
		_loading = true;
		IsRefreshing = true;
		LoadError = "";
		try
		{
			RefreshHrScope();
			await base.LoadAsync();
			var data = await _api.GetDashboardAsync();
			_hotelStaffDirectory.Clear();
			_hotelStaffDirectory.AddRange(data.Staff.Select(x => new HrStaffLookupItem
			{
				StaffCode = x.StaffCode, FullName = x.FullName, Department = x.Department,
				Position = x.Position, WorkStatus = x.WorkStatus, WorkStatusDetail = x.WorkStatusDetail,
				InternalPhone = x.Phone
			}));
			DepartmentChips.Clear();
			DepartmentChips.Add(new() { Name = "Tất cả", IsSelected = true });
			foreach (var dept in data.Staff.Select(x => x.Department).Distinct().OrderBy(x => x))
				DepartmentChips.Add(new() { Name = dept });
			SelectedDepartment = "Tất cả";
			FilterStaff();
			MorningShiftText = data.Morning.Display;
			MorningShiftStatus = data.Morning.Status;
			AfternoonShiftText = data.Afternoon.Display;
			AfternoonShiftStatus = data.Afternoon.Status;
			NightShiftText = data.Night.Display;
			NightShiftStatus = data.Night.Status;
			OnDutyStaffSummary = $"{data.OnDutyCount} nhân sự đã vào và chưa ra ca";
			LeaveAndAbsenceText = $"{data.LeaveCount} nghỉ được duyệt · {data.AbsentCount} ca kết thúc không chấm công";
			ReviewedLeaveCount = data.ReviewedCount;
			PendingLeaveCount = data.PendingCount;
			PendingLeaveSub = data.PendingCount == 0 ? "Không có đơn chờ duyệt" : "Toàn khách sạn";
			ExpiringContractCount = data.ExpiringContractCount;
			ExpiringContractDescription = data.ContractDescription;
			DailyEvents.Clear();
			foreach (var item in data.Events)
				DailyEvents.Add(new() { Icon = item.Icon, Title = item.Title, Subtitle = item.Subtitle });
			LastUpdatedText = $"Cập nhật lúc {data.UpdatedAt.ToLocalTime():HH:mm}";
		}
		catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or UnauthorizedAccessException or TaskCanceledException or System.Text.Json.JsonException)
		{
			LoadError = $"Không thể tải dữ liệu HR: {ex.Message}";
			_hotelStaffDirectory.Clear(); SearchResults.Clear(); DepartmentChips.Clear(); DailyEvents.Clear();
			HasSearchResults = false;
			FilterResultText = "Chưa tải được danh sách nhân sự";
			MorningShiftText = AfternoonShiftText = NightShiftText = "—";
			MorningShiftStatus = AfternoonShiftStatus = NightShiftStatus = "Chưa có dữ liệu";
			OnDutyStaffSummary = LeaveAndAbsenceText = ExpiringContractDescription = "Chưa tải được dữ liệu";
			ExpiringContractCount = ReviewedLeaveCount = PendingLeaveCount = 0;
			PendingLeaveSub = "Chưa tải được dữ liệu";
			LastUpdatedText = "Chưa cập nhật";
		}
		finally { _loading = false; IsRefreshing = false; }
	}

	protected override Task LoadBranchKpiAsync(UserSession user) => Task.CompletedTask;

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
			FilterResultText = $"Tổng {_hotelStaffDirectory.Count} nhân sự · Gõ mã hoặc họ tên để tra cứu";
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

}
