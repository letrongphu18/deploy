using System.Collections.Generic;
using AIHUBOS.Models;

namespace AIHUBOS.Models.ViewModels
{
	public class PendingRequestsViewModel
	{
		public List<OvertimeRequest> Overtime { get; set; } = new();
		public List<LeaveRequest> Leave { get; set; } = new();
		public List<LateRequest> Late { get; set; } = new();
		public string? SelectedType { get; set; }
	}
}
