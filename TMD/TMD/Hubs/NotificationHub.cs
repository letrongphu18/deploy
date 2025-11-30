using Microsoft.AspNetCore.SignalR;

namespace AIHUBOS.Hubs
{
	public class NotificationHub : Hub
	{
		public override async Task OnConnectedAsync()
		{
			var httpContext = Context.GetHttpContext();

			if (httpContext?.Session == null)
			{
				Console.WriteLine($"⚠️ No session available for connection {Context.ConnectionId}");
				await base.OnConnectedAsync();
				return;
			}

			try
			{
				await httpContext.Session.LoadAsync();

				var userId = httpContext.Session.GetInt32("UserId");
				var role = httpContext.Session.GetString("RoleName");
				var deptId = httpContext.Session.GetInt32("DepartmentId");

				Console.WriteLine($"📡 New connection: {Context.ConnectionId}");
				Console.WriteLine($"   UserId: {userId}, Role: {role}, DeptId: {deptId}");

				// ✅ 1. USER GROUP (Personal notifications)
				if (userId.HasValue)
				{
					await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
					Console.WriteLine($"✅ Added to User_{userId}");
				}

				// ✅ 2. DYNAMIC ROLE GROUP (Works for ANY role)
				if (!string.IsNullOrEmpty(role))
				{
					await Groups.AddToGroupAsync(Context.ConnectionId, $"Role_{role}");
					Console.WriteLine($"✅ Added to Role_{role}");
				}

				// ✅ 3. DEPARTMENT GROUP (Optional)
				if (deptId.HasValue)
				{
					await Groups.AddToGroupAsync(Context.ConnectionId, $"Dept_{deptId}");
					Console.WriteLine($"✅ Added to Dept_{deptId}");
				}

				// ✅ 4. BROADCAST GROUP (All users)
				await Groups.AddToGroupAsync(Context.ConnectionId, "AllUsers");
				Console.WriteLine($"✅ Added to AllUsers");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ Error in OnConnectedAsync: {ex.Message}");
			}

			await base.OnConnectedAsync();
		}

		public override async Task OnDisconnectedAsync(Exception? exception)
		{
			Console.WriteLine($"❌ Disconnected: {Context.ConnectionId}");
			if (exception != null)
			{
				Console.WriteLine($"   Reason: {exception.Message}");
			}
			await base.OnDisconnectedAsync(exception);
		}

		// ✅ TEST METHOD
		public async Task TestConnection(string message)
		{
			Console.WriteLine($"🧪 Test message from {Context.ConnectionId}: {message}");
			await Clients.Caller.SendAsync("TestResponse", "Server received: " + message);
		}
	}
}