namespace healthapp.DTOs.DoctorDTOs
{
    public class UpdateDoctorInfoDto
    {
        public int? Speciality { get; set; }
        public string? Hospital { get; set; }
        public string? About { get; set; }
        public int? Experience { get; set; }
        public string? Province { get; set; }
        public string? District { get; set; }
        public string? Neighborhood { get; set; }
        public string? Location { get; set; }
    }
}