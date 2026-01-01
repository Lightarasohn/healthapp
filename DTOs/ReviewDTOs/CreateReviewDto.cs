namespace healthapp.DTOs.ReviewDTOs
{
    public class CreateReviewDto
    {
        public int DoctorId { get; set; }
        public int Rating { get; set; } // 1-5 arası
        public string? Comment { get; set; }
    }
}