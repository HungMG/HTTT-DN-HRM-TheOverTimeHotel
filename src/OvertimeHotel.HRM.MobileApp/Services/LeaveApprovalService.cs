using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Dịch vụ duyệt đơn từ Production kết nối Backend WebAdmin qua Mobile REST API có JWT.
/// </summary>
/// <remarks>
/// <para>
/// <b>Phạm vi có chủ đích:</b> khác với <c>AuthService</c> (đăng nhập + hồ sơ, gọi
/// thẳng Supabase), dịch vụ này CỐ TÌNH đi qua Web Admin. Lý do là nghiệp vụ
/// duyệt đơn cần chạy trên máy chủ: kiểm tra quyền theo vai trò, đối soát ca làm
/// việc và ghi CSDL bằng kết nối Postgres trực tiếp — những việc app không được
/// tự quyết.
/// </para>
/// <para>
/// Token gửi lên là access token do Supabase GoTrue cấp; Web Admin xác thực bằng
/// JWKS công khai của Supabase nên không cần chia sẻ mật khẩu giữa app và server.
/// Không fallback giả mạo kết quả khi API lỗi — người dùng phải biết là thao tác
/// chưa thành công.
/// </para>
/// </remarks>
public class LeaveApprovalService : ILeaveApprovalService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly ILogger<LeaveApprovalService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public bool IsDemoMode => false;

    public LeaveApprovalService(
        IAuthService authService,
        ILogger<LeaveApprovalService> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<LeaveRequestListResponse> GetLeaveRequestsAsync(string view = "pending", string type = "all")
    {
        var token = await _authService.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Không tìm thấy JWT token khi gọi GetLeaveRequestsAsync.");
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/mobile/leave-requests?view={view}&type={type}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<LeaveRequestListResponse>(json, _jsonOptions);
                return result ?? new LeaveRequestListResponse(new List<LeaveRequestListItemDto>(), 0, "production");
            }

            _logger.LogError("Lỗi tải danh sách đơn từ máy chủ: HTTP {StatusCode} - {Content}", response.StatusCode, json);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
            }
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException("Bạn không có thẩm quyền truy cập danh sách duyệt đơn.");
            }

            throw new HttpRequestException($"Lỗi máy chủ ({response.StatusCode}). Vui lòng thử lại sau.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối tới API WebAdmin tại {BaseUrl}", ApiConfig.BaseUrl);
            throw new HttpRequestException("Không thể kết nối máy chủ. Vui lòng kiểm tra kết nối mạng.", ex);
        }
    }

    public async Task<LeaveReviewResult> ReviewLeaveRequestAsync(int maDon, bool approved, string? phanHoi)
    {
        var token = await _authService.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return new LeaveReviewResult(false, "UNAUTHENTICATED", "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        }

        var payload = new LeaveReviewRequest(approved, phanHoi);
        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/mobile/leave-requests/{maDon}/review")
        {
            Content = jsonContent
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<LeaveReviewResult>(responseJson, _jsonOptions);
            if (result != null)
            {
                return result;
            }

            return response.IsSuccessStatusCode
                ? new LeaveReviewResult(true, "REVIEWED", approved ? "Phê duyệt đơn thành công." : "Đã từ chối đơn thành công.")
                : new LeaveReviewResult(false, response.StatusCode.ToString(), "Xử lý thất bại từ máy chủ.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi gửi yêu cầu review đơn #{MaDon}", maDon);
            return new LeaveReviewResult(false, "NETWORK_ERROR", "Không thể kết nối máy chủ để xử lý đơn. Vui lòng thử lại.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi review đơn #{MaDon}", maDon);
            return new LeaveReviewResult(false, "UNKNOWN_ERROR", "Đã xảy ra lỗi không xác định. Vui lòng thử lại.");
        }
    }

    public async Task<List<LeaveRequestItem>> GetPendingRequestsAsync(string role, int? maPhongBan)
    {
        var res = await GetLeaveRequestsAsync("pending", "all");
        return MapToItems(res.Items);
    }

    public async Task<List<LeaveRequestItem>> GetReviewedRequestsAsync(string role, int? maPhongBan)
    {
        var res = await GetLeaveRequestsAsync("reviewed", "all");
        return MapToItems(res.Items);
    }

    public async Task<int> GetPendingCountAsync(string role, int? maPhongBan)
    {
        var res = await GetLeaveRequestsAsync("pending", "all");
        return res.PendingCount;
    }

    public async Task<(bool Success, string Message)> ApproveAsync(int maDon)
    {
        var res = await ReviewLeaveRequestAsync(maDon, true, null);
        return (res.Success, res.Message);
    }

    public async Task<(bool Success, string Message)> RejectAsync(int maDon, string lyDoTuChoi)
    {
        var res = await ReviewLeaveRequestAsync(maDon, false, lyDoTuChoi);
        return (res.Success, res.Message);
    }

    private static List<LeaveRequestItem> MapToItems(List<LeaveRequestListItemDto> dtos)
    {
        return dtos.Select(d => new LeaveRequestItem
        {
            MaDon = d.MaDon,
            MaNhanVien = d.MaNhanVien,
            TenNhanVien = d.HoTenNhanVien,
            TenChucVu = d.ChucVu ?? "",
            MaPhongBan = d.MaPhongBan,
            TenPhongBan = d.TenPhongBan ?? "",
            MaLoaiDon = d.MaLoaiDon,
            TenLoaiDon = d.TenLoaiDon,
            CoHuongLuong = d.CoHuongLuong,
            TuNgay = d.TuNgay,
            DenNgay = d.DenNgay,
            LyDo = d.LyDo,
            NgayGui = d.NgayGui,
            TrangThai = d.TrangThai,
            PhanHoiDuyet = d.PhanHoiDuyet,
            NgayDuyet = d.NgayDuyet,
            NguoiDuyetTen = d.HoTenNguoiDuyet
        }).ToList();
    }
}
