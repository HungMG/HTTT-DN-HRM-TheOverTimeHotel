namespace OvertimeHotel.HRM.Data.Services;

public interface IBackupService
{
    Task<string> ExportDatabaseToJsonAsync();
    Task<(bool Success, string Message, int RestoredRecords)> RestoreDatabaseFromJsonAsync(string jsonContent);
    Task<Dictionary<string, int>> GetTableStatisticsAsync();
}
