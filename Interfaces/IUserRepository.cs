using healthapp.DTOs;
using healthapp.DTOs.UserDTOs;
using healthapp.Models;

namespace healthapp.Interfaces
{
    public interface IUserRepository
    {
        // Auth
        Task<ApiResponse<object>> RegisterAsync(RegisterDto dto, string? documentPath);
        Task<ApiResponse<object>> LoginAsync(LoginDto dto);
        Task<ApiResponse<object>> VerifyEmailAsync(string token);
        Task<ApiResponse<object>> ResendVerificationEmailAsync(int userId); // EKLENDI
        Task<ApiResponse<object>> ForgotPasswordAsync(string email);
        Task<ApiResponse<object>> ResetPasswordAsync(string token, ResetPasswordDto dto);
        Task<ApiResponse<object>> ChangePasswordAsync(int userId, ChangePasswordDto dto); // EKLENDI
        Task<ApiResponse<object>> RefreshTokenAsync(RefreshTokenDto dto);
        Task<ApiResponse<object>> LogoutAsync(int userId, string refreshToken);
        
        // User
        Task<ApiResponse<User>> GetProfileAsync(int userId);
        Task<ApiResponse<User>> UpdateProfileAsync(int userId, UpdateProfileDto dto);
        Task<ApiResponse<bool>> DeleteAccountAsync(int userId); // EKLENDI
        
        // Favorites & History
        Task<ApiResponse<IEnumerable<Doctor>>> GetFavoriteDoctorsAsync(int userId);
        Task<ApiResponse<object>> AddFavoriteDoctorAsync(int userId, int doctorId);
        Task<ApiResponse<object>> RemoveFavoriteDoctorAsync(int userId, string doctorId);
        Task<ApiResponse<IEnumerable<HealthHistory>>> GetHealthHistoryAsync(int userId);
        Task<ApiResponse<object>> ConfirmEmailChangeAsync(string token);
    }
}