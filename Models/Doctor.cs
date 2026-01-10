using System;
using System.Collections.Generic;

namespace healthapp.Models;

public partial class Doctor
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public int? Speciality { get; set; }

    public string? Hospital { get; set; }

    public string? About { get; set; }

    public int? Experience { get; set; }

    public string? Location { get; set; }

    public decimal? ConsultationFee { get; set; }

    public decimal? Rating { get; set; }

    public int? ReviewCount { get; set; }

    public string? Clocks { get; set; }

    public string? UnavailableDates { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool Deleted { get; set; }

    public string? Province { get; set; }

    public string? District { get; set; }

    public string? Neighborhood { get; set; }

    public string? FullLocation { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<HealthHistory> HealthHistories { get; set; } = new List<HealthHistory>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual Speciality? SpecialityNavigation { get; set; }

    public virtual User? User { get; set; }

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
