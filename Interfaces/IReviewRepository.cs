using healthapp.DTOs;
using healthapp.DTOs.ReviewDTOs;
using healthapp.Models;

namespace healthapp.Interfaces
{
    public interface IReviewRepository
    {
        Task<ApiResponse<Review>> AddReviewAsync(int patientId, CreateReviewDto dto);
        Task<ApiResponse<IEnumerable<Review>>> GetReviewsByDoctorIdAsync(int doctorId);
        Task<ApiResponse<bool>> DeleteReviewAsync(int userId, string role, int reviewId); 
        Task<ApiResponse<Review>> UpdateReviewAsync(int userId, int reviewId, UpdateReviewDto dto);
    }
}