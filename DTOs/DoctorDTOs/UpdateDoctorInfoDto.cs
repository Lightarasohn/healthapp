namespace healthapp.DTOs.DoctorDTOs
{
    public class UpdateDoctorInfoDto
    {
        public int? Speciality { get; set; }
        public string? Hospital { get; set; }
        public string? About { get; set; }
        public int? Experience { get; set; }
    }
}