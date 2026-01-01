using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using healthapp.DTOs;
using healthapp.DTOs.SpecialityDTOs;
using healthapp.Models;

namespace healthapp.Interfaces
{
    public interface ISpecialityRepository
    {
        Task<ApiResponse<IEnumerable<Speciality>>> GetAllSpecialitiesAsync();
        Task<ApiResponse<Speciality>> GetSpecialityByIdAsync(int id);
        Task<ApiResponse<Speciality>> AddSpecialityAsync(CreateSpecialityDto dto);
        Task<ApiResponse<Speciality>> UpdateSpecialityAsync(int id, UpdateSpecialityDto dto);
        Task<ApiResponse<bool>> DeleteSpecialityAsync(int id);
    }
}