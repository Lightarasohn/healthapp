using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace healthapp.DTOs.AppointmentDTOs
{
    public class BookedSlotsQueryDto
    {
        public int DoctorId { get; set; }
        public string Date { get; set; } = string.Empty!;
    }
}