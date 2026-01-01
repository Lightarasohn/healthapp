using System;
using System.Collections.Generic;

namespace healthapp.Models;

public partial class Review
{
    public int Id { get; set; }

    public int? DoctorId { get; set; }

    public int? PatientId { get; set; }

    public decimal Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool Deleted { get; set; }

    public virtual Doctor? Doctor { get; set; }

    public virtual User? Patient { get; set; }
}
