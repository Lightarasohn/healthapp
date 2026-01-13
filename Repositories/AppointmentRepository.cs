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
        private readonly IEmailService _emailService;
        public AppointmentRepository(PostgresContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<ApiResponse<IEnumerable<Appointment>>> GetAllAppointmentsAsync()
        {
            var list = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.Patient)
                .OrderByDescending(a => a.Date)
                .ThenBy(a => a.Start)
                .ToListAsync();

            return new ApiResponse<IEnumerable<Appointment>>(200, "Tüm randevular listelendi", list);
        }

        public async Task<ApiResponse<Appointment>> CreateAppointmentAsync(int patientId, CreateAppointmentDto dto)
        {
            try
            {

                var now = DateTime.Now;
                var today = DateOnly.FromDateTime(now);
                var nowTime = TimeOnly.FromDateTime(now);


                if (dto.Date < today)
                {
                    return new ApiResponse<Appointment>(400, "Geçmiş bir tarihe randevu alınamaz.");
                }


                if (dto.Date == today && dto.Start <= nowTime)
                {
                    return new ApiResponse<Appointment>(400, "Geçmiş bir saate randevu alınamaz.");
                }


                var doctor = await _context.Doctors
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.Id == dto.DoctorId);

                if (doctor == null) return new ApiResponse<Appointment>(400, "Geçersiz doktor ID.");


                if (!string.IsNullOrEmpty(doctor.UnavailableDates))
                {
                    try
                    {
                        var unavailableList = JsonDocument.Parse(doctor.UnavailableDates).RootElement;
                        foreach (var range in unavailableList.EnumerateArray())
                        {
                            JsonElement startProp, endProp;
                            if (!range.TryGetProperty("startDate", out startProp) && !range.TryGetProperty("StartDate", out startProp)) continue;
                            if (!range.TryGetProperty("endDate", out endProp) && !range.TryGetProperty("EndDate", out endProp)) continue;

                            var startRange = DateOnly.FromDateTime(startProp.GetDateTime());
                            var endRange = DateOnly.FromDateTime(endProp.GetDateTime());

                            if (dto.Date >= startRange && dto.Date <= endRange)
                                return new ApiResponse<Appointment>(400, "Doktor bu tarihler arasında müsait değil.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"UnavailableDates parse error: {ex.Message}");

                    }
                }


                if (string.IsNullOrEmpty(doctor.Clocks)) return new ApiResponse<Appointment>(400, "Doktorun çalışma saatleri tanımlanmamış.");

                var dayName = dto.Date.DayOfWeek.ToString().ToLower();


                var clocksJson = JsonDocument.Parse(doctor.Clocks).RootElement;

                if (!clocksJson.TryGetProperty(dayName, out var daySchedule))
                    return new ApiResponse<Appointment>(400, $"Doktor {dayName} günü çalışmamaktadır.");

                if (!daySchedule.TryGetProperty("start", out var startElem) || !daySchedule.TryGetProperty("end", out var endElem))
                    return new ApiResponse<Appointment>(400, $"Doktorun {dayName} günü için saat bilgisi eksik.");

                var startStr = startElem.GetString();
                var endStr = endElem.GetString();

                if (string.IsNullOrEmpty(startStr) || string.IsNullOrEmpty(endStr))
                    return new ApiResponse<Appointment>(400, $"Doktor {dayName} günü çalışmamaktadır.");

                var scheduleStart = TimeOnly.Parse(startStr);
                var scheduleEnd = TimeOnly.Parse(endStr);


                var finalEnd = dto.End ?? dto.Start.AddHours(1);


                if (dto.Start < scheduleStart || finalEnd > scheduleEnd)
                    return new ApiResponse<Appointment>(400, $"Çalışma saatleri dışı: {scheduleStart} - {scheduleEnd}");


                var isOverlapping = await _context.Appointments.AnyAsync(a =>
                    a.DoctorId == dto.DoctorId &&
                    a.Date == dto.Date &&
                    a.Status != "cancelled" &&
                    dto.Start == a.Start && finalEnd == a.End
                );

                if (isOverlapping) return new ApiResponse<Appointment>(400, "Bu saat aralığında başka bir randevu mevcut.");


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


                try
                {
                    var patient = await _context.Users.FindAsync(patientId);

                    if (patient != null && !string.IsNullOrEmpty(patient.Email))
                    {
                        var subject = "Randevunuz Oluşturuldu - HealthApp";
                        var body = $@"
                    <h3>Randevunuz Oluşturuldu</h3>
                    <p>Sayın <b>{patient.Name}</b>,</p>
                    <p>Randevunuz başarıyla oluşturulmuştur. Detaylar aşağıdadır:</p>
                    <ul>
                        <li><b>Doktor:</b> {doctor.User?.Name}</li>
                        <li><b>Tarih:</b> {dto.Date:dd.MM.yyyy}</li>
                        <li><b>Saat:</b> {dto.Start} - {finalEnd}</li>
                        <li><b>Hastane:</b> {doctor.Hospital}</li>
                    </ul>
                    <p>Sağlıklı günler dileriz.</p>";

                        await _emailService.SendEmailAsync(patient.Email, subject, body);
                    }
                }
                catch (Exception ex)
                {

                    Console.WriteLine("Email gönderme hatası: " + ex.Message);
                }

                return new ApiResponse<Appointment>(201, "Randevu başarıyla oluşturuldu", appointment);
            }
            catch (Exception ex)
            {
                return new ApiResponse<Appointment>(500, $"Randevu oluşturulurken beklenmeyen bir hata oluştu: {ex.Message}");
            }
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

            var appointment = await _context.Appointments
                .IgnoreQueryFilters()
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return new ApiResponse<bool>(404, "Randevu bulunamadı.");

            bool isAuthorized = false;


            if (role == "admin") isAuthorized = true;
            else if (role == "patient" && appointment.PatientId == userId) isAuthorized = true;
            else if (role == "doctor")
            {

                if (appointment.Doctor != null && appointment.Doctor.UserId == userId)
                    isAuthorized = true;
            }

            if (!isAuthorized) return new ApiResponse<bool>(403, "Bu işlemi yapmaya yetkiniz yok.");

            appointment.Status = "cancelled";
            await _context.SaveChangesAsync();


            try
            {
                if (appointment.Patient != null && !string.IsNullOrEmpty(appointment.Patient.Email))
                {
                    var canceller = role == "admin" ? "Yönetici" : "Doktor";
                    var subject = "Randevu İptali - HealthApp";
                    var body = $@"
                        <h3>Randevunuz İptal Edildi</h3>
                        <p>Sayın <b>{appointment.Patient.Name}</b>,</p>
                        <p>Aşağıdaki randevunuz {canceller} tarafından iptal edilmiştir:</p>
                        <ul>
                            <li><b>Doktor:</b> {appointment.Doctor?.User?.Name}</li>
                            <li><b>Tarih:</b> {appointment.Date:dd.MM.yyyy}</li>
                            <li><b>Saat:</b> {appointment.Start}</li>
                        </ul>
                        <p>Yaşattığımız rahatsızlıktan dolayı özür dileriz. Yeni bir randevu almak için sistemimizi ziyaret edebilirsiniz.</p>";

                    await _emailService.SendEmailAsync(appointment.Patient.Email, subject, body);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("İptal emaili gönderme hatası: " + ex.Message);
            }


            return new ApiResponse<bool>(200, "Randevu iptal edildi.", true);
        }

        public async Task<ApiResponse<IEnumerable<Appointment>>> GetDoctorAppointmentsAsync(int userId)
        {
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor == null) return new ApiResponse<IEnumerable<Appointment>>(404, "Doktor bulunamadı.");

            var list = await _context.Appointments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(a => a.DoctorId == doctor.Id && !a.Deleted)
                .Include(a => a.Patient)
                .Include(a => a.HealthHistory)
                .OrderBy(a => a.Date).ThenBy(a => a.Start)
                .ToListAsync();

            return new ApiResponse<IEnumerable<Appointment>>(200, "Randevular getirildi", list);
        }

        public async Task<ApiResponse<IEnumerable<Appointment>>> GetPatientAppointmentsAsync(int patientId)
        {
            var list = await _context.Appointments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(a => a.PatientId == patientId && !a.Deleted)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.HealthHistory)
                .OrderByDescending(a => a.Date).ThenBy(a => a.Start)
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


            if (appointment.PatientId != userId) return new ApiResponse<Appointment>(403, "Yetkisiz işlem.");

            appointment.Date = dto.Date;
            appointment.Start = dto.Start;

            appointment.End = dto.End ?? dto.Start.AddHours(1);
            appointment.Status = "booked";

            await _context.SaveChangesAsync();
            return new ApiResponse<Appointment>(200, "Randevu güncellendi", appointment);
        }
        public async Task<ApiResponse<Appointment>> CompleteAppointmentAsync(int userId, int appointmentId, CompleteAppointmentDto dto)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null) return new ApiResponse<Appointment>(404, "Randevu bulunamadı.");


            if (appointment.Doctor?.UserId != userId)
                return new ApiResponse<Appointment>(403, "Bu işlem için yetkiniz yok.");

            if (appointment.Status != "booked" && appointment.Status != "confirmed")
                return new ApiResponse<Appointment>(400, "Sadece aktif randevular tamamlanabilir.");


            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {

                appointment.Status = "completed";


                var history = new HealthHistory
                {
                    PatientId = appointment.PatientId,
                    AppointmentId = appointment.Id,
                    DoctorId = appointment.DoctorId,
                    Diagnosis = dto.Diagnosis,
                    Treatment = dto.Treatment,
                    Notes = dto.Notes,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.HealthHistories.AddAsync(history);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();


                appointment.HealthHistory = history;
                return new ApiResponse<Appointment>(200, "Randevu başarıyla tamamlandı ve kayıt oluşturuldu.", appointment);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ApiResponse<Appointment>(500, "İşlem sırasında hata oluştu: " + ex.Message);
            }
        }
        public async Task<ApiResponse<IEnumerable<string>>> GetBookedSlotsAsync(int doctorId, DateOnly date)
        {

            var bookedSlots = await _context.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId && a.Date == date && a.Status != "cancelled")
                .Select(a => a.Start)
                .ToListAsync();


            var formattedSlots = bookedSlots.Select(t => t.ToString("HH:mm"));

            return new ApiResponse<IEnumerable<string>>(200, "Dolu saatler getirildi", formattedSlots);
        }
    }
}