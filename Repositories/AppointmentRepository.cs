using healthapp.Context;
using healthapp.DTOs;
using healthapp.DTOs.AppointmentDTOs;
using healthapp.Interfaces;
using healthapp.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace healthapp.Repositories
{
    public class AppointmentRepository : IAppointmentRepository
    {
        private readonly PostgresContext _context;

        public AppointmentRepository(PostgresContext context) => _context = context;

        public async Task<ApiResponse<IEnumerable<Appointment>>> GetAllAppointmentsAsync()
        {
            var list = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Patient)
                .OrderByDescending(a => a.Date)
                .ThenBy(a => a.Start)
                .ToListAsync();

            return new ApiResponse<IEnumerable<Appointment>>(200, "Tüm randevular listelendi", list);
        }

        public async Task<ApiResponse<Appointment>> CreateAppointmentAsync(int patientId, CreateAppointmentDto dto)
        {
            // 1. Geçmiş saat kontrolü
            var now = DateTime.Now;
            if (dto.Date == DateOnly.FromDateTime(now))
            {
                if (dto.Start.Hour < now.Hour || (dto.Start.Hour == now.Hour && dto.Start.Minute <= now.Minute))
                    return new ApiResponse<Appointment>(400, "Geçmiş bir saate randevu alınamaz.");
            }

            // 2. Doktor ve Fiyat kontrolü
            var doctor = await _context.Doctors.FindAsync(dto.DoctorId);
            if (doctor == null) return new ApiResponse<Appointment>(400, "Geçersiz doktor ID.");

            // 3. İzinli gün (unavailableDates) kontrolü
            if (!string.IsNullOrEmpty(doctor.UnavailableDates))
            {
                var unavailableList = JsonDocument.Parse(doctor.UnavailableDates).RootElement;
                foreach (var range in unavailableList.EnumerateArray())
                {
                    var startRange = DateOnly.FromDateTime(range.GetProperty("startDate").GetDateTime());
                    var endRange = DateOnly.FromDateTime(range.GetProperty("endDate").GetDateTime());
                    if (dto.Date >= startRange && dto.Date <= endRange)
                        return new ApiResponse<Appointment>(400, "Doktor bu tarihler arasında müsait değil.");
                }
            }

            // 4. Çalışma saatleri (clocks) kontrolü
            if (string.IsNullOrEmpty(doctor.Clocks)) return new ApiResponse<Appointment>(400, "Doktorun çalışma saatleri tanımlanmamış.");
            
            var dayName = dto.Date.DayOfWeek.ToString().ToLower();
            var clocksJson = JsonDocument.Parse(doctor.Clocks).RootElement;
            
            if (!clocksJson.TryGetProperty(dayName, out var daySchedule) || 
                string.IsNullOrEmpty(daySchedule.GetProperty("start").GetString()))
                return new ApiResponse<Appointment>(400, $"Doktor {dayName} günü çalışmamaktadır.");

            var scheduleStart = TimeOnly.Parse(daySchedule.GetProperty("start").GetString()!);
            var scheduleEnd = TimeOnly.Parse(daySchedule.GetProperty("end").GetString()!);

            // Bitiş saati yoksa 1 saat ekle
            var finalEnd = dto.End ?? dto.Start.AddHours(1);

            if (dto.Start < scheduleStart || finalEnd > scheduleEnd)
                return new ApiResponse<Appointment>(400, $"Çalışma saatleri dışı: {scheduleStart} - {scheduleEnd}");

            // 5. Çakışan randevu kontrolü
            var isOverlapping = await _context.Appointments.AnyAsync(a =>
                a.DoctorId == dto.DoctorId &&
                a.Date == dto.Date &&
                a.Status != "cancelled" &&
                ((dto.Start < a.End && dto.Start >= a.Start) || (finalEnd > a.Start && finalEnd <= a.End)));

            if (isOverlapping) return new ApiResponse<Appointment>(400, "Bu saat aralığında başka bir randevu mevcut.");

            // 6. Kayıt
            var appointment = new Appointment
            {
                DoctorId = dto.DoctorId,
                PatientId = patientId,
                Date = dto.Date,
                Start = dto.Start,
                End = finalEnd,
                Notes = dto.Notes,
                Status = "booked",
                Price = doctor.ConsultationFee ?? 0,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Appointments.AddAsync(appointment);
            await _context.SaveChangesAsync();

            return new ApiResponse<Appointment>(201, "Randevu oluşturuldu", appointment);
        }

        public async Task<ApiResponse<Appointment>> GetAppointmentDetailsAsync(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return new ApiResponse<Appointment>(404, "Randevu bulunamadı.");
            return new ApiResponse<Appointment>(200, "Detaylar getirildi", appointment);
        }

        public async Task<ApiResponse<bool>> CancelAppointmentAsync(int userId, string role, int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return new ApiResponse<bool>(404, "Randevu bulunamadı.");

            bool isAuthorized = false;
            
            // Admin her şeyi iptal edebilir
            if (role == "admin") isAuthorized = true;
            else if (role == "patient" && appointment.PatientId == userId) isAuthorized = true;
            else if (role == "doctor")
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
                if (doctor != null && appointment.DoctorId == doctor.Id) isAuthorized = true;
            }

            if (!isAuthorized) return new ApiResponse<bool>(403, "Bu işlemi yapmaya yetkiniz yok.");
            
            appointment.Status = "cancelled";
            await _context.SaveChangesAsync();
            return new ApiResponse<bool>(200, "Randevu iptal edildi.", true);
        }

        public async Task<ApiResponse<IEnumerable<Appointment>>> GetDoctorAppointmentsAsync(int userId)
        {
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor == null) return new ApiResponse<IEnumerable<Appointment>>(404, "Doktor bulunamadı.");

            var list = await _context.Appointments
                .Where(a => a.DoctorId == doctor.Id && (a.Status == "booked" || a.Status == "completed"))
                .Include(a => a.Patient)
                .OrderBy(a => a.Date).ThenBy(a => a.Start)
                .ToListAsync();

            return new ApiResponse<IEnumerable<Appointment>>(200, "Randevular getirildi", list);
        }

        public async Task<ApiResponse<IEnumerable<Appointment>>> GetPatientAppointmentsAsync(int patientId)
        {
            var list = await _context.Appointments
                .Where(a => a.PatientId == patientId)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .OrderBy(a => a.Date).ThenBy(a => a.Start)
                .ToListAsync();

            return new ApiResponse<IEnumerable<Appointment>>(200, "Randevular getirildi", list);
        }

        public async Task<ApiResponse<Appointment>> UpdateStatusAsync(int userId, int id, string status)
        {
            var appointment = await _context.Appointments.Include(a => a.Doctor).FirstOrDefaultAsync(a => a.Id == id);
            if (appointment == null) return new ApiResponse<Appointment>(404, "Randevu bulunamadı.");

            if (appointment.Doctor?.UserId != userId)
                return new ApiResponse<Appointment>(403, "Sadece kendi randevularınızı güncelleyebilirsiniz.");

            appointment.Status = status;
            await _context.SaveChangesAsync();
            return new ApiResponse<Appointment>(200, "Durum güncellendi", appointment);
        }

        public async Task<ApiResponse<Appointment>> RescheduleAppointmentAsync(int userId, int id, RescheduleDto dto)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return new ApiResponse<Appointment>(404, "Randevu bulunamadı.");

            // Sadece randevu sahibi veya doktor değiştirebilir (basitlik için sadece hasta diyelim)
            if (appointment.PatientId != userId) return new ApiResponse<Appointment>(403, "Yetkisiz işlem.");

            appointment.Date = dto.Date;
            appointment.Start = dto.Start;
            // End yoksa 1 saat ekle
            appointment.End = dto.End ?? dto.Start.AddHours(1);
            appointment.Status = "booked"; // İptal edilmişse tekrar aktif et

            await _context.SaveChangesAsync();
            return new ApiResponse<Appointment>(200, "Randevu güncellendi", appointment);
        }
    }
}