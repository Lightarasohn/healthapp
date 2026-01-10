using healthapp.DTOs;
using healthapp.DTOs.AppointmentDTOs;
using healthapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace healthapp.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/appointments")]
    public class AppointmentController : ControllerBase
    {
        private readonly IAppointmentRepository _repository;

        public AppointmentController(IAppointmentRepository repository)
        {
            _repository = repository;
        }

        [HttpGet("all")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAllAppointments()
        {
            var response = await _repository.GetAllAppointmentsAsync();
            return Ok(response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Reschedule(int id, [FromBody] RescheduleDto dto)
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _repository.RescheduleAppointmentAsync(userId, id, dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateAppointmentDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue("id")!);
                var response = await _repository.CreateAppointmentAsync(userId, dto);
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>(500, ex.Message));
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDetails(int id)
        {
            var response = await _repository.GetAppointmentDetailsAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var role = User.FindFirstValue(ClaimTypes.Role)!;
            var response = await _repository.CancelAppointmentAsync(userId, role, id);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("doctor")]
        [Authorize(Roles = "doctor")]
        public async Task<IActionResult> GetForDoctor()
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _repository.GetDoctorAppointmentsAsync(userId);
            return Ok(response);
        }

        [HttpGet("patient")]
        public async Task<IActionResult> GetForPatient()
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _repository.GetPatientAppointmentsAsync(userId);
            return Ok(response);
        }

        [HttpPatch("{id}/status")]
        [Authorize(Roles = "doctor")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDto dto)
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _repository.UpdateStatusAsync(userId, id, dto.Status);
            return StatusCode(response.StatusCode, response);
        }
        [HttpPost("{id}/complete")]
        [Authorize(Roles = "doctor")]
        public async Task<IActionResult> CompleteAppointment(int id, [FromBody] CompleteAppointmentDto dto)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value!);
            var result = await _repository.CompleteAppointmentAsync(userId, id, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}