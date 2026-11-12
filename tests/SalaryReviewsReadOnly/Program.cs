using System.Text.Json;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Npgsql;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.Core.Policies;

if (!args.Contains("--live-readonly")) { Console.WriteLine("Pass --live-readonly explicitly; this check only runs SELECT in a read-only transaction."); return; }
string? cs=Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if(string.IsNullOrEmpty(cs))
 foreach(var file in new[]{"appsettings.json","appsettings.Development.json"})
 {
  using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine("src/OvertimeHotel.HRM.WebAdmin",file)),new JsonDocumentOptions {CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true});
  if(json.RootElement.TryGetProperty("ConnectionStrings",out var config)&&config.TryGetProperty("DefaultConnection",out var value))cs=value.GetString();
 }
try
{
 var counter=new QueryCounter();
 await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).AddInterceptors(counter).Options);
 await using var tx=await db.Database.BeginTransactionAsync();
 await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
 var http=new DefaultHttpContext();http.Request.Headers.XRequestedWith="XMLHttpRequest";
 var c=new HopDongController(db,NullLogger<HopDongController>.Instance){ControllerContext=new ControllerContext{HttpContext=http}};
 var clock=Stopwatch.StartNew();
 var result=await c.SalaryReviews();
 clock.Stop();
 if(result is not PartialViewResult {ViewName:"_SalaryReviewList",Model:IReadOnlyList<HopDongController.SalaryReviewItem> rows}) throw new Exception("Unexpected result");
 Console.WriteLine($"PASS live read-only review query: {rows.Count} review rows; AJAX list partial returned.");
 Console.WriteLine($"Review queries: {counter.Readers}; elapsed: {clock.Elapsed.TotalSeconds:F2}s");
 var sang=rows.FirstOrDefault(x=>x.ContractId==2);
 if(sang!=null)Console.WriteLine($"Contract #2 reference: {sang.Minimum?.ToString() ?? "unknown"} - {sang.Maximum?.ToString() ?? "unknown"}");
 if(args.Contains("--expect-resolved") && (sang?.Minimum!=9_000_000m || sang.Maximum!=11_000_000m || counter.Readers>4))throw new Exception("Reference or query budget regression");
 Console.WriteLine($"Rows requiring salary verification: {rows.Count(x=>x.Salary==null)}");
 // Audit the full active workforce, independently of tenure reminder eligibility.
 IReadOnlyList<HopDongController.SalaryReviewItem> auditRows=rows;
 if(args.Contains("--outside-band") || args.Contains("--unresolved"))
 {
  var today=DateOnly.FromDateTime(DateTime.Today);
  var contracts=await db.HopDongs.AsNoTracking().Include(x=>x.NhanVien).Include(x=>x.PhuLucLuongs)
   .Where(x=>x.TrangThai=="HIEU_LUC" && x.NgayBatDau<=today && (x.NgayKetThuc==null || x.NgayKetThuc>=today) && x.HinhThucHopDong!="THU_VIEC").ToListAsync();
  var groups=await db.ChucVus.AsNoTracking().ToDictionaryAsync(x=>x.MaChucVu,x=>x.NhomLuong);
  var bands=await db.KhungLuongCapBacs.AsNoTracking().ToListAsync();
  var audit=new List<HopDongController.SalaryReviewItem>();
  foreach(var contract in contracts)
  {
   var person=contract.NhanVien;
   if(person==null || person.TrangThai=="DA_THOI_VIEC")continue;
   var tenure=ReportPolicy.TenureMonths(person.NgayVaoLam,today);
   var position=person.MaChucVu;
   decimal? salary=null;
   var reason="";
   try
   {
    var effective=EffectiveSalaryResolver.Resolve(contracts.Where(x=>x.MaNhanVien==person.MaNhanVien),today);
    salary=effective?.MonthlySalary;
    position=effective?.PositionId ?? position;
   }
   catch(InvalidOperationException ex){reason=ex.Message;}
   var band=person.NgayVaoLam>today?null:SalaryBandLookup.Find(bands,position.HasValue?groups.GetValueOrDefault(position.Value):null,person.TrinhDo,tenure);
   audit.Add(new(contract.MaHopDong,person.HoTen,tenure,salary,band?.LuongToiThieu,band?.LuongToiDa,reason));
  }
  auditRows=audit;
  Console.WriteLine($"Full salary audit: {audit.Count} active official contracts; {contracts.Where(x=>x.NhanVien!=null && x.NhanVien.TrangThai!="DA_THOI_VIEC").Select(x=>x.MaNhanVien).Distinct().Count()} employees");
  var missing=audit.Where(x=>!x.Salary.HasValue).ToList();
  Console.WriteLine($"Missing or invalid salary/history: {missing.Count}");
  foreach(var row in missing)Console.WriteLine(JsonSerializer.Serialize(new{row.ContractId,row.Name,row.Minimum,row.Maximum,row.Reason},new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
 }
 if(args.Contains("--outside-band"))
 {
  var outside=auditRows.Where(x=>x.Salary.HasValue && x.Minimum.HasValue && x.Maximum.HasValue && (x.Salary<x.Minimum || x.Salary>x.Maximum)).ToList();
  Console.WriteLine($"Outside configured band: {outside.Count}");
  foreach(var row in outside)Console.WriteLine(JsonSerializer.Serialize(new{row.ContractId,row.Name,row.Salary,row.Minimum,row.Maximum},new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
  if(args.Contains("--write-preview"))
  {
   var culture=System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
   string Money(decimal value)=>value.ToString("N0",culture);
   var text=new System.Text.StringBuilder("# Phương án đối chiếu lương cũ với khung mới\n\nChỉ là bản xem trước; chưa cập nhật mức lương nào. Phương án: lương dưới tối thiểu đưa lên tối thiểu, lương trên tối đa đưa về tối đa; giữ các mức đang trong khung. Chưa quyết định sửa trực tiếp hay lập phụ lục và ngày hiệu lực.\n\n| Hợp đồng | Nhân viên | Lương hiện tại (đ) | Khung hiện tại (đ) | Mức xem trước (đ) |\n|---|---|---:|---|---:|\n");
   foreach(var row in outside)
   {
    var proposed=Math.Clamp(row.Salary!.Value,row.Minimum!.Value,row.Maximum!.Value);
    text.AppendLine($"| #{row.ContractId} | {row.Name.Replace("|","/").Replace("\n"," ")} | {Money(row.Salary.Value)} | {Money(row.Minimum.Value)}–{Money(row.Maximum.Value)} | {Money(proposed)} |");
   }
   text.AppendLine("\nCác hồ sơ thiếu mức lương hợp lệ (như lương bằng 0) cần xác nhận riêng, chưa được đưa vào phương án này. Khung phải được tra lại tại ngày hiệu lực nếu chọn lập phụ lục. Không đổi chức vụ hoặc bằng cấp để làm khớp lương.");
   var path="/home/bryan/.cache/hrm-operations/salary-alignment-preview.md";
   Directory.CreateDirectory(Path.GetDirectoryName(path)!);
   File.WriteAllText(path,text.ToString());
   if(!OperatingSystem.IsWindows())File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);
   Console.WriteLine("Preview saved: "+path);
  }

 }
 if(args.Contains("--unresolved"))
 {
  var unresolved=auditRows.Where(x=>x.Minimum==null).ToList();
  var ids=unresolved.Select(x=>x.ContractId).ToArray();
  var details=await db.HopDongs.AsNoTracking().Include(x=>x.NhanVien).Include(x=>x.PhuLucLuongs).Where(x=>ids.Contains(x.MaHopDong)).ToDictionaryAsync(x=>x.MaHopDong);
  var positions=await db.ChucVus.AsNoTracking().ToDictionaryAsync(x=>x.MaChucVu);
  Console.WriteLine($"Unresolved references: {unresolved.Count}");
  foreach(var row in unresolved)
  {
   var contract=details[row.ContractId]; var person=contract.NhanVien!;
   int? pos=person.MaChucVu;
   try{pos=EffectiveSalaryResolver.Resolve(new[]{contract},DateOnly.FromDateTime(DateTime.Today))?.PositionId ?? pos;}catch(InvalidOperationException){}
   var position=pos.HasValue?positions.GetValueOrDefault(pos.Value):null;
   Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {row.ContractId,row.Name,Education=person.TrinhDo,Grade=SalaryBandLookup.EducationGrade(person.TrinhDo),Position=position?.TenChucVu,Group=position?.NhomLuong,row.Salary,row.Reason},new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
  }
 }
 if(args.Contains("--inspect-indexes"))
 {
  var connection=(NpgsqlConnection)db.Database.GetDbConnection();
  var transaction=(NpgsqlTransaction)tx.GetDbTransaction();
  foreach(var table in new[]{"hop_dong","nhan_vien","phu_luc_luong","khung_luong_cap_bac","chuc_vu"})
  {
   await using var count=new NpgsqlCommand("SELECT count(*) FROM public."+table,connection,transaction);
   Console.WriteLine($"Rows {table}: {await count.ExecuteScalarAsync()}");
  }
  await using(var indexes=new NpgsqlCommand("SELECT tablename,indexname,indexdef FROM pg_indexes WHERE schemaname='public' AND tablename IN ('hop_dong','nhan_vien','phu_luc_luong','khung_luong_cap_bac','chuc_vu') ORDER BY tablename,indexname",connection,transaction))
  await using(var reader=await indexes.ExecuteReaderAsync())
   while(await reader.ReadAsync())Console.WriteLine($"INDEX {reader.GetString(0)}: {reader.GetString(1)} | {reader.GetString(2)}");
  var number=0;
  foreach(var snapshot in counter.Snapshots.ToArray())
  {
   await using var explain=new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) "+snapshot.Sql,connection,transaction);
   foreach(var parameter in snapshot.Parameters)explain.Parameters.Add(new NpgsqlParameter(parameter.ParameterName,parameter.NpgsqlDbType){Value=parameter.Value});
   using var plan=JsonDocument.Parse((string)(await explain.ExecuteScalarAsync())!);
   var summary=plan.RootElement[0];
   Console.WriteLine($"PLAN {++number}: execution {summary.GetProperty("Execution Time")} ms; planning {summary.GetProperty("Planning Time")} ms; rows {summary.GetProperty("Plan").GetProperty("Actual Rows")}");
   void Nodes(JsonElement node)
   {
    Console.WriteLine("  "+node.GetProperty("Node Type").GetString()+(node.TryGetProperty("Relation Name",out var relation)?" "+relation.GetString():"")+(node.TryGetProperty("Index Name",out var index)?" via "+index.GetString():""));
    if(node.TryGetProperty("Plans",out var children))foreach(var child in children.EnumerateArray())Nodes(child);
   }
   Nodes(summary.GetProperty("Plan"));
  }
 }
 await tx.RollbackAsync();
}
catch(Exception e) {Console.WriteLine("FAIL read-only review query: "+e.GetType().Name);Environment.ExitCode=1;}

sealed class QueryCounter : DbCommandInterceptor
{
 public int Readers;
 public List<(string Sql, NpgsqlParameter[] Parameters)> Snapshots { get; } = new();
 public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken=default)
 {
  Interlocked.Increment(ref Readers);
  Snapshots.Add((command.CommandText,command.Parameters.Cast<NpgsqlParameter>().Select(p=>new NpgsqlParameter(p.ParameterName,p.NpgsqlDbType){Value=p.Value}).ToArray()));
  return ValueTask.FromResult(result);
 }
}
