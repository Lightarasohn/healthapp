using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace healthapp.DTOs.DoctorDTOs
{
    public class UnavailableDateDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Reason { get; set; }
    }
}