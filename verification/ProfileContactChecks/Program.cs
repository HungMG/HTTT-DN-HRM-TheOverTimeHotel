using OvertimeHotel.HRM.MobileApp.Services;
if (ProfileContactValidation.Validate("0901234567", "person@example.com", "Hồ Chí Minh") != null) throw new Exception("valid contact rejected");
if (ProfileContactValidation.Validate("", "", "") == null) throw new Exception("empty phone accepted");
if (ProfileContactValidation.Validate("0901234567", "bad-email", "") == null) throw new Exception("invalid email accepted");
if (ProfileContactValidation.Validate(new string('1', 21), "", "") == null) throw new Exception("oversized phone accepted");
if (ProfileContactValidation.Validate("0901234567", "", new string('x', 256)) == null) throw new Exception("oversized address accepted");
Console.WriteLine("Profile contact validation passed");
