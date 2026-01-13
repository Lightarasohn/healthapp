using healthapp.DTOs;
using healthapp.DTOs.UserDTOs;
using healthapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace healthapp.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepository;

        public UserController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        [Authorize]
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _userRepository.ChangePasswordAsync(userId, dto);
            return StatusCode(response.StatusCode, response);
        }

        [Authorize]
        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification()
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _userRepository.ResendVerificationEmailAsync(userId);
            return StatusCode(response.StatusCode, response);
        }

        [Authorize]
        [HttpDelete("me")]
        public async Task<IActionResult> DeleteAccount()
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _userRepository.DeleteAccountAsync(userId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] dynamic data)
        {
            string email = data.GetProperty("email").GetString();
            var response = await _userRepository.ForgotPasswordAsync(email);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("reset-password/{token}")]
        public async Task<IActionResult> ResetPassword(string token, [FromBody] ResetPasswordDto dto)
        {
            var response = await _userRepository.ResetPasswordAsync(token, dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] RegisterDto dto, IFormFile? documents)
        {
            string? filePath = null;
            if (documents != null)
            {
                filePath = Path.Combine("Uploads", Guid.NewGuid() + Path.GetExtension(documents.FileName));
                using var stream = new FileStream(filePath, FileMode.Create);
                await documents.CopyToAsync(stream);
            }

            var response = await _userRepository.RegisterAsync(dto, filePath);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var response = await _userRepository.LoginAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _userRepository.GetProfileAsync(userId);
            return StatusCode(response.StatusCode, response);
        }

        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _userRepository.UpdateProfileAsync(userId, dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("confirm-email-change/{token}")]
        public async Task<IActionResult> ConfirmEmailChange(string token)
        {
            var response = await _userRepository.ConfirmEmailChangeAsync(token);
            return StatusCode(response.StatusCode, response);
        }

        [Authorize]
        [HttpGet("favorites")]
        public async Task<IActionResult> GetFavorites()
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _userRepository.GetFavoriteDoctorsAsync(userId);
            return Ok(response);
        }

        [Authorize]
        [HttpPost("favorites")]
        public async Task<IActionResult> AddFavorite([FromBody] int doctorId)
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _userRepository.AddFavoriteDoctorAsync(userId, doctorId);
            return Ok(response);
        }

        [Authorize]
        [HttpDelete("favorites/{doctorId}")]
        public async Task<IActionResult> RemoveFavorite(string doctorId)
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _userRepository.RemoveFavoriteDoctorAsync(userId, doctorId);
            return Ok(response);
        }

        [Authorize]
        [HttpGet("health-history")]
        public async Task<IActionResult> GetHistory()
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _userRepository.GetHealthHistoryAsync(userId);
            return Ok(response);
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            var response = await _userRepository.RefreshTokenAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("verify/{token}")]
        public async Task<IActionResult> VerifyEmail(string token)
        {
            var response = await _userRepository.VerifyEmailAsync(token);
            return StatusCode(response.StatusCode, response);
        }

        [Authorize]
        [HttpPost("verify-identity")]
        public async Task<IActionResult> VerifyIdentity([FromBody] VerifyIdentityDto dto)
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _userRepository.VerifyIdentityAsync(userId, dto.Tc);
            return StatusCode(response.StatusCode, response);
        }
    }
}