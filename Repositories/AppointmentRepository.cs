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
                // 1. DÜZELTME: Geçmiş Tarih ve Saat Kontrolü (Tam kapsamlı)
                var now = DateTime.Now;
                var today = DateOnly.FromDateTime(now);
                var nowTime = TimeOnly.FromDateTime(now);

                // a) Tarih geçmişse (dün, geçen ay vs.) direkt hata ver
                if (dto.Date < today)
                {
                    return new ApiResponse<Appointment>(400, "Geçmiş bir tarihe randevu alınamaz.");
                }

                // b) Tarih bugünse, saatin geçip geçmediğini kontrol et
                if (dto.Date == today && dto.Start <= nowTime)
                {
                    return new ApiResponse<Appointment>(400, "Geçmiş bir saate randevu alınamaz.");
                }

                // 2. Doktor ve Fiyat kontrolü
                // Email'de doktor adını kullanmak için User tablosunu da Include ediyoruz.
                var doctor = await _context.Doctors
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.Id == dto.DoctorId);

                if (doctor == null) return new ApiResponse<Appointment>(400, "Geçersiz doktor ID.");

                // 3. İzinli gün (unavailableDates) kontrolü
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
                        // JSON hatası akışı durdurmasın diye loglayıp devam edebilir veya hata fırlatabilirsin.
                        // Burada devam etmeyi seçiyoruz.
                    }
                }

                // 4. Çalışma saatleri (clocks) kontrolü
                if (string.IsNullOrEmpty(doctor.Clocks)) return new ApiResponse<Appointment>(400, "Doktorun çalışma saatleri tanımlanmamış.");

                var dayName = dto.Date.DayOfWeek.ToString().ToLower();

                // Clocks parse işlemi
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

                // Bitiş saati verilmemişse varsayılan olarak 1 saat ekle
                var finalEnd = dto.End ?? dto.Start.AddHours(1);

                // Randevu çalışma saatleri dışında mı?
                if (dto.Start < scheduleStart || finalEnd > scheduleEnd)
                    return new ApiResponse<Appointment>(400, $"Çalışma saatleri dışı: {scheduleStart} - {scheduleEnd}");

                // 5. Çakışan randevu kontrolü (Overlap Check)
                var isOverlapping = await _context.Appointments.AnyAsync(a =>
                    a.DoctorId == dto.DoctorId &&
                    a.Date == dto.Date &&
                    // İptal edilmemiş tüm randevuları dikkate al (booked, completed vs.)
                    a.Status != "cancelled" &&
                    // Kesişim Formülü: (YeniBaşlangıç < EskiBitiş) VE (YeniBitiş > EskiBaşlangıç)
                    dto.Start < a.End && finalEnd > a.Start &&
                    dto.Start != a.Start && finalEnd != a.End
                );

                if (isOverlapping) return new ApiResponse<Appointment>(400, "Bu saat aralığında başka bir randevu mevcut.");

                // 6. Kayıt İşlemi
                var appointment = new Appointment
                {
                    DoctorId = dto.DoctorId,
                    PatientId = patientId,
                    Date = dto.Date,
                    Start = dto.Start,
                    End = finalEnd,
                    Notes = dto.Notes,
                    Status = "booked", // İleride enum kullanmak daha temiz olur
                    Price = doctor.ConsultationFee ?? 0,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Appointments.AddAsync(appointment);
                await _context.SaveChangesAsync();

                // 7. Email Gönderme İşlemi
                // Bu işlem ana akışı bozmaması için try-catch içinde yapılır.
                try
                {
                    var patient = await _context.Users.FindAsync(patientId);

                    if (patient != null && !string.IsNullOrEmpty(patient.Email))
                    {
                        var subject = "Randevunuz Oluşturuldu - HealthApp";
                        var body = $@"
                    <h3>Randevu Onayı</h3>
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
                    // Email hatası kritik değildir, loglayıp devam et.
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
            // Email gönderebilmek için Patient ve Doctor bilgilerini Include etmemiz lazım.
            // FindAsync Include desteklemez, FirstOrDefaultAsync kullanıyoruz.
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return new ApiResponse<bool>(404, "Randevu bulunamadı.");

            bool isAuthorized = false;

            // Yetki Kontrolü
            if (role == "admin") isAuthorized = true;
            else if (role == "patient" && appointment.PatientId == userId) isAuthorized = true;
            else if (role == "doctor")
            {
                // Randevudaki doktor ID'si, işlemi yapan kullanıcının doktor profiliyle eşleşiyor mu?
                if (appointment.Doctor != null && appointment.Doctor.UserId == userId)
                    isAuthorized = true;
            }

            if (!isAuthorized) return new ApiResponse<bool>(403, "Bu işlemi yapmaya yetkiniz yok.");

            appointment.Status = "cancelled";
            await _context.SaveChangesAsync();

            // --- EMAIL GÖNDERME İŞLEMİ (İPTAL) ---
            try
            {
                if (appointment.Patient != null && !string.IsNullOrEmpty(appointment.Patient.Email))
                {
                    var subject = "Randevu İptali - HealthApp";
                    var body = $@"
                        <h3>Randevunuz İptal Edildi</h3>
                        <p>Sayın <b>{appointment.Patient.Name}</b>,</p>
                        <p>Aşağıdaki randevunuz iptal edilmiştir:</p>
                        <ul>
                            <li><b>Doktor:</b> {appointment.Doctor?.User?.Name}</li>
                            <li><b>Tarih:</b> {appointment.Date:dd.MM.yyyy}</li>
                            <li><b>Saat:</b> {appointment.Start}</li>
                        </ul>
                        <p>Yeni bir randevu almak için sistemimizi ziyaret edebilirsiniz.</p>";

                    await _emailService.SendEmailAsync(appointment.Patient.Email, subject, body);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("İptal emaili gönderme hatası: " + ex.Message);
            }
            // -------------------------------------

            return new ApiResponse<bool>(200, "Randevu iptal edildi.", true);
        }

        public async Task<ApiResponse<IEnumerable<Appointment>>> GetDoctorAppointmentsAsync(int userId)
        {
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor == null) return new ApiResponse<IEnumerable<Appointment>>(404, "Doktor bulunamadı.");

            var list = await _context.Appointments
                .Where(a => a.DoctorId == doctor.Id && (a.Status == "booked" || a.Status == "completed"))
                .Include(a => a.Patient)
                .Include(a => a.HealthHistory)
                .OrderBy(a => a.Date).ThenBy(a => a.Start)
                .ToListAsync();

            return new ApiResponse<IEnumerable<Appointment>>(200, "Randevular getirildi", list);
        }

        public async Task<ApiResponse<IEnumerable<Appointment>>> GetPatientAppointmentsAsync(int patientId)
        {
            var list = await _context.Appointments
                .Where(a => a.PatientId == patientId)
                .Include(a => a.Doctor).ThenInclude(d => d!.User)
                .Include(a => a.HealthHistory) // <-- EKLENDİ
                .OrderByDescending(a => a.Date).ThenBy(a => a.Start) // Tarihe göre tersten sıralamak daha iyidir
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
        public async Task<ApiResponse<Appointment>> CompleteAppointmentAsync(int userId, int appointmentId, CompleteAppointmentDto dto)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null) return new ApiResponse<Appointment>(404, "Randevu bulunamadı.");

            // Yetki kontrolü: Sadece randevunun doktoru tamamlayabilir
            if (appointment.Doctor?.UserId != userId)
                return new ApiResponse<Appointment>(403, "Bu işlem için yetkiniz yok.");

            if (appointment.Status != "booked" && appointment.Status != "confirmed")
                return new ApiResponse<Appointment>(400, "Sadece aktif randevular tamamlanabilir.");

            // Transaction (İşlem bütünlüğü) başlatıyoruz
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Randevu durumunu güncelle
                appointment.Status = "completed";

                // 2. Sağlık Geçmişi (HealthHistory) oluştur
                var history = new HealthHistory
                {
                    PatientId = appointment.PatientId,
                    AppointmentId = appointment.Id, // İlişki kuruldu
                    DoctorId = appointment.DoctorId, // Modelde varsa
                    Diagnosis = dto.Diagnosis,
                    Treatment = dto.Treatment,
                    Notes = dto.Notes,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.HealthHistories.AddAsync(history);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // Geriye güncel veriyi döndür (HealthHistory dahil)
                appointment.HealthHistory = history;
                return new ApiResponse<Appointment>(200, "Randevu başarıyla tamamlandı ve kayıt oluşturuldu.", appointment);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ApiResponse<Appointment>(500, "İşlem sırasında hata oluştu: " + ex.Message);
            }
        }
    }
}