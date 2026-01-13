using healthapp.Context;
using healthapp.DTOs;
using healthapp.DTOs.UserDTOs;
using healthapp.Helpers;
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
        private readonly IAppointmentRepository _appointmentRepository;

        public UserRepository(PostgresContext context, IPasswordService passwordService, ITokenService tokenService, IConfiguration config, IEmailService emailService, IAppointmentRepository appointmentRepository)
        {
            _context = context;
            _passwordService = passwordService;
            _tokenService = tokenService;
            _config = config;
            _emailService = emailService;
            _appointmentRepository = appointmentRepository;
        }

        public async Task<ApiResponse<object>> ChangePasswordAsync(int userId, ChangePasswordDto dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return new ApiResponse<object>(404, "Kullanıcı bulunamadı.");
            if (!_passwordService.VerifyPassword(dto.CurrentPassword, user.PasswordHash)) return new ApiResponse<object>(400, "Mevcut şifre yanlış.");
            user.PasswordHash = _passwordService.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();
            return new ApiResponse<object>(200, "Şifre değiştirildi.");
        }

        public async Task<ApiResponse<object>> ResendVerificationEmailAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return new ApiResponse<object>(404, "Kullanıcı bulunamadı.");
            if (user.IsVerified == true) return new ApiResponse<object>(400, "Hesap zaten doğrulanmış.");


            user.VerificationToken = Guid.NewGuid().ToString("N");
            await _context.SaveChangesAsync();

            var verificationLink = $"{_config["CLIENT_URL"]}/verify-email/{user.VerificationToken}";

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


            user.Deleted = true;
            user.RefreshTokens = null;


            var doctor = await _context.Doctors
                .Include(d => d.Appointments)
                .FirstOrDefaultAsync(d => d.UserId == userId);

            if (doctor != null)
            {
                doctor.Deleted = true;


                var futureAppointments = doctor.Appointments
                    .Where(a => a.Date >= DateOnly.FromDateTime(DateTime.Now) && a.Status != "cancelled" && !a.Deleted)
                    .ToList();

                foreach (var appointment in futureAppointments)
                {
                    await _appointmentRepository.CancelAppointmentAsync(user.Id, "system", appointment.Id);
                }
            }
            else
            {

                var patientFutureAppointments = await _context.Appointments
                     .Where(a => a.PatientId == userId && a.Date >= DateOnly.FromDateTime(DateTime.Now) && a.Status != "cancelled" && !a.Deleted)
                     .ToListAsync();

                foreach (var app in patientFutureAppointments)
                {
                    await _appointmentRepository.CancelAppointmentAsync(user.Id, "system", app.Id);
                }
            }

            await _context.SaveChangesAsync();
            return new ApiResponse<bool>(200, "Hesabınız silindi. Gelecek randevularınız iptal edildi.", true);
        }
        public async Task<ApiResponse<object>> RegisterAsync(RegisterDto dto, string? documentPath)
        {

            if (string.IsNullOrEmpty(dto.Tc) || !TcValidator.Validate(dto.Tc))
                return new ApiResponse<object>(400, "Geçersiz TC Kimlik Numarası.");


            var activeUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email || u.Tc == dto.Tc);
            if (activeUser != null)
                return new ApiResponse<object>(400, "Bu bilgilerle kayıtlı aktif bir kullanıcı zaten var.");


            if (dto.Role == "doctor" && string.IsNullOrEmpty(documentPath))
                return new ApiResponse<object>(400, "Doktor hesabı için belge zorunludur.");

            User user;
            bool isReactivating = false;


            var deletedUser = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Doctors)
                .FirstOrDefaultAsync(u => (u.Email == dto.Email || u.Tc == dto.Tc) && u.Deleted);

            if (deletedUser != null)
            {

                isReactivating = true;
                user = deletedUser;

                user.Name = dto.Name;
                user.Email = dto.Email;
                user.PasswordHash = _passwordService.HashPassword(dto.Password);
                user.Deleted = false;
                user.IsVerified = false;
                user.VerificationToken = Guid.NewGuid().ToString("N");
                user.UpdatedAt = DateTime.UtcNow;




                if (user.Role == "doctor" && dto.Role == "patient")
                {
                    user.Role = "patient";

                    var existingDoc = user.Doctors.FirstOrDefault();
                    if (existingDoc != null) existingDoc.Deleted = true;
                }

                else if (user.Role == "patient" && dto.Role == "doctor")
                {
                    user.Role = "doctor";
                    user.IsDoctorApproved = false;
                    if (documentPath != null) user.DoctorDocuments = documentPath;

                }

                else
                {
                    if (user.Role == "doctor")
                    {
                        user.IsDoctorApproved = false;
                        if (documentPath != null) user.DoctorDocuments = documentPath;
                    }
                }
            }
            else
            {

                user = new User
                {
                    Name = dto.Name,
                    Email = dto.Email,
                    PasswordHash = _passwordService.HashPassword(dto.Password),
                    Role = dto.Role,
                    Tc = dto.Tc,
                    IsVerified = false,
                    VerificationToken = Guid.NewGuid().ToString("N"),
                    IsDoctorApproved = false,
                    DoctorDocuments = documentPath,
                    CreatedAt = DateTime.UtcNow,
                    Deleted = false
                };
                await _context.Users.AddAsync(user);
            }

            await _context.SaveChangesAsync();


            if (user.Role == "doctor")
            {
                var existingDoctor = await _context.Doctors
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(d => d.UserId == user.Id);

                if (existingDoctor != null)
                {

                    existingDoctor.Deleted = false;
                    existingDoctor.Speciality = dto.Speciality;
                    existingDoctor.Hospital = dto.Hospital ?? "Belirtilmemiş";
                    existingDoctor.UpdatedAt = DateTime.UtcNow;

                }
                else
                {

                    var doctor = new Doctor
                    {
                        UserId = user.Id,
                        Speciality = dto.Speciality,
                        Hospital = dto.Hospital ?? "Belirtilmemiş",
                        CreatedAt = DateTime.UtcNow,
                        Deleted = false
                    };
                    await _context.Doctors.AddAsync(doctor);
                }
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

            return new ApiResponse<object>(201, isReactivating ? "Hesabınız tekrar aktifleştirildi." : "Kayıt başarılı.", new { user.Id });
        }

        public async Task<ApiResponse<object>> LoginAsync(LoginDto dto)
        {

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null || !_passwordService.VerifyPassword(dto.Password, user.PasswordHash))
                return new ApiResponse<object>(401, "Geçersiz email veya şifre");

            if (user.IsVerified != true) return new ApiResponse<object>(401, "E-posta doğrulanmamış.");
            if (user.Role == "doctor" && user.IsDoctorApproved != true) return new ApiResponse<object>(403, "Doktor onayı bekleniyor.");

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
            return user == null ? new ApiResponse<User>(404, "Bulunamadı") : new ApiResponse<User>(200, "Profil", user);
        }

        public async Task<ApiResponse<User>> UpdateProfileAsync(int userId, UpdateProfileDto dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return new ApiResponse<User>(404, "Kullanıcı bulunamadı.");


            if (!string.IsNullOrEmpty(dto.Name)) user.Name = dto.Name;


            if (!string.IsNullOrEmpty(dto.Avatar)) user.Avatar = dto.Avatar;


            bool emailChangeRequested = false;
            if (!string.IsNullOrEmpty(dto.Email) && dto.Email != user.Email)
            {

                var emailExists = await _context.Users.AnyAsync(u => u.Email == dto.Email);
                if (emailExists)
                    return new ApiResponse<User>(400, "Bu email adresi kullanımda.");


                user.PendingEmail = dto.Email;
                user.PendingEmailToken = Guid.NewGuid().ToString("N");


                user.PendingEmailTokenExpire = DateTime.UtcNow.AddMinutes(1);

                emailChangeRequested = true;


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

        public async Task<ApiResponse<object>> ConfirmEmailChangeAsync(string token)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PendingEmailToken == token);

            if (user == null)
                return new ApiResponse<object>(400, "Geçersiz token.");


            if (user.PendingEmailTokenExpire < DateTime.UtcNow)
            {

                user.PendingEmail = null;
                user.PendingEmailToken = null;
                user.PendingEmailTokenExpire = null;
                await _context.SaveChangesAsync();
                return new ApiResponse<object>(400, "Doğrulama süresi (1 dakika) doldu. İşlem iptal edildi.");
            }


            user.Email = user.PendingEmail!;


            user.PendingEmail = null;
            user.PendingEmailToken = null;
            user.PendingEmailTokenExpire = null;


            await _context.SaveChangesAsync();

            return new ApiResponse<object>(200, "E-posta adresiniz başarıyla güncellendi.");
        }

        public async Task<ApiResponse<IEnumerable<HealthHistory>>> GetHealthHistoryAsync(int userId)
        {

            var history = await _context.HealthHistories
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(h => h.Doctor).ThenInclude(d => d!.User)
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

        public async Task<ApiResponse<object>> AddFavoriteDoctorAsync(int userId, int doctorId)
        {
            var doctor = await _context.Doctors.FindAsync(doctorId);

            var user = await _context.Users.Include(u => u.DoctorsNavigation).FirstOrDefaultAsync(u => u.Id == userId);

            if (user != null && doctor != null)
            {

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

            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.DoctorsNavigation).ThenInclude(d => d.User)
                .Include(u => u.DoctorsNavigation).ThenInclude(d => d.SpecialityNavigation)
                .FirstOrDefaultAsync(u => u.Id == userId);

            return new ApiResponse<IEnumerable<Doctor>>(200, "Favoriler", user?.DoctorsNavigation ?? new List<Doctor>());
        }

        public async Task<ApiResponse<object>> RemoveFavoriteDoctorAsync(int userId, string doctorId)
        {
            if (!int.TryParse(doctorId, out int dId))
                return new ApiResponse<object>(400, "Geçersiz Doktor ID");


            var user = await _context.Users.Include(u => u.DoctorsNavigation).FirstOrDefaultAsync(u => u.Id == userId);

            var doctor = user?.DoctorsNavigation.FirstOrDefault(d => d.Id == dId);
            if (doctor != null)
            {
                user!.DoctorsNavigation.Remove(doctor);
                await _context.SaveChangesAsync();
            }
            return new ApiResponse<object>(200, "Favori doktor kaldırıldı.");
        }


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
                <p>1 saat geçerlidir.</p>";

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

        public async Task<ApiResponse<bool>> VerifyIdentityAsync(int userId, string tcNumber)
        {
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return new ApiResponse<bool>(404, "Kullanıcı bulunamadı.");


            if (user.Tc != tcNumber)
                return new ApiResponse<bool>(400, "Girdiğiniz TC Kimlik Numarası sistemdeki ile uyuşmuyor.", false);

            return new ApiResponse<bool>(200, "Kimlik doğrulandı.", true);
        }
    }
}