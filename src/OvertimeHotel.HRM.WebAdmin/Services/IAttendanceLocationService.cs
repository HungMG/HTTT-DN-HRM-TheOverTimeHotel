using OvertimeHotel.HRM.WebAdmin.Models;

namespace OvertimeHotel.HRM.WebAdmin.Services;

public interface IAttendanceLocationService
{
    Task<AttendanceLocationViewModel> GetLocationConfigAsync();
    Task<bool> UpdateLocationConfigAsync(AttendanceLocationViewModel model, string updatedBy, int? accountId, string ipAddress);
}
