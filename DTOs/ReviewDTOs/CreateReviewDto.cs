namespace healthapp.DTOs.ReviewDTOs
{
    public class CreateReviewDto
    {
        public int DoctorId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}