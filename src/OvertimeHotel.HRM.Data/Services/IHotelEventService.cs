using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Data.Services;

public interface IHotelEventService
{
    Task<IReadOnlyList<DuAn>> ListAsync(EventActor actor, DateTimeOffset from, DateTimeOffset to);
    Task<DuAn?> GetAsync(EventActor actor, int id);
    Task<IReadOnlyList<NhanVien>> ManagersAsync();
    Task<IReadOnlyList<NhanVien>> EmployeesAsync();
    Task<IReadOnlyList<PhanCa>> ShiftsAsync(DuAn item);
    Task<IReadOnlyList<NhatKyQuanTri>> HistoryAsync(int id);
    Task<int> SaveAsync(EventActor actor, EventInput input);
    Task AssignAsync(EventActor actor, int id, EventAssignmentInput input);
    Task AssignManyAsync(EventActor actor, int id, long version, IReadOnlyList<EventAssignmentInput> inputs);
    Task RemoveAssignmentAsync(EventActor actor, int id, int assignmentId, long version);
    Task ReviewAsync(EventActor actor, int id, EventReviewInput input);
    Task ChangeStatusAsync(EventActor actor, int id, long version, string status, string? reason = null);
    Task AcceptAsync(EventActor actor, int id, long version);
    Task AcceptHourlyAsync(EventActor actor, int id, long version, int paymentPeriodId, string previewToken);
    Task<EventPayPreview> PreviewAsync(EventActor actor, int id);
    Task SaveNeedAsync(EventActor actor, int id, EventNeedInput input);
    Task DeleteNeedAsync(EventActor actor, int id, int needId, long version, string reason);
    Task RecordWorkAsync(EventActor actor, int id, EventWorkInput input);
    Task DeleteWorkAsync(EventActor actor, int id, int workId, long version, string reason);
    Task EndParticipationAsync(EventActor actor, int id, int assignmentId, long version, DateTime end, string reason);
    Task StopAsync(EventActor actor, int id, long version, DateTime end, string reason);
    Task<IReadOnlyList<PhanCa>> AvailableShiftsAsync(EventActor actor, int id);
    Task<IReadOnlyList<KyLuong>> PaymentPeriodsAsync(EventActor actor);
    Task<int> EnsurePaymentPeriodAsync(EventActor actor, int month, int year);
    Task ReconcileLegacyAsync(EventActor actor, int id, long version, int paymentPeriodId, string reason);
    Task<IReadOnlyList<DiaDiemSuKien>> VenuesAsync();
    Task<int> SaveVenueAsync(EventActor actor, string name, int capacity);
    Task PublishWeekAsync(EventActor actor, DateOnly weekStart, long version, string? reason);
    Task<CongBoLichTuan?> PublishedWeekAsync(DateOnly weekStart);
    Task<int> AutoCancelExpiredEventsAsync();
}
