using System.Windows.Input;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.MobileApp.Services;
using OvertimeHotel.HRM.MobileApp.ViewModels;

// 17:00 UTC is midnight in Vietnam, even when the host still sees Nov 5.
var beforeMidnight = DateTimeOffset.Parse("2026-11-05T16:59:59Z");
var midnight = DateTimeOffset.Parse("2026-11-05T17:00:00Z");
var nov5 = new DateOnly(2026, 11, 5);
var nov6 = new DateOnly(2026, 11, 6);
var nov7 = new DateOnly(2026, 11, 7);
Check(!ShiftSwapPolicy.ValidateDates(nov5, nov6, beforeMidnight).IsValid, "Today sender is rejected");
Check(!ShiftSwapPolicy.ValidateDates(nov6, nov5, beforeMidnight).IsValid, "Today receiver is rejected");
Check(ShiftSwapPolicy.ValidateDates(nov6, nov6, beforeMidnight).IsValid, "Tomorrow is allowed before Vietnam midnight");
Check(!ShiftSwapPolicy.ValidateDates(nov6, nov7, midnight).IsValid, "Sender becomes ineligible at Vietnam midnight");
Check(!ShiftSwapPolicy.ValidateDates(nov7, nov6, midnight).IsValid, "Receiver becomes ineligible at Vietnam midnight");
Check(ShiftSwapPolicy.ValidateDates(nov7, new DateOnly(2026, 12, 6), midnight).IsValid, "Tomorrow through exactly +30 days is allowed");
Check(!ShiftSwapPolicy.ValidateDates(new DateOnly(2026, 12, 7), nov7, midnight).IsValid, "Sender +31 is rejected");
Check(!ShiftSwapPolicy.ValidateDates(nov7, new DateOnly(2026, 12, 7), midnight).IsValid, "Receiver +31 is rejected");
Check(!ShiftSwapPolicy.ValidateDates(nov5, nov7, midnight).IsValid, "Past work date is rejected");
Console.WriteLine("PASS: date window for both shifts, Vietnam midnight, tomorrow, +30/+31, past date");

var service = new FixtureService();
var vm = new ShiftSwapViewModel(service);
await vm.LoadAsync();
Check(!vm.HasError && vm.EligiblePartnerShifts.Select(s => s.MaPhanCa).SequenceEqual([101]), "Initial sender filters partners");
vm.LyDo = "Đổi ca";
Check(vm.CanSubmit, "Valid selected pair can submit");
vm.SelectedMyShift = vm.EligibleMyShifts[1];
Check(vm.EligiblePartnerShifts.Select(s => s.MaPhanCa).SequenceEqual([102]) && vm.SelectedPartnerShift?.MaPhanCa == 102, "Changing sender removes stale selected partner");
vm.SelectedMyShift = null;
Check(vm.EligiblePartnerShifts.Count == 0 && vm.SelectedPartnerShift == null && !vm.CanSubmit, "No sender clears all partners and disables submit");
service.NoPartners = true;
await vm.LoadAsync();
Check(!vm.HasError && vm.SelectedPartnerShift == null && !vm.CanSubmit, "No compatible options must not retain a stale selection");
Console.WriteLine("PASS: sender changes, compatible options, selection reset, empty results, submit state");
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

sealed class FixtureService : IShiftSwapService
{
    public bool IsDemoMode => false;
    public bool NoPartners { get; set; }
    public Task<List<EligibleMyShiftDto>> GetEligibleMyShiftsAsync(CancellationToken ct = default) => Task.FromResult(new List<EligibleMyShiftDto> { new() { MaPhanCa = 1 }, new() { MaPhanCa = 2 } });
    public Task<List<EligiblePartnerShiftDto>> GetEligiblePartnerShiftsAsync(CancellationToken ct = default) => Task.FromResult(NoPartners ? new List<EligiblePartnerShiftDto>() : [new() { MaPhanCa = 101, CompatibleSenderShiftIds = [1] }, new() { MaPhanCa = 102, CompatibleSenderShiftIds = [2] }]);
    public Task<List<ShiftSwapItemDto>> GetMyShiftSwapsAsync(CancellationToken ct = default) => Task.FromResult(new List<ShiftSwapItemDto>());
    public Task<ShiftSwapItemDto?> GetShiftSwapDetailAsync(int id, CancellationToken ct = default) => Task.FromResult<ShiftSwapItemDto?>(null);
    public Task<ShiftSwapOperationResult> CreateShiftSwapAsync(CreateShiftSwapRequest r) => throw new NotSupportedException();
    public Task<ShiftSwapOperationResult> RespondShiftSwapAsync(int id, RespondShiftSwapRequest r) => throw new NotSupportedException();
    public Task<ShiftSwapOperationResult> WithdrawShiftSwapAsync(int id) => throw new NotSupportedException();
}

// Only MAUI commands/navigation are stubbed; the actual production ViewModel is linked.
public class Command(Action action, Func<bool>? can = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public virtual bool CanExecute(object? p) => can?.Invoke() ?? true;
    public virtual void Execute(object? p) => action();
    public void ChangeCanExecute() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
public sealed class Command<T>(Action<T> action, Func<T,bool>? can = null) : Command(() => { })
{
    public override bool CanExecute(object? p) => can?.Invoke((T)p!) ?? true;
    public override void Execute(object? p) => action((T)p!);
}
public sealed class Shell
{
    public static Shell Current { get; } = new();
    public Task GoToAsync(string route) => Task.CompletedTask;
}
