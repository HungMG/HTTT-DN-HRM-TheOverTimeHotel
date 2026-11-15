using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Models;
// Platform substitutes: the real Supabase RPC service and ViewModels are compiled unchanged.
public class Command(Action action) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action();
}
public class Command<T>(Action<T> action) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action((T)parameter!);
}
public class Shell
{
    public static Shell Current { get; } = new();
    public Task GoToAsync(string route) => Task.CompletedTask;
}
namespace OvertimeHotel.HRM.MobileApp.Models
{
    public class DepartmentFilterChip { public string Name { get; set; } = ""; public bool IsSelected { get; set; } }
}
namespace OvertimeHotel.HRM.MobileApp.Services
{
    public sealed class SupabaseRestClient { public Task<string> GetAsync(string path) => throw new NotSupportedException(); }
    public interface IAuthService { UserSession? CurrentUser { get; } Task<string?> GetTokenAsync(); }
    public interface ILeaveApprovalService { Task<int> GetPendingCountAsync(string role, int? department); }
}
