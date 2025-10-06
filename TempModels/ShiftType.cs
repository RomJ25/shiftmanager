using System;
using System.Collections.Generic;

namespace ShiftManager.TempModels;

public partial class ShiftType
{
    public int Id { get; set; }

    public string Key { get; set; } = null!;

    public string Start { get; set; } = null!;

    public string End { get; set; } = null!;
}
