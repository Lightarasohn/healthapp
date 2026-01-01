using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace healthapp.DTOs.DoctorDTOs
{
    public class UpdateDoctorScheduleDto
    {
        public object Clocks { get; set; } = null!;
        public string? Location { get; set; }
        public decimal? ConsultationFee { get; set; }
    }
}