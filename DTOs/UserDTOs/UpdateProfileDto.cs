using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace healthapp.DTOs.UserDTOs
{
    public class UpdateProfileDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public  string? Avatar { get; set; }
    }
}