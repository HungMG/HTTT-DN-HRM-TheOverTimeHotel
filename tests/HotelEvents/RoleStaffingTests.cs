using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;

static class RoleStaffingTests
{
    private static readonly DateTimeOffset Start = new(2028, 3, 1, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddHours(4);

    public static int Run()
    {
        var failed = 0;
        var tests = new (string Name, Action Run)[]
        {
            ("All 27 department catalogs scale primary roles and require one support", Catalog),
            ("Roles are scoped and custom names never satisfy mandatory roles", RoleValidation),
            ("Surplus primary or optional people cannot replace missing support", MissingSupport),
            ("Adjacent shifts cover a role but gaps and duplicate employees do not", Coverage),
            ("Guest increases and zero guests cannot report fully staffed", Scale),
            ("Legacy assignments remain visible without silently gaining a required role", Legacy),
            ("Ongoing removal needs replacement only for remaining time", Removal)
        };
        foreach (var test in tests)
        {
            try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
            catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
        }
        return failed;
    }

    private static void Catalog()
    {
        var capacities = new (string Type, string Dept, int K)[]
        {
            ("TIEC_CUOI","TIEN_SANH",100), ("TIEC_CUOI","AM_THUC",20), ("TIEC_CUOI","BEP",50),
            ("TIEC_CUOI","BUONG_PHONG",100), ("TIEC_CUOI","KY_THUAT",200), ("TIEC_CUOI","AN_NINH",100),
            ("HOI_NGHI_MICE","TIEN_SANH",75), ("HOI_NGHI_MICE","AM_THUC",40), ("HOI_NGHI_MICE","BEP",80),
            ("HOI_NGHI_MICE","KY_THUAT",100), ("HOI_NGHI_MICE","AN_NINH",100), ("HOI_NGHI_MICE","IT",100),
            ("GALA_DINNER","TIEN_SANH",100), ("GALA_DINNER","AM_THUC",20), ("GALA_DINNER","BEP",50),
            ("GALA_DINNER","KY_THUAT",100), ("GALA_DINNER","AN_NINH",100),
            ("FESTIVAL_AM_THUC","AM_THUC",25), ("FESTIVAL_AM_THUC","BEP",30),
            ("FESTIVAL_AM_THUC","BUONG_PHONG",100), ("FESTIVAL_AM_THUC","KY_THUAT",150), ("FESTIVAL_AM_THUC","AN_NINH",100),
            ("DOAN_VIP","TIEN_SANH",20), ("DOAN_VIP","AM_THUC",10), ("DOAN_VIP","BEP",25),
            ("DOAN_VIP","BUONG_PHONG",15), ("DOAN_VIP","AN_NINH",20)
        };
        foreach (var (type, dept, k) in capacities)
        {
            var atBoundary = HotelEventRoleRules.GetRoles(type, dept, k);
            Equal(3, atBoundary.Count);
            Equal(1, atBoundary[0].RequiredCount);
            Equal(1, atBoundary[1].RequiredCount);
            Equal(false, atBoundary[2].IsRequired);
            Equal(0, atBoundary[2].RequiredCount);
            Equal(2, HotelEventRoleRules.GetRoles(type, dept, k + 1)[0].RequiredCount);
        }
        Equal(107374183, HotelEventRoleRules.GetRoles("TIEC_CUOI", "AM_THUC", int.MaxValue)[0].RequiredCount);
        Equal(0, HotelEventRoleRules.GetRoles("TIEC_CUOI", "IT", 100).Count);
        Equal(0, HotelEventRoleRules.GetRoles("INVALID", "AM_THUC", 100).Count);
    }

    private static void RoleValidation()
    {
        Equal("Phục vụ bàn tiệc", HotelEventRoleRules.ResolveRole("TIEC_CUOI", "AM_THUC", " phục vụ bàn tiệc ", null));
        Equal<string?>(null, HotelEventRoleRules.ResolveRole("TIEC_CUOI", "BEP", "Phục vụ bàn tiệc", null));
        Equal<string?>(null, HotelEventRoleRules.ResolveRole("TIEC_CUOI", "AM_THUC", HotelEventRoleRules.OtherRoleValue, " "));
        Equal<string?>(null, HotelEventRoleRules.ResolveRole("TIEC_CUOI", "AM_THUC", HotelEventRoleRules.OtherRoleValue, new string('a',100)));
        Equal("Khác: Phục vụ bàn tiệc", HotelEventRoleRules.ResolveRole("TIEC_CUOI", "AM_THUC", HotelEventRoleRules.OtherRoleValue, "Phục vụ bàn tiệc"));
    }

    private static void MissingSupport()
    {
        var e = Event(100);
        for (var id=1; id<=6; id++) e.PhanCongs.Add(Member(id,"Phục vụ bàn tiệc"));
        e.PhanCongs.Add(Member(7,"Điều phối phục vụ"));
        var dept = Department(e);
        Equal(7, dept.AssignedCount);
        Equal(false, dept.IsSufficient);
        Equal(1, dept.Roles.Single(r=>r.Name=="Phục vụ đồ uống").MissingCount);
        Equal(0, dept.Roles.Single(r=>r.Name=="Phục vụ bàn tiệc").MissingCount);
        e.PhanCongs.Add(Member(8,"Phục vụ đồ uống"));
        Equal(true, Department(e).IsSufficient);
    }

    private static void Coverage()
    {
        var e = Event(20);
        e.PhanCongs.Add(Member(1,"Phục vụ bàn tiệc", Start, Start.AddHours(2)));
        e.PhanCongs.Add(Member(2,"Phục vụ bàn tiệc", Start.AddHours(2), End));
        e.PhanCongs.Add(Member(3,"Phục vụ đồ uống"));
        Equal(true, Department(e).IsSufficient);
        e.PhanCongs.Single(p=>p.MaNhanVien==2).BatDau=Start.AddHours(2).AddMinutes(1);
        Equal(false, Department(e).IsSufficient);
        e=Event(21);
        e.PhanCongs.Add(Member(1,"Phục vụ bàn tiệc"));
        e.PhanCongs.Add(Member(1,"Phục vụ bàn tiệc"));
        e.PhanCongs.Add(Member(3,"Phục vụ đồ uống"));
        Equal(1, Department(e).Roles[0].MinimumConcurrentCount);
        Equal(false, Department(e).IsSufficient);
    }

    private static void Scale()
    {
        var e=Event(20);
        e.PhanCongs.Add(Member(1,"Phục vụ bàn tiệc"));
        e.PhanCongs.Add(Member(2,"Phục vụ đồ uống"));
        Equal(true, Department(e).IsSufficient);
        e.SoKhachDuKien=21;
        Equal(false, Department(e).IsSufficient);
        e.SoKhachDuKien=0;
        Equal(false, HotelEventRules.GetStaffingSummary(e).IsFullyStaffed);
    }

    private static void Legacy()
    {
        var e=Event(20);
        var old=Member(1,"Support"); old.NhiemVuCuThe="Legacy task";
        e.PhanCongs.Add(old);
        e.PhanCongs.Add(Member(2,"Khác: Phục vụ đồ uống"));
        var d=Department(e);
        Equal(2,d.Assignments.Count);
        Equal(false,d.IsSufficient);
        Equal("Support",old.VaiTroDuAn);
        Equal("Legacy task",old.NhiemVuCuThe);
    }

    private static void Removal()
    {
        var e=Event(20);
        e.PhanCongs.Add(Member(1,"Phục vụ bàn tiệc"));
        e.PhanCongs.Add(Member(2,"Phục vụ đồ uống"));
        Equal(false, HotelEventRoleRules.CanRemove(e,1,Start.AddHours(1)));
        e.PhanCongs.Add(Member(3,"Phục vụ bàn tiệc",Start.AddHours(1),End));
        Equal(true, HotelEventRoleRules.CanRemove(e,1,Start.AddHours(1)));
        Equal(false, HotelEventRoleRules.CanRemove(e,1,Start.AddMinutes(59)));
        e.PhanCongs.Add(Member(4,"Khác: Hỗ trợ"));
        Equal(true,HotelEventRoleRules.CanRemove(e,4,Start.AddHours(1)));
        var alreadyGapped = Event(20);
        alreadyGapped.PhanCongs.Add(Member(1,"Phục vụ bàn tiệc",Start,Start.AddHours(2)));
        alreadyGapped.PhanCongs.Add(Member(2,"Phục vụ đồ uống"));
        Equal(false,HotelEventRoleRules.CanRemove(alreadyGapped,1,Start.AddHours(1)));
    }

    private static DuAn Event(int guests) => new() { LoaiDuAn="TIEC_CUOI", SoKhachDuKien=guests, NgayBatDau=Start, NgayKetThuc=End };
    private static PhanCongDuAn Member(int id,string role,DateTimeOffset? start=null,DateTimeOffset? end=null) => new()
    { MaPhanCong=id, MaNhanVien=id, VaiTroDuAn=role, BatDau=start??Start, KetThuc=end??End, NhanVien=new NhanVien { MaPhongBan=4 } };
    private static EventDepartmentStatus Department(DuAn e) => HotelEventRules.GetStaffingSummary(e).Departments.Single(d=>d.Code=="AM_THUC");
    private static void Equal<T>(T expected,T actual)
    { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}, got {actual}"); }
}
