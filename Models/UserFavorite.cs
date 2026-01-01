using System;
using System.Collections.Generic;

namespace healthapp.Models;

public partial class UserFavorite
{
    public int UserId { get; set; }

    public int DoctorId { get; set; }

    public virtual User User { get; set; } = null!;
}
