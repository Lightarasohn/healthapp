using healthapp.DTOs;
using healthapp.DTOs.ReviewDTOs;
using healthapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace healthapp.Controllers
{
    [ApiController]
    [Route("api/reviews")]
    public class ReviewController : ControllerBase
    {
        private readonly IReviewRepository _reviewRepository;

        public ReviewController(IReviewRepository reviewRepository)
        {
            _reviewRepository = reviewRepository;
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReview(int id)
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var role = User.FindFirstValue(ClaimTypes.Role)!;
            var response = await _reviewRepository.DeleteReviewAsync(userId, role, id);
            return StatusCode(response.StatusCode, response);
        }

        [Authorize(Roles = "patient")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateReview(int id, [FromBody] UpdateReviewDto dto)
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _reviewRepository.UpdateReviewAsync(userId, id, dto);
            return StatusCode(response.StatusCode, response);
        }

        [Authorize(Roles = "patient")]
        [HttpPost]
        public async Task<IActionResult> AddReview(CreateReviewDto dto)
        {
            var userId = int.Parse(User.FindFirstValue("id")!);
            var response = await _reviewRepository.AddReviewAsync(userId, dto);
            return StatusCode(response.StatusCode, response);
        }

        [Authorize]
        [HttpGet("{doctorId}")]
        public async Task<IActionResult> GetReviews(int doctorId)
        {
            var response = await _reviewRepository.GetReviewsByDoctorIdAsync(doctorId);
            return Ok(response);
        }
    }
}