using healthapp.DTOs;
using healthapp.DTOs.DoctorDTOs;
using healthapp.Models;

namespace healthapp.Interfaces
{
    public interface IDoctorRepository
    {
        Task<ApiResponse<Doctor>> CreateDoctorAsync(int userId, CreateDoctorDto dto);
        Task<ApiResponse<Doctor>> GetDoctorByIdAsync(int id);
        Task<ApiResponse<object>> GetDoctorsBySpecialityAsync(DoctorFilterDto filter);
        Task<ApiResponse<object>> GetDoctorReviewsAsync(int doctorId);
        Task<ApiResponse<IEnumerable<Doctor>>> GetDoctorsByMaxRatingAsync();
        Task<ApiResponse<Doctor>> UpdateDoctorScheduleAsync(int userId, UpdateDoctorScheduleDto dto);
        Task<ApiResponse<bool>> AddHealthHistoryAsync(AddHealthHistoryDto dto);
        Task<ApiResponse<Doctor>> GetMyDoctorProfileAsync(int userId);
        Task<ApiResponse<bool>> AddUnavailableDateAsync(int userId, UnavailableDateDto dto);
        Task<ApiResponse<bool>> CancelUnavailableDateAsync(int userId, string dateKey);
        Task<ApiResponse<bool>> DeleteDoctorAsync(int id);
        Task<ApiResponse<Doctor>> UpdateDoctorInfoAsync(int userId, UpdateDoctorInfoDto dto);
    }
}