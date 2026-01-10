using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace healthapp.DTOs.AppointmentDTOs
{
    public class CompleteAppointmentDto
    {
        public string Diagnosis { get; set; } = default!;
        public string Treatment { get; set; } = default!;
        public string? Notes { get; set; }
    }
}