using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace healthapp.DTOs.DoctorDTOs
{
    public class CreateDoctorDto
    {
        public int? Speciality { get; set; } = null!;
        public string? Location { get; set; }
        public object? Clocks { get; set; } // JSON formatında
    }
}