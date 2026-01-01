namespace healthapp.DTOs.AdminDTOs
{
    public class CreateAdminDto
    {
        public required string Name { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
}