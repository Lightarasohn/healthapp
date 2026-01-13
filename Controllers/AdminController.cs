using healthapp.DTOs;
using healthapp.DTOs.AdminDTOs;
using healthapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace healthapp.Controllers
{
    [Authorize(Roles = "admin")]
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminRepository _adminRepository;

        public AdminController(IAdminRepository adminRepository)
        {
            _adminRepository = adminRepository;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var response = await _adminRepository.GetUserStatsAsync();
            return Ok(response);
        }

        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var response = await _adminRepository.GetUserByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPatch("users/{id}/role")]
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateRoleDto dto)
        {
            var response = await _adminRepository.UpdateUserRoleAsync(id, dto.Role);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("create-admin")]
        public async Task<IActionResult> CreateAdmin(CreateAdminDto dto)
        {
            var response = await _adminRepository.CreateAdminAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var response = await _adminRepository.GetAllUsersAsync();
            return Ok(response);
        }

        [HttpGet("users/role/{role}")]
        public async Task<IActionResult> GetUsersByRole(string role)
        {
            var response = await _adminRepository.GetUsersByRoleAsync(role);
            return Ok(response);
        }

        [HttpPatch("approve-doctor/{id}")]
        public async Task<IActionResult> ApproveDoctor(int id)
        {
            var response = await _adminRepository.ApproveDoctorAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("pending-doctors")]
        public async Task<IActionResult> GetPendingDoctors()
        {
            var response = await _adminRepository.GetPendingDoctorsAsync();
            return Ok(response);
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var response = await _adminRepository.DeleteUserAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("doctor-document/{userId}")]
        public async Task<IActionResult> DownloadDocument(int userId)
        {
            var (stream, contentType, fileName) = await _adminRepository.DownloadDoctorDocumentAsync(userId);
            if (stream == null) return NotFound(new ApiResponse<object>(404, "Belge bulunamadı."));

            return File(stream, contentType, fileName);
        }
    }
}