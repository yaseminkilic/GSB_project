using System;
using System.Collections.Generic;

namespace GSB_project.Domain.Entities;

public partial class Participant
{
    public int Id { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }
}
