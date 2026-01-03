using System;
using System.Collections.Generic;

namespace healthapp.Models;

public partial class User
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public bool? IsVerified { get; set; }

    public string? Avatar { get; set; }

    public string? VerificationToken { get; set; }

    public bool? IsDoctorApproved { get; set; }

    public string? DoctorDocuments { get; set; }

    public List<string>? RefreshTokens { get; set; }

    public string? ResetPasswordToken { get; set; }

    public DateTime? ResetPasswordExpire { get; set; }

    public string? PendingEmail { get; set; }

    public string? PendingEmailToken { get; set; }

    public DateTime? PendingEmailTokenExpire { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool Deleted { get; set; }

    public string? Tc { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();

    public virtual ICollection<HealthHistory> HealthHistories { get; set; } = new List<HealthHistory>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<Doctor> DoctorsNavigation { get; set; } = new List<Doctor>();
}
