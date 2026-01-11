using healthapp.Context;
using healthapp.DTOs;
using healthapp.DTOs.ReviewDTOs;
using healthapp.Interfaces;
using healthapp.Models;
using Microsoft.EntityFrameworkCore;

namespace healthapp.Repositories
{
    public class ReviewRepository : IReviewRepository
    {
        private readonly PostgresContext _context;

        public ReviewRepository(PostgresContext context) => _context = context;

        // --- YENİ EKLENEN METOTLAR ---
        public async Task<ApiResponse<Review>> UpdateReviewAsync(int userId, int reviewId, UpdateReviewDto dto)
        {
            var review = await _context.Reviews.FindAsync(reviewId);
            if (review == null) return new ApiResponse<Review>(404, "Yorum bulunamadı");
            if (review.PatientId != userId) return new ApiResponse<Review>(403, "Yetkisiz işlem");

            review.Rating = dto.Rating;
            if (dto.Comment != null) review.Comment = dto.Comment;
            
            await _context.SaveChangesAsync();
            return new ApiResponse<Review>(200, "Yorum güncellendi", review);
        }

        public async Task<ApiResponse<bool>> DeleteReviewAsync(int userId, string role, int reviewId)
        {
            var review = await _context.Reviews.FindAsync(reviewId);
            if (review == null) return new ApiResponse<bool>(404, "Bulunamadı");

            // Admin veya yorum sahibi silebilir
            if (role != "admin" && review.PatientId != userId)
                return new ApiResponse<bool>(403, "Yetkisiz");

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
            return new ApiResponse<bool>(200, "Silindi", true);
        }

        // --- MEVCUT METOTLAR ---
        public async Task<ApiResponse<Review>> AddReviewAsync(int patientId, CreateReviewDto dto)
        {
            if (await _context.Reviews.AnyAsync(r => r.DoctorId == dto.DoctorId && r.PatientId == patientId && !r.Deleted))
                return new ApiResponse<Review>(400, "Zaten yorum yapılmış");

            var review = new Review { DoctorId = dto.DoctorId, PatientId = patientId, Rating = dto.Rating, Comment = dto.Comment, CreatedAt = DateTime.UtcNow };
            var doctor = await _context.Doctors.Include(d => d.Reviews).FirstOrDefaultAsync(d => d.Id == dto.DoctorId && !d.Deleted);
            doctor!.ReviewCount++;
            doctor!.Rating = (doctor.Reviews.Sum(x => x.Rating) + review.Rating) / doctor.ReviewCount;
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();
            return new ApiResponse<Review>(201, "Eklendi", review);
        }

        public async Task<ApiResponse<IEnumerable<Review>>> GetReviewsByDoctorIdAsync(int doctorId) =>
            new ApiResponse<IEnumerable<Review>>(200, "Liste", await _context.Reviews.AsNoTracking().Where(r => r.DoctorId == doctorId).Include(r => r.Patient).OrderByDescending(r => r.CreatedAt).ToListAsync());
    }
}