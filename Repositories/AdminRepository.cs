using healthapp.Context;
using healthapp.DTOs;
using healthapp.DTOs.AdminDTOs;
using healthapp.Interfaces;
using healthapp.Models;
using Microsoft.EntityFrameworkCore;

namespace healthapp.Repositories
{
    public class AdminRepository : IAdminRepository
    {
        private readonly PostgresContext _context;
        private readonly IPasswordService _passwordService;

        public AdminRepository(PostgresContext context, IPasswordService passwordService)
        {
            _context = context;
            _passwordService = passwordService;
        }

        // --- YENİ EKLENEN METOTLAR ---

        public async Task<ApiResponse<User>> GetUserByIdAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            return user == null 
                ? new ApiResponse<User>(404, "Kullanıcı bulunamadı") 
                : new ApiResponse<User>(200, "Kullanıcı getirildi", user);
        }

        public async Task<ApiResponse<User>> UpdateUserRoleAsync(int id, string role)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return new ApiResponse<User>(404, "Kullanıcı bulunamadı");

            user.Role = role;
            
            // Eğer doktor olduysa Doctor tablosuna da eklemek gerekebilir (basit tutuyoruz)
            if (role == "doctor" && !await _context.Doctors.AnyAsync(d => d.UserId == id))
            {
                _context.Doctors.Add(new Doctor { UserId = id, Speciality = 1 });
            }

            await _context.SaveChangesAsync();
            return new ApiResponse<User>(200, "Rol güncellendi", user);
        }

        public async Task<ApiResponse<object>> GetUserStatsAsync()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalPatients = await _context.Users.CountAsync(u => u.Role == "patient");
            var totalDoctors = await _context.Users.CountAsync(u => u.Role == "doctor");
            var pendingDoctors = await _context.Doctors.CountAsync(d => d.User!.IsDoctorApproved == false);
            var totalAppointments = await _context.Appointments.CountAsync();

            var stats = new
            {
                totalUsers,
                totalPatients,
                totalDoctors,
                pendingDoctors,
                totalAppointments
            };

            return new ApiResponse<object>(200, "İstatistikler", stats);
        }

        // --- MEVCUT METOTLAR ---
        public async Task<ApiResponse<object>> CreateAdminAsync(CreateAdminDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email)) return new ApiResponse<object>(400, "Email kayıtlı");
            var admin = new User { Name = dto.Name, Email = dto.Email, PasswordHash = _passwordService.HashPassword(dto.Password), Role = "admin", IsVerified = true };
            _context.Users.Add(admin);
            await _context.SaveChangesAsync();
            return new ApiResponse<object>(201, "Admin oluşturuldu");
        }

        public async Task<ApiResponse<IEnumerable<User>>> GetAllUsersAsync() => 
            new ApiResponse<IEnumerable<User>>(200, "Liste", await _context.Users.ToListAsync());

        public async Task<ApiResponse<IEnumerable<User>>> GetUsersByRoleAsync(string role) => 
            new ApiResponse<IEnumerable<User>>(200, "Liste", await _context.Users.Where(u => u.Role == role).ToListAsync());

        public async Task<ApiResponse<Doctor>> ApproveDoctorAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if(user == null) return new ApiResponse<Doctor>(404, "Kullanıcı yok");
            user.IsDoctorApproved = true;
            await _context.SaveChangesAsync();
            return new ApiResponse<Doctor>(200, "Onaylandı");
        }

        public async Task<ApiResponse<IEnumerable<Doctor>>> GetPendingDoctorsAsync() =>
            new ApiResponse<IEnumerable<Doctor>>(200, "Bekleyenler", await _context.Doctors.Include(d => d.User).Where(d => d.User!.Role == "doctor" && d.User.IsDoctorApproved == false).ToListAsync());

        public async Task<ApiResponse<bool>> DeleteUserAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if(user == null) return new ApiResponse<bool>(404, "Kullanıcı yok");
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return new ApiResponse<bool>(200, "Silindi", true);
        }

        public async Task<(Stream? FileStream, string ContentType, string FileName)> DownloadDoctorDocumentAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.DoctorDocuments)) return (null, "", "");
            if (!File.Exists(user.DoctorDocuments)) return (null, "", "");
            var memory = new MemoryStream();
            using(var stream = new FileStream(user.DoctorDocuments, FileMode.Open)) { await stream.CopyToAsync(memory); }
            memory.Position = 0;
            return (memory, "application/pdf", Path.GetFileName(user.DoctorDocuments));
        }
    }
}