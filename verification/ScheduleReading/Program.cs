using System.Text.Json;
using System.Text.Json.Nodes;
using OvertimeHotel.HRM.MobileApp.Services;

var checks = 0;
JsonObject Row() => JsonNode.Parse("""
{"ma_phan_ca":1867,"ma_nhan_vien":1,"ma_ca":1,"ngay_lam_viec":"2026-11-09",
 "bat_dau_du_kien":"-infinity","ket_thuc_du_kien":"-infinity","trang_thai":"DA_PHAN",
 "ca_lam_viec":{"ten_ca":"Ca sáng","gio_bat_dau":"06:00:00","gio_ket_thuc":"14:00:00","qua_dem":false},"cham_cong":null}
""")!.AsObject();
OvertimeHotel.HRM.Core.Models.PhanCa Read(JsonObject row) =>
    AttendanceSummaryPostgrstMapper.ToShift(JsonSerializer.SerializeToElement(row));
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
void Reject(JsonObject row, string message)
{
    try { Read(row); } catch (JsonException) { checks++; return; }
    throw new Exception(message);
}
var row = Row();
var shift = Read(row);
Check(shift.BatDauDuKien == DateTimeOffset.Parse("2026-11-08T23:00:00Z"), "Legacy morning start must use Vietnam timezone");
Check(shift.KetThucDuKien == DateTimeOffset.Parse("2026-11-09T07:00:00Z"), "Legacy morning end must use its assigned template");
row["ca_lam_viec"]!["gio_bat_dau"] = "22:00:00";
row["ca_lam_viec"]!["gio_ket_thuc"] = "06:00:00";
row["ca_lam_viec"]!["qua_dem"] = true;
shift = Read(row);
Check(shift.KetThucDuKien - shift.BatDauDuKien == TimeSpan.FromHours(8), "Night shifts must end on the next local day");
Check(shift.KetThucDuKien == DateTimeOffset.Parse("2026-11-09T23:00:00Z"), "Night end must retain the correct UTC date");
row = Row();
row["bat_dau_du_kien"] = "2026-11-09T01:30:00Z";
row["ket_thuc_du_kien"] = "2026-11-09T05:00:00Z";
shift = Read(row);
Check(shift.BatDauDuKien == DateTimeOffset.Parse("2026-11-09T01:30:00Z") && shift.KetThucDuKien == DateTimeOffset.Parse("2026-11-09T05:00:00Z"), "Explicit event or customized hours must never be replaced by template hours");
row = Row(); row["bat_dau_du_kien"] = null; row["ket_thuc_du_kien"] = null;
Check(Read(row).KetThucDuKien - Read(row).BatDauDuKien == TimeSpan.FromHours(8), "Missing planned hours can use a valid template");
row = Row(); row["ca_lam_viec"] = null;
Reject(row, "A missing template must not invent a schedule");
row = Row(); row["bat_dau_du_kien"] = "invalid-timestamp";
Reject(row, "Unexpected timestamp corruption must be reported");
row = Row(); row["cham_cong"] = JsonNode.Parse("""{"ma_cham_cong":1,"gio_check_in":"-infinity","gio_check_out":null,"trang_thai":"DI_LAM"}""");
Reject(row, "Actual attendance times must never be synthesized from planned hours");
Console.WriteLine($"PASS: {checks} schedule reading checks");
