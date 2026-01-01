using healthapp.DTOs;
using healthapp.DTOs.AdminDTOs;
using healthapp.Models;

namespace healthapp.Interfaces
{
    public interface IAdminRepository
    {
        Task<ApiResponse<object>> CreateAdminAsync(CreateAdminDto dto);
        Task<ApiResponse<IEnumerable<User>>> GetAllUsersAsync();
        Task<ApiResponse<IEnumerable<User>>> GetUsersByRoleAsync(string role);
        Task<ApiResponse<User>> GetUserByIdAsync(int id); // EKLENDI
        Task<ApiResponse<User>> UpdateUserRoleAsync(int id, string role); // EKLENDI
        Task<ApiResponse<Doctor>> ApproveDoctorAsync(int userId);
        Task<ApiResponse<IEnumerable<Doctor>>> GetPendingDoctorsAsync();
        Task<ApiResponse<bool>> DeleteUserAsync(int id);
        Task<ApiResponse<object>> GetUserStatsAsync(); // EKLENDI
        Task<(Stream? FileStream, string ContentType, string FileName)> DownloadDoctorDocumentAsync(int userId);
    }
}