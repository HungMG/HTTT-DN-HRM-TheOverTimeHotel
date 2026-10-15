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
    Task RemoveAssignmentAsync(EventActor actor, int id, int assignmentId, long version);
    Task ReviewAsync(EventActor actor, int id, EventReviewInput input);
    Task ChangeStatusAsync(EventActor actor, int id, long version, string status, string? reason = null);
    Task AcceptAsync(EventActor actor, int id, long version);
}
