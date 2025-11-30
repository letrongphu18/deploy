using System;
using System.Collections.Generic;

namespace AIHUBOS.Models;

public partial class VwPendingRequestsSummary
{
    public string RequestType { get; set; } = null!;

    public int? PendingCount { get; set; }
}
