using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace healthapp.DTOs.DoctorDTOs
{
    public class AddHealthHistoryDto
    {
        public int PatientId { get; set; }
        public string Diagnosis { get; set; } = null!;
        public string Treatment { get; set; } = null!;
        public string? Notes { get; set; }
    }
}