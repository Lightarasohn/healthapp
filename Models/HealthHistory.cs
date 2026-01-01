using System;
using System.Collections.Generic;

namespace healthapp.Models;

public partial class HealthHistory
{
    public int Id { get; set; }

    public int? PatientId { get; set; }

    public string? Diagnosis { get; set; }

    public string? Treatment { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool Deleted { get; set; }

    public virtual User? Patient { get; set; }
}
