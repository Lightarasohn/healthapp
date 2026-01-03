namespace healthapp.DTOs.DoctorDTOs
{
    public class DoctorFilterDto
    {
        public int? Speciality { get; set; }
        public string? Province { get; set; }
        public string? District { get; set; }
        public string? Neighborhood { get; set; }
        
        public decimal? MinRating { get; set; }
        public string? Search { get; set; }
        public string? Sort { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 12;
    }
}