using healthapp.DTOs;
using healthapp.DTOs.DoctorDTOs;
using healthapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace healthapp.Controllers
{
    [ApiController]
    [Route("api/doctors")]
    public class DoctorController : ControllerBase
    {
        private readonly IDoctorRepository _doctorRepository;

        public DoctorController(IDoctorRepository doctorRepository)
        {
            _doctorRepository = doctorRepository;
        }

        [Authorize(Roles = "doctor,admin")]
        [HttpPost]
        public async Task<IActionResult> Create(CreateDoctorDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue("id")!);
                var response = await _doctorRepository.CreateDoctorAsync(userId, dto);
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>(500, ex.Message));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDoctors([FromQuery] DoctorFilterDto filter)
        {
            try
            {
                var response = await _doctorRepository.GetDoctorsBySpecialityAsync(filter);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>(500, ex.Message));
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _doctorRepository.GetDoctorByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        [Authorize(Roles = "doctor")]
        [HttpPut("me/schedule")]
        public async Task<IActionResult> UpdateSchedule(UpdateDoctorScheduleDto dto)
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _doctorRepository.UpdateDoctorScheduleAsync(userId, dto);
            return Ok(response);
        }

        [Authorize(Roles = "doctor")]
        [HttpPost("health-history")]
        public async Task<IActionResult> AddHealthHistory(AddHealthHistoryDto dto)
        {
            var response = await _doctorRepository.AddHealthHistoryAsync(dto);
            return Ok(response);
        }

        [Authorize(Roles = "doctor")]
        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _doctorRepository.GetMyDoctorProfileAsync(userId);
            return StatusCode(response.StatusCode, response);
        }

        [Authorize(Roles = "doctor")]
        [HttpPost("me/unavailable")]
        public async Task<IActionResult> AddUnavailableDate(UnavailableDateDto dto)
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _doctorRepository.AddUnavailableDateAsync(userId, dto);
            return Ok(response);
        }

        [HttpGet("{id}/reviews")]
        public async Task<IActionResult> GetReviews(int id)
        {
            var response = await _doctorRepository.GetDoctorReviewsAsync(id);
            return Ok(response);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _doctorRepository.DeleteDoctorAsync(id);
            return Ok(response);
        }

        [Authorize(Roles = "doctor")]
        [HttpPut("me/info")]
        public async Task<IActionResult> UpdateInfo(UpdateDoctorInfoDto dto)
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _doctorRepository.UpdateDoctorInfoAsync(userId, dto);
            return Ok(response);
        }
    }
}