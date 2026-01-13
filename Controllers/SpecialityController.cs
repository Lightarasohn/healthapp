using healthapp.DTOs.SpecialityDTOs;
using healthapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace healthapp.Controllers
{
    [ApiController]
    [Route("api/specialities")]
    public class SpecialityController : ControllerBase
    {
        private readonly ISpecialityRepository _specialityRepository;

        public SpecialityController(ISpecialityRepository specialityRepository)
        {
            _specialityRepository = specialityRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var response = await _specialityRepository.GetAllSpecialitiesAsync();
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _specialityRepository.GetSpecialityByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<IActionResult> Add([FromBody] CreateSpecialityDto dto)
        {
            var response = await _specialityRepository.AddSpecialityAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        [Authorize(Roles = "admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateSpecialityDto dto)
        {
            var response = await _specialityRepository.UpdateSpecialityAsync(id, dto);
            return StatusCode(response.StatusCode, response);
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _specialityRepository.DeleteSpecialityAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}