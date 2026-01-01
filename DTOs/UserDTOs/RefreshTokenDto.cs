using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace healthapp.DTOs.UserDTOs
{
    public class RefreshTokenDto
    {
        public required string RefreshToken { get; set; }
    }
}