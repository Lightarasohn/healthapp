using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace healthapp.DTOs.UserDTOs
{
    public class RegisterDto
    {
        public required string Name { get; set; }
        public required string Email  { get; set; }
        public required string Password  { get; set; }
        public required string Role { get; set; } 
        public int? Speciality { get; set; }
        public string? Hospital { get; set; }
    }
}