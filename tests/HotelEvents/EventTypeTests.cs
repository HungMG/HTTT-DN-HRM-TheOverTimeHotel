using OvertimeHotel.HRM.Core.Policies;

static class EventTypeTests
{
    public static int Run()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Event labels include birthday year-end and press events while preserving history", () =>
            {
                Require(HotelEventRules.TypeLabels.GetValueOrDefault("HOI_NGHI_MICE") == "Hội nghị", "Conference label is not updated.");
                foreach (var (code, label) in new[] { ("TIEC_SINH_NHAT", "Tiệc sinh nhật"), ("TIEC_TAT_NIEN", "Tiệc tất niên"), ("HOP_BAO", "Họp báo"), ("GALA_DINNER", "Gala Dinner"), ("DOAN_VIP", "Đoàn VIP") })
                    Require(HotelEventRules.TypeLabels.GetValueOrDefault(code) == label, "Missing event label: " + code);
            }),
            ("New party roles scale guest capacity without requiring every support department", () =>
            {
                foreach (var type in new[] { "TIEC_SINH_NHAT", "TIEC_TAT_NIEN" })
                {
                    var roles = HotelEventRoleRules.GetRoles(type, "AM_THUC", 20);
                    Require(roles.Count == 3 && roles[0].IsRequired && roles[0].RequiredCount == 1, "Missing scaled party service role.");
                    Require(HotelEventRoleRules.GetRoles(type, "AM_THUC", 21)[0].RequiredCount == 2, "Party staffing does not round guests up.");
                    Require(roles[1].IsRequired && roles[1].RequiredCount == 1 && !roles[2].IsRequired, "Party drinks and optional support are not separated.");
                    var housekeeping = HotelEventRoleRules.GetRoles(type, "BUONG_PHONG", 100);
                    Require(housekeeping.Count > 0 && housekeeping.All(r => !r.IsRequired && r.RequiredCount == 0), "Housekeeping must be available without mandatory staffing.");
                }
            }),
            ("Press events require reception technical network and security but catering is optional", () =>
            {
                foreach (var department in new[] { "TIEN_SANH", "KY_THUAT", "IT", "AN_NINH" })
                    Require(HotelEventRoleRules.GetRoles("HOP_BAO", department, 40).Any(r => r.IsRequired && r.RequiredCount > 0), "Missing required press department " + department);
                foreach (var department in new[] { "AM_THUC", "BEP", "BUONG_PHONG" })
                {
                    var roles = HotelEventRoleRules.GetRoles("HOP_BAO", department, 40);
                    Require(roles.Count > 0 && roles.All(r => !r.IsRequired && r.RequiredCount == 0), "Press event requires catering or housekeeping.");
                    Require(HotelEventRoleRules.ResolveRole("HOP_BAO", department, roles[0].Name, null) == roles[0].Name, "HR cannot add optional press support.");
                }
            })
        };
        var failed = 0;
        foreach (var test in tests)
        {
            try { test.Run(); Console.WriteLine("PASS " + test.Name); }
            catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + test.Name + ": " + ex.Message); }
        }
        return failed;
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
