using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace healthapp.DTOs.AppointmentDTOs
{
    public class CreateAppointmentDto
    {
        public int DoctorId { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly Start { get; set; }
        public TimeOnly? End { get; set; }
        public string? Notes { get; set; }
    }
}