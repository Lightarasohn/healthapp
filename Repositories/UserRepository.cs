using healthapp.Context;
using healthapp.DTOs;
using healthapp.DTOs.UserDTOs;
using healthapp.Interfaces;
using healthapp.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace healthapp.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly PostgresContext _context;
        private readonly IPasswordService _passwordService;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;

        public UserRepository(PostgresContext context, IPasswordService passwordService, ITokenService tokenService, IConfiguration config, IEmailService emailService)
        {
            _context = context;
            _passwordService = passwordService;
            _tokenService = tokenService;
            _config = config;
            _emailService = emailService;
        }

        public async Task<ApiResponse<object>> ChangePasswordAsync(int userId, ChangePasswordDto dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return new ApiResponse<object>(404, "Kullanıcı bulunamadı.");

            if (!_passwordService.VerifyPassword(dto.CurrentPassword, user.PasswordHash))
                return new ApiResponse<object>(400, "Mevcut şifre yanlış.");

            user.PasswordHash = _passwordService.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();

            return new ApiResponse<object>(200, "Şifre başarıyla değiştirildi.");
        }

        public async Task<ApiResponse<object>> ResendVerificationEmailAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return new ApiResponse<object>(404, "Kullanıcı bulunamadı.");
            if (user.IsVerified == true) return new ApiResponse<object>(400, "Hesap zaten doğrulanmış.");

            // Yeni token oluştur
            user.VerificationToken = Guid.NewGuid().ToString("N");
            await _context.SaveChangesAsync();

            var verificationLink = $"{_config["CLIENT_URL"]}/verify-email/{user.VerificationToken}";
            // Email gönderme işlemi (Basitçe)
            try
            {
                await _emailService.SendEmailAsync(user.Email, "Doğrulama Linki", $"<a href='{verificationLink}'>Doğrula</a>");
            }
            catch { }

            return new ApiResponse<object>(200, "Doğrulama emaili tekrar gönderildi.");
        }

        public async Task<ApiResponse<bool>> DeleteAccountAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return new ApiResponse<bool>(404, "Kullanıcı bulunamadı.");

            // Soft Delete
            user.Deleted = true;
            // İlişkili doktor profili varsa onu da sil
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor != null) doctor.Deleted = true;

            await _context.SaveChangesAsync();
            return new ApiResponse<bool>(200, "Hesap silindi.", true);
        }

        public async Task<ApiResponse<object>> RegisterAsync(RegisterDto dto, string? documentPath)
        {
            var existingUser = await _context.Users.AnyAsync(u => u.Email == dto.Email);
            if (existingUser) return new ApiResponse<object>(400, "Bu email adresi zaten kayıtlı");

            if (dto.Role == "doctor" && string.IsNullOrEmpty(documentPath))
                return new ApiResponse<object>(400, "Doktor hesabı için belge yüklemesi zorunludur");

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                PasswordHash = _passwordService.HashPassword(dto.Password),
                Role = dto.Role,
                IsVerified = false,
                VerificationToken = Guid.NewGuid().ToString("N"),
                IsDoctorApproved = false,
                DoctorDocuments = documentPath,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            if (user.Role == "doctor")
            {
                var doctor = new Doctor
                {
                    UserId = user.Id,
                    Speciality = dto.Speciality,
                    Hospital = dto.Hospital ?? "Belirtilmemiş",
                    CreatedAt = DateTime.UtcNow
                };
                await _context.Doctors.AddAsync(doctor);
                await _context.SaveChangesAsync();
            }

            var verificationLink = $"{_config["CLIENT_URL"]}/verify-email/{user.VerificationToken}";
            var emailBody = $@"
                <h3>Hesap Doğrulama</h3>
                <p>HealthApp'e hoş geldiniz! Hesabınızı doğrulamak için lütfen aşağıdaki linke tıklayın:</p>
                <a href='{verificationLink}'>Hesabımı Doğrula</a>";

            try
            {
                await _emailService.SendEmailAsync(user.Email, "Hesap Doğrulama", emailBody);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Email gönderme hatası: " + ex.Message);
            }

            return new ApiResponse<object>(201, "Kayıt başarılı. Lütfen e-postanızı doğrulayın.", new { user.Id, user.Email });
        }

        public async Task<ApiResponse<object>> LoginAsync(LoginDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || !_passwordService.VerifyPassword(dto.Password, user.PasswordHash))
                return new ApiResponse<object>(401, "Geçersiz email veya şifre");

            if (user.IsVerified != true)
                return new ApiResponse<object>(401, "Lütfen önce e-posta adresinizi doğrulayın.");

            if (user.Role == "doctor" && user.IsDoctorApproved != true)
                return new ApiResponse<object>(403, "Hesabınız henüz admin tarafından onaylanmamış.");

            var accessToken = _tokenService.GenerateToken(user, true);
            var refreshToken = _tokenService.GenerateToken(user, false);

            user.RefreshTokens ??= new List<string>();
            if (user.RefreshTokens.Count >= 3) user.RefreshTokens.RemoveAt(0);
            user.RefreshTokens.Add(refreshToken);

            await _context.SaveChangesAsync();

            return new ApiResponse<object>(200, "Giriş başarılı", new { user, tokens = new { accessToken, refreshToken } });
        }

        public async Task<ApiResponse<User>> GetProfileAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return new ApiResponse<User>(404, "Kullanıcı bulunamadı.");
            return new ApiResponse<User>(200, "Profil getirildi", user);
        }

        public async Task<ApiResponse<User>> UpdateProfileAsync(int userId, UpdateProfileDto dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return new ApiResponse<User>(404, "Kullanıcı bulunamadı.");

            // İsim güncellemesi (Direkt yapılır)
            if (!string.IsNullOrEmpty(dto.Name)) user.Name = dto.Name;

            // Avatar güncellemesi (Direkt yapılır)
            if (!string.IsNullOrEmpty(dto.Avatar)) user.Avatar = dto.Avatar;

            // Email Güncelleme Mantığı (Beklemeli)
            bool emailChangeRequested = false;
            if (!string.IsNullOrEmpty(dto.Email) && dto.Email != user.Email)
            {
                // 1. Bu email başkası tarafından kullanılıyor mu?
                var emailExists = await _context.Users.AnyAsync(u => u.Email == dto.Email);
                if (emailExists)
                    return new ApiResponse<User>(400, "Bu email adresi kullanımda.");

                // 2. Email'i hemen değiştirme! Pending alanlarına yaz.
                user.PendingEmail = dto.Email;
                user.PendingEmailToken = Guid.NewGuid().ToString("N");

                // Kural: 1 Dakika süre
                user.PendingEmailTokenExpire = DateTime.UtcNow.AddMinutes(1);

                emailChangeRequested = true;

                // 3. Yeni e-posta adresine doğrulama linki gönder
                var verifyUrl = $"{_config["CLIENT_URL"]}/verify-email-change/{user.PendingEmailToken}";
                var emailBody = $@"
            <h3>E-posta Değişikliği Onayı</h3>
            <p>E-posta adresinizi <b>{dto.Email}</b> olarak değiştirmek istediniz.</p>
            <p>Bu değişikliği onaylamak için 1 dakika içinde aşağıdaki linke tıklayın:</p>
            <a href='{verifyUrl}'>Değişikliği Onayla</a>
            <p>Eğer bu işlemi siz yapmadıysanız, bu emaili dikkate almayın.</p>";

                try
                {
                    await _emailService.SendEmailAsync(dto.Email, "E-posta Değişikliği", emailBody);
                }
                catch
                {
                    return new ApiResponse<User>(500, "Doğrulama maili gönderilemedi.");
                }
            }

            await _context.SaveChangesAsync();

            string message = "Profil güncellendi.";
            if (emailChangeRequested)
            {
                message = "Profil güncellendi. Yeni e-posta adresinize doğrulama linki gönderildi (1 dakika geçerli). Lütfen onaylayın.";
            }

            return new ApiResponse<User>(200, message, user);
        }

        // Yeni Metot: Email Değişikliğini Onaylama
        public async Task<ApiResponse<object>> ConfirmEmailChangeAsync(string token)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PendingEmailToken == token);

            if (user == null)
                return new ApiResponse<object>(400, "Geçersiz token.");

            // Süre kontrolü (1 dakika kuralı)
            if (user.PendingEmailTokenExpire < DateTime.UtcNow)
            {
                // Süre dolduysa pending alanlarını temizle
                user.PendingEmail = null;
                user.PendingEmailToken = null;
                user.PendingEmailTokenExpire = null;
                await _context.SaveChangesAsync();
                return new ApiResponse<object>(400, "Doğrulama süresi (1 dakika) doldu. İşlem iptal edildi.");
            }

            // Onay başarılı: PendingEmail'i gerçek Email yap
            user.Email = user.PendingEmail!;

            // Temizlik
            user.PendingEmail = null;
            user.PendingEmailToken = null;
            user.PendingEmailTokenExpire = null;

            // Güvenlik: Email değiştiği için isterseniz oturumları kapatabilirsiniz veya verified true kalabilir
            // user.IsVerified = true; // Zaten verified idi.

            await _context.SaveChangesAsync();

            return new ApiResponse<object>(200, "E-posta adresiniz başarıyla güncellendi.");
        }

        public async Task<ApiResponse<IEnumerable<HealthHistory>>> GetHealthHistoryAsync(int userId)
        {
            var history = await _context.HealthHistories
                .Where(h => h.PatientId == userId && !h.Deleted)
                .ToListAsync();
            return new ApiResponse<IEnumerable<HealthHistory>>(200, "Geçmiş getirildi", history);
        }

        public async Task<ApiResponse<object>> VerifyEmailAsync(string token)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.VerificationToken == token);
            if (user == null) return new ApiResponse<object>(400, "Geçersiz veya süresi dolmuş link.");

            user.IsVerified = true;
            user.VerificationToken = null;
            await _context.SaveChangesAsync();

            return new ApiResponse<object>(200, "E-posta başarıyla doğrulandı.");
        }

        // --- FAVORİ İŞLEMLERİ (DÜZELTİLDİ) ---
        // Not: User modelindeki 'Doctors' -> Kullanıcının kendi doktor profili (varsa)
        //      User modelindeki 'DoctorsNavigation' -> Kullanıcının FAVORİLEDİĞİ doktorlar (EF Core otomatik isimlendirme)

        public async Task<ApiResponse<object>> AddFavoriteDoctorAsync(int userId, int doctorId)
        {
            var doctor = await _context.Doctors.FindAsync(doctorId);
            // DÜZELTİLDİ: Include(u => u.DoctorsNavigation) kullanıldı
            var user = await _context.Users.Include(u => u.DoctorsNavigation).FirstOrDefaultAsync(u => u.Id == userId);

            if (user != null && doctor != null)
            {
                // DÜZELTİLDİ: Eğer zaten favori değilse ekle
                if (!user.DoctorsNavigation.Any(d => d.Id == doctorId))
                {
                    user.DoctorsNavigation.Add(doctor);
                    await _context.SaveChangesAsync();
                }
            }
            return new ApiResponse<object>(200, "Favori doktora eklendi.");
        }

        public async Task<ApiResponse<IEnumerable<Doctor>>> GetFavoriteDoctorsAsync(int userId)
        {
            // DÜZELTİLDİ: DoctorsNavigation include edildi
            var user = await _context.Users
                .Include(u => u.DoctorsNavigation)
                .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(u => u.Id == userId);

            return new ApiResponse<IEnumerable<Doctor>>(200, "Favoriler getirildi", user?.DoctorsNavigation ?? new List<Doctor>());
        }

        public async Task<ApiResponse<object>> RemoveFavoriteDoctorAsync(int userId, string doctorId)
        {
            if (!int.TryParse(doctorId, out int dId))
                return new ApiResponse<object>(400, "Geçersiz Doktor ID");

            // DÜZELTİLDİ: DoctorsNavigation kullanıldı
            var user = await _context.Users.Include(u => u.DoctorsNavigation).FirstOrDefaultAsync(u => u.Id == userId);

            var doctor = user?.DoctorsNavigation.FirstOrDefault(d => d.Id == dId);
            if (doctor != null)
            {
                user!.DoctorsNavigation.Remove(doctor);
                await _context.SaveChangesAsync();
            }
            return new ApiResponse<object>(200, "Favori doktor kaldırıldı.");
        }
        // ------------------------------------

        public async Task<ApiResponse<object>> ForgotPasswordAsync(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return new ApiResponse<object>(404, "Bu email ile kayıtlı kullanıcı bulunamadı");

            user.ResetPasswordToken = Guid.NewGuid().ToString("N");
            user.ResetPasswordExpire = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            var resetUrl = $"{_config["CLIENT_URL"]}/reset-password/{user.ResetPasswordToken}";
            var message = $@"
                <h3>Şifre Sıfırlama</h3>
                <p>Şifrenizi sıfırlamak için aşağıdaki linke tıklayın:</p>
                <a href='{resetUrl}'>Şifremi Sıfırla</a>
                <p>Link 1 saat geçerlidir.</p>";

            try
            {
                await _emailService.SendEmailAsync(user.Email, "Şifre Sıfırlama", message);
            }
            catch
            {
                return new ApiResponse<object>(500, "Email gönderilemedi");
            }

            return new ApiResponse<object>(200, "Şifre sıfırlama linki email adresinize gönderildi.");
        }

        public async Task<ApiResponse<object>> ResetPasswordAsync(string token, ResetPasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.ResetPasswordToken == token && u.ResetPasswordExpire > DateTime.UtcNow);
            if (user == null) return new ApiResponse<object>(400, "Geçersiz veya süresi dolmuş token");

            user.PasswordHash = _passwordService.HashPassword(dto.Password);
            user.ResetPasswordToken = null;
            user.ResetPasswordExpire = null;
            await _context.SaveChangesAsync();

            return new ApiResponse<object>(200, "Şifreniz sıfırlandı.");
        }

        public async Task<ApiResponse<object>> RefreshTokenAsync(RefreshTokenDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshTokens!.Contains(dto.RefreshToken));
            if (user == null) return new ApiResponse<object>(401, "Geçersiz refresh token");

            var newAccessToken = _tokenService.GenerateToken(user, true);
            return new ApiResponse<object>(200, "Token yenilendi", new { accessToken = newAccessToken });
        }

        public async Task<ApiResponse<object>> LogoutAsync(int userId, string refreshToken)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null && user.RefreshTokens != null)
            {
                user.RefreshTokens.Remove(refreshToken);
                await _context.SaveChangesAsync();
            }
            return new ApiResponse<object>(200, "Çıkış yapıldı.");
        }
    }
}