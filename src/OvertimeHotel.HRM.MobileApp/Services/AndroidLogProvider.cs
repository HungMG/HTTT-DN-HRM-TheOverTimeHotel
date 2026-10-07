// Gọi đủ tên định kỳ `Android.Util.Log` chứ không `using Android.Util;`:
// bên trong lớp này có method `ILogger.Log`, nên tên `Log` trần sẽ bị method
// đó che mất (CS0119). Tên định kỳ thì vẫn dẫn tới đúng kiểu của Android.
//
// Lưu ý tên hàm là `WriteLine` (không phải `Write` — kiểu này không hề có).
using Microsoft.Extensions.Logging;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Ghi log của .NET xuống logcat của Android bằng thẻ <c>HRM</c>.
///
/// <para>
/// <b>Vì sao cần</b> — <c>logging.AddDebug()</c> mà Microsoft khuyên dùng gọi
/// <c>System.Diagnostics.Debug.WriteLine</c>, mà hàm đó bị compiler cắt rỗng
/// hoàn toàn trong bản Release ([Conditional("DEBUG")]). Hệ quả: đúng bản Release
/// chạy thật trên emulator thì mọi <c>_logger.LogError</c> biến mất, nên app
/// chỉ hiện "Đã xảy ra lỗi trong quá trình đăng nhập" mà không ai biết nguyên
/// nhân. Provider này nối thẳng vào <c>Android.Util.Log</c> nên vẫn sống trong
/// Release.
/// </para>
///
/// <para>
/// <b>Ràng buộc</b> — chỉ ghi mức Debug trở lên. Provider không tự quyết định
/// việc lọc: <c>LoggerFactory</c> đã lọc sẵn theo mức tối thiểu trước khi gọi
/// tới đây. Nội dung ghi ra chỉ là thông báo chẩn đoán, không ghi mật khẩu,
/// access token hay refresh token.
/// </para>
/// </summary>
public sealed class AndroidLogProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new AndroidLogger(categoryName);

    public void Dispose()
    {
        // Provider không giữ tài nguyên nào (không mở file, không đăng ký handler).
    }

    private sealed class AndroidLogger : ILogger
    {
        private readonly string _tag;

        public AndroidLogger(string categoryName)
        {
            // Logcat giới hạn tag 23 ký tự, categoryName của .NET thường dài hơn
            // nhiều (kèm namespace). Cắt phần đuôi để nhìn log không bị cắt cụt.
            const int MaxTagLength = 23;
            var name = categoryName.Split('.').LastOrDefault() ?? categoryName;
            _tag = name.Length <= MaxTagLength ? name : name[..MaxTagLength];
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);

            // Nối exception vào cùng dòng: logcat hiển thị theo từng dòng nên nếu in
            // riêng, phần stack trace quan trọng nhất sẽ bị tách rời khỏi lỗi gốc.
            if (exception is not null)
            {
                message = $"{message} | {exception.GetType().Name}: {exception.Message}";
            }

            #if ANDROID
            var priority = logLevel switch
            {
                LogLevel.Trace or LogLevel.Debug => Android.Util.LogPriority.Debug,
                LogLevel.Information => Android.Util.LogPriority.Info,
                LogLevel.Warning => Android.Util.LogPriority.Warn,
                _ => Android.Util.LogPriority.Error,
            };

            Android.Util.Log.WriteLine(priority, _tag, message);
#else
            // Ngoài Android không có logcat (và Mono.Android.dll không được tham
            // chiếu nên không bind được ở trên). Rơi về Debug.WriteLine để bản
            // Windows/iOS vẫn thấy log ở cửa sổ Output của Visual Studio.
            System.Diagnostics.Debug.WriteLine($"[{_tag}] {message}");
#endif
        }
    }
}