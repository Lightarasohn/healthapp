using System;
using System.Collections.Generic;

namespace healthapp.Models;

public partial class Appointment
{
    public int Id { get; set; }

    public int? DoctorId { get; set; }

    public int? PatientId { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly Start { get; set; }

    public TimeOnly End { get; set; }

    public string? Notes { get; set; }

    public string? Status { get; set; }

    public decimal? Price { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool Deleted { get; set; }

    public virtual Doctor? Doctor { get; set; }

    public virtual HealthHistory? HealthHistory { get; set; }

    public virtual User? Patient { get; set; }
}
