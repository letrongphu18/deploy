using System;
using System.Collections.Generic;
using AIHUBOS.Models;

namespace AIHUBOS.ViewModels
{
	public class UserListViewModel
	{
		public List<User> Users { get; set; } = new();
		public List<Role> Roles { get; set; } = new();
		public List<Department> Departments { get; set; } = new();

		// Paging
		public int Page { get; set; } = 1;
		public int PageSize { get; set; } = 10;
		public int TotalCount { get; set; }
		public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

		// Filters (giữ trạng thái giữa các lần request)
		public string? Search { get; set; }
		public string? RoleName { get; set; }
		public string? Status { get; set; }          // "active" / "inactive" / null
		public int? DepartmentId { get; set; }
	}
}
