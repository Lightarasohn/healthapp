using healthapp.Context;
using healthapp.DTOs;
using healthapp.DTOs.SpecialityDTOs;
using healthapp.Interfaces;
using healthapp.Models;
using Microsoft.EntityFrameworkCore;

namespace healthapp.Repositories
{
    public class SpecialityRepository : ISpecialityRepository
    {
        private readonly PostgresContext _context;

        public SpecialityRepository(PostgresContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<IEnumerable<Speciality>>> GetAllSpecialitiesAsync()
        {
            var list = await _context.Specialities.OrderBy(x => x.Name).AsNoTracking().ToListAsync();
            return new ApiResponse<IEnumerable<Speciality>>(200, "Uzmanlık alanları listelendi", list);
        }

        public async Task<ApiResponse<Speciality>> GetSpecialityByIdAsync(int id)
        {
            var speciality = await _context.Specialities.FindAsync(id);
            if (speciality == null)
                return new ApiResponse<Speciality>(404, "Uzmanlık alanı bulunamadı");

            return new ApiResponse<Speciality>(200, "Detay getirildi", speciality);
        }

        public async Task<ApiResponse<Speciality>> AddSpecialityAsync(CreateSpecialityDto dto)
        {

            if (await _context.Specialities.AnyAsync(s => s.Name.ToLower() == dto.Name.ToLower()))
                return new ApiResponse<Speciality>(400, "Bu uzmanlık alanı zaten mevcut");

            var speciality = new Speciality
            {
                Name = dto.Name
            };

            _context.Specialities.Add(speciality);
            await _context.SaveChangesAsync();

            return new ApiResponse<Speciality>(201, "Uzmanlık alanı başarıyla eklendi", speciality);
        }

        public async Task<ApiResponse<Speciality>> UpdateSpecialityAsync(int id, UpdateSpecialityDto dto)
        {
            var speciality = await _context.Specialities.FindAsync(id);
            if (speciality == null)
                return new ApiResponse<Speciality>(404, "Uzmanlık alanı bulunamadı");

            speciality.Name = dto.Name;

            await _context.SaveChangesAsync();
            return new ApiResponse<Speciality>(200, "Uzmanlık alanı güncellendi", speciality);
        }

        public async Task<ApiResponse<bool>> DeleteSpecialityAsync(int id)
        {
            var speciality = await _context.Specialities.FindAsync(id);
            if (speciality == null)
                return new ApiResponse<bool>(404, "Uzmanlık alanı bulunamadı");


            _context.Specialities.Remove(speciality);
            await _context.SaveChangesAsync();

            return new ApiResponse<bool>(200, "Uzmanlık alanı silindi", true);
        }
    }
}