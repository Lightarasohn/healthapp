using healthapp.Context;
using healthapp.DTOs;
using healthapp.DTOs.DoctorDTOs;
using healthapp.Interfaces;
using healthapp.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace healthapp.Repositories
{
    public class DoctorRepository : IDoctorRepository
    {
        private readonly PostgresContext _context;

        public DoctorRepository(PostgresContext context) => _context = context;

        public async Task<ApiResponse<Doctor>> CreateDoctorAsync(int userId, CreateDoctorDto dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.Role != "doctor")
                return new ApiResponse<Doctor>(400, "Sadece doktor rolündeki kullanıcılar profil oluşturabilir.");

            if (user.IsDoctorApproved != true)
                return new ApiResponse<Doctor>(403, "Doktor profiliniz henüz admin tarafından onaylanmadı.");

            var doctor = new Doctor
            {
                UserId = userId,
                Speciality = dto.Speciality,
                Location = dto.Location,
                Clocks = JsonSerializer.Serialize(dto.Clocks),
                CreatedAt = DateTime.UtcNow,
                UnavailableDates = { }
            };

            await _context.Doctors.AddAsync(doctor);
            await _context.SaveChangesAsync();
            return new ApiResponse<Doctor>(201, "Doktor profili oluşturuldu", doctor);
        }

        public async Task<ApiResponse<object>> GetDoctorsBySpecialityAsync(DoctorFilterDto filter)
        {
            var query = _context.Doctors
                .AsNoTracking()
                .Include(d => d.User)
                .Include(d => d.SpecialityNavigation)
                .AsQueryable();

            if (filter.Speciality.HasValue)
                query = query.Where(d => d.Speciality == filter.Speciality);

            // --- YENİ FİLTRELER ---
            if (!string.IsNullOrWhiteSpace(filter.Province))
                query = query.Where(d => d.Province == filter.Province);

            if (!string.IsNullOrWhiteSpace(filter.District))
                query = query.Where(d => d.District == filter.District);

            if (!string.IsNullOrWhiteSpace(filter.Neighborhood))
                query = query.Where(d => EF.Functions.ILike(d.Neighborhood!, $"%{filter.Neighborhood}%"));
            // ---------------------

            if (filter.MinRating.HasValue)
                query = query.Where(d => d.Rating >= filter.MinRating);

            if (filter.MaxPrice.HasValue)
                query = query.Where(d => d.ConsultationFee <= filter.MaxPrice);

            if (filter.MinPrice.HasValue)
                query = query.Where(d => d.ConsultationFee >= filter.MinPrice);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                query = query.Where(d =>
                    d.User != null &&
                    (
                        EF.Functions.ILike(d.User.Name, $"%{filter.Search}%") ||
                        EF.Functions.ILike(d.Speciality.ToString()!, $"%{filter.Search}%")
                    )
                );
            }

            filter.Sort = filter.Sort?.ToLower();
            query = filter.Sort == "desc"
                ? query.OrderByDescending(d => d.User!.Name)
                : query.OrderBy(d => d.User!.Name);

            var page = filter.Page < 1 ? 1 : filter.Page;
            var limit = filter.Limit < 1 ? 12 : filter.Limit;

            var total = await query.CountAsync();
            var doctors = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            return new ApiResponse<object>(200, "Doktorlar listelendi", new
            {
                doctors,
                pagination = new
                {
                    total,
                    page,
                    pages = (int)Math.Ceiling((double)total / limit)
                }
            });
        }


        public async Task<ApiResponse<object>> GetDoctorReviewsAsync(int doctorId)
        {
            var reviews = await _context.Reviews
                .AsNoTracking()
                .Where(r => r.DoctorId == doctorId && !r.Deleted)
                .OrderByDescending(r => r.Rating)
                .ToListAsync();

            var totalReviews = reviews.Count;
            var averageRating = totalReviews > 0 ? reviews.Average(r => r.Rating) : 0;

            return new ApiResponse<object>(200, "Yorumlar getirildi", new { reviews, totalReviews, averageRating = Math.Round(averageRating, 1) });
        }

        public async Task<ApiResponse<Doctor>> UpdateDoctorScheduleAsync(int userId, UpdateDoctorScheduleDto dto)
        {
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor == null) return new ApiResponse<Doctor>(404, "Doktor bulunamadı");

            doctor.Clocks = JsonSerializer.Serialize(dto.Clocks);
            if (dto.ConsultationFee != null) doctor.ConsultationFee = dto.ConsultationFee;

            await _context.SaveChangesAsync();
            return new ApiResponse<Doctor>(200, "Çalışma saatleri güncellendi", doctor);
        }

        public async Task<ApiResponse<bool>> AddHealthHistoryAsync(AddHealthHistoryDto dto)
        {
            var history = new HealthHistory
            {
                PatientId = dto.PatientId,
                Diagnosis = dto.Diagnosis,
                Treatment = dto.Treatment,
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow
            };

            await _context.HealthHistories.AddAsync(history);
            await _context.SaveChangesAsync();
            return new ApiResponse<bool>(200, "Sağlık geçmişi eklendi", true);
        }

        public async Task<ApiResponse<Doctor>> GetDoctorByIdAsync(int id)
        {
            var doctor = await _context.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id);
            return doctor == null ? new ApiResponse<Doctor>(404, "Doktor bulunamadı") : new ApiResponse<Doctor>(200, "Doktor bulundu", doctor);
        }

        public async Task<ApiResponse<IEnumerable<Doctor>>> GetDoctorsByMaxRatingAsync()
        {
            // Review'lara göre en yüksek rating'li doktorları sırala
            var doctors = await _context.Doctors
                .AsNoTracking()
                .OrderByDescending(d => d.Rating)
                .Take(10)
                .ToListAsync();
            return new ApiResponse<IEnumerable<Doctor>>(200, "Popüler doktorlar", doctors);
        }

        public async Task<ApiResponse<Doctor>> GetMyDoctorProfileAsync(int userId)
        {
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            return doctor == null ? new ApiResponse<Doctor>(404, "Profil bulunamadı") : new ApiResponse<Doctor>(200, "Profil getirildi", doctor);
        }

        public async Task<ApiResponse<bool>> AddUnavailableDateAsync(int userId, UnavailableDateDto dto)
        {
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor == null) return new ApiResponse<bool>(404, "Doktor bulunamadı");

            // Mevcut JSON'ı Dictionary olarak deserialize et
            var datesDict = string.IsNullOrEmpty(doctor.UnavailableDates) || doctor.UnavailableDates == "[]"
                ? new Dictionary<string, UnavailableDateDetail>()
                : JsonSerializer.Deserialize<Dictionary<string, UnavailableDateDetail>>(doctor.UnavailableDates);

            if (datesDict == null) datesDict = new Dictionary<string, UnavailableDateDetail>();

            // Benzersiz bir Key oluştur (Örn: leave_638123123)
            // DateTime.Ticks kullanarak sıralı ve benzersiz olmasını sağlıyoruz.
            string uniqueKey = $"leave_{DateTime.UtcNow.Ticks}";

            var newDateDetail = new UnavailableDateDetail
            {
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Reason = dto.Reason,
                IsDeleted = false
            };

            datesDict.Add(uniqueKey, newDateDetail);

            doctor.UnavailableDates = JsonSerializer.Serialize(datesDict);
            await _context.SaveChangesAsync();
            return new ApiResponse<bool>(200, "İzin eklendi", true);
        }

        public async Task<ApiResponse<bool>> CancelUnavailableDateAsync(int userId, string dateKey)
        {
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor == null) return new ApiResponse<bool>(404, "Doktor bulunamadı");

            var datesDict = string.IsNullOrEmpty(doctor.UnavailableDates)
                ? new Dictionary<string, UnavailableDateDetail>()
                : JsonSerializer.Deserialize<Dictionary<string, UnavailableDateDetail>>(doctor.UnavailableDates);

            if (datesDict == null || !datesDict.ContainsKey(dateKey))
                return new ApiResponse<bool>(404, "Belirtilen izin kaydı bulunamadı");

            var targetDate = datesDict[dateKey];

            // VALIDATION: Bitiş tarihi geçmişse silinemez
            if (targetDate.EndDate < DateTime.UtcNow)
            {
                return new ApiResponse<bool>(400, "Geçmiş tarihli bir izin iptal edilemez.");
            }

            if (targetDate.IsDeleted)
            {
                return new ApiResponse<bool>(400, "Bu izin zaten iptal edilmiş.");
            }

            // Soft Delete işlemi
            targetDate.IsDeleted = true;

            // Objeyi güncelle ve kaydet
            datesDict[dateKey] = targetDate;
            doctor.UnavailableDates = JsonSerializer.Serialize(datesDict);

            await _context.SaveChangesAsync();
            return new ApiResponse<bool>(200, "İzin iptal edildi", true);
        }

        public async Task<ApiResponse<bool>> DeleteDoctorAsync(int id)
        {
            var doctor = await _context.Doctors.FindAsync(id);
            if (doctor == null) return new ApiResponse<bool>(404, "Doktor bulunamadı");

            doctor.Deleted = true;
            await _context.SaveChangesAsync();
            return new ApiResponse<bool>(200, "Doktor silindi", true);
        }
        public async Task<ApiResponse<Doctor>> UpdateDoctorInfoAsync(int userId, UpdateDoctorInfoDto dto)
        {
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor == null) return new ApiResponse<Doctor>(404, "Doktor profili bulunamadı");

            if (dto.Speciality.HasValue) doctor.Speciality = dto.Speciality;
            if (!string.IsNullOrEmpty(dto.Hospital)) doctor.Hospital = dto.Hospital;
            if (!string.IsNullOrEmpty(dto.About)) doctor.About = dto.About;
            if (dto.Experience.HasValue) doctor.Experience = dto.Experience.Value;

            // --- LOKASYON GÜNCELLEME MANTIĞI ---
            bool locationChanged = false;

            if (dto.Province != null) { doctor.Province = dto.Province; locationChanged = true; }
            if (dto.District != null) { doctor.District = dto.District; locationChanged = true; }
            if (dto.Neighborhood != null) { doctor.Neighborhood = dto.Neighborhood; locationChanged = true; }
            if (dto.Location != null) { doctor.Location = dto.Location; locationChanged = true; }

            if (locationChanged)
            {
                // Format: İstanbul/Bayrampaşa/Yıldırım (Mahallesi), Ekstra Tarif
                var locParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(doctor.Province)) locParts.Add(doctor.Province);
                if (!string.IsNullOrWhiteSpace(doctor.District)) locParts.Add(doctor.District);
                if (!string.IsNullOrWhiteSpace(doctor.Neighborhood)) locParts.Add(doctor.Neighborhood);

                string mainLoc = string.Join("/", locParts);

                if (!string.IsNullOrWhiteSpace(doctor.Location))
                {
                    doctor.FullLocation = $"{mainLoc}, {doctor.Location}";
                }
                else
                {
                    doctor.FullLocation = mainLoc;
                }
            }
            // -----------------------------------

            doctor.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return new ApiResponse<Doctor>(200, "Profil bilgileri güncellendi", doctor);
        }
    }
}