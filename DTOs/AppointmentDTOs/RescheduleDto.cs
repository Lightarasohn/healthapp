using System;

namespace healthapp.DTOs.AppointmentDTOs
{
    public class RescheduleDto
    {
        public DateOnly Date { get; set; }
        public TimeOnly Start { get; set; }
        public TimeOnly? End { get; set; }
    }
}