using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace healthapp.DTOs.DoctorDTOs
{
    public class DoctorFilterDto
    {
        public int? Speciality { get; set; }
        public string? Location { get; set; }
        public decimal? MinRating { get; set; }
        public string? Search { get; set; }
        public string? Sort { get; set; } // "asc" or "desc"
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 12;
    }
}