using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMD.Models;
using TMD.Hubs;

namespace TMD.Controllers
{
	public class ChatController : Controller
	{
		private readonly AihubSystemContext _context;

		public ChatController(AihubSystemContext context)
		{
			_context = context;
		}

		// GET: /Chat/Index - Main chat page
		public IActionResult Index()
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (userId == null)
			{
				return RedirectToAction("Login", "Account");
			}

			return View();
		}

		// GET: /Chat/GetConversations - Get all conversations for current user
		[HttpGet]
		public async Task<IActionResult> GetConversations()
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (userId == null)
			{
				return Unauthorized(new { success = false, message = "Unauthorized" });
			}

			try
			{
				var conversations = await _context.Conversations
					.Where(c => c.User1Id == userId || c.User2Id == userId)
					.OrderByDescending(c => c.LastMessageAt)
					.Select(c => new
					{
						conversationId = c.ConversationId,
						otherUser = new
						{
							userId = c.User1Id == userId ? c.User2Id : c.User1Id,
							fullName = c.User1Id == userId ? c.User2.FullName : c.User1.FullName,
							avatar = c.User1Id == userId ? c.User2.Avatar : c.User1.Avatar,
							departmentName = c.User1Id == userId ? c.User2.Department.DepartmentName : c.User1.Department.DepartmentName,
							roleName = c.User1Id == userId ? c.User2.Role.RoleName : c.User1.Role.RoleName
						},
						lastMessage = c.ChatMessages
							.OrderByDescending(m => m.SentAt)
							.Select(m => new
							{
								senderId = m.SenderId,
								content = m.MessageContent,
								sentAt = m.SentAt
							})
							.FirstOrDefault(),
						unreadCount = c.ChatMessages
							.Count(m => m.SenderId != userId && m.IsRead == false),
						isOnline = ChatHub.GetOnlineUsers().Contains(c.User1Id == userId ? c.User2Id : c.User1Id)
					})
					.ToListAsync();

				return Json(new { success = true, conversations });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		// GET: /Chat/GetMessages?conversationId={id} - Get messages for a conversation
		[HttpGet]
		public async Task<IActionResult> GetMessages(int conversationId)
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (userId == null)
			{
				return Unauthorized(new { success = false, message = "Unauthorized" });
			}

			try
			{
				// Verify user is part of this conversation
				var conversation = await _context.Conversations
					.FirstOrDefaultAsync(c => c.ConversationId == conversationId &&
											(c.User1Id == userId || c.User2Id == userId));

				if (conversation == null)
				{
					return NotFound(new { success = false, message = "Conversation not found" });
				}

				var messages = await _context.ChatMessages
					.Where(m => m.ConversationId == conversationId && m.IsDeleted == false)
					.OrderBy(m => m.SentAt)
					.Select(m => new
					{
						messageId = m.MessageId,
						conversationId = m.ConversationId,
						senderId = m.SenderId,
						senderName = m.Sender.FullName,
						senderAvatar = m.Sender.Avatar,
						message = m.MessageContent,
						attachmentUrl = m.AttachmentUrl,
						attachmentType = m.AttachmentType,
						sentAt = m.SentAt,
						isRead = m.IsRead
					})
					.ToListAsync();

				return Json(new { success = true, messages });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		// GET: /Chat/SearchUsers?query={query} - Search users to start new chat
		[HttpGet]
		public async Task<IActionResult> SearchUsers(string query)
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (userId == null)
			{
				return Unauthorized(new { success = false, message = "Unauthorized" });
			}

			try
			{
				var users = await _context.Users
					.Where(u => u.UserId != userId &&
							   u.IsActive == true &&
							   (string.IsNullOrEmpty(query) ||
								u.FullName.Contains(query) ||
								u.Email.Contains(query) ||
								u.Username.Contains(query)))
					.OrderBy(u => u.FullName)
					.Select(u => new
					{
						userId = u.UserId,
						fullName = u.FullName,
						email = u.Email,
						avatar = u.Avatar,
						departmentName = u.Department != null ? u.Department.DepartmentName : "N/A",
						roleName = u.Role.RoleName,
						isOnline = ChatHub.GetOnlineUsers().Contains(u.UserId)
					})
					.Take(20)
					.ToListAsync();

				return Json(new { success = true, users });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		// POST: /Chat/StartConversation - Start a new conversation with a user
		[HttpPost]
		public async Task<IActionResult> StartConversation([FromBody] int receiverId)
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (userId == null)
			{
				return Unauthorized(new { success = false, message = "Unauthorized" });
			}

			try
			{
				int minId = Math.Min(userId.Value, receiverId);
				int maxId = Math.Max(userId.Value, receiverId);

				// Check if conversation already exists
				var existingConversation = await _context.Conversations
					.FirstOrDefaultAsync(c => (c.User1Id == minId && c.User2Id == maxId) ||
											(c.User1Id == maxId && c.User2Id == minId));

				if (existingConversation != null)
				{
					var otherUser = await _context.Users
						.Where(u => u.UserId == receiverId)
						.Select(u => new
						{
							userId = u.UserId,
							fullName = u.FullName,
							avatar = u.Avatar,
							isOnline = ChatHub.GetOnlineUsers().Contains(u.UserId)
						})
						.FirstOrDefaultAsync();

					return Json(new
					{
						success = true,
						conversationId = existingConversation.ConversationId,
						otherUser
					});
				}

				// Create new conversation
				var newConversation = new Conversation
				{
					User1Id = minId,
					User2Id = maxId,
					CreatedAt = DateTime.Now,
					LastMessageAt = DateTime.Now,
					IsArchived = false
				};

				_context.Conversations.Add(newConversation);
				await _context.SaveChangesAsync();

				var receiver = await _context.Users
					.Where(u => u.UserId == receiverId)
					.Select(u => new
					{
						userId = u.UserId,
						fullName = u.FullName,
						avatar = u.Avatar,
						isOnline = ChatHub.GetOnlineUsers().Contains(u.UserId)
					})
					.FirstOrDefaultAsync();

				return Json(new
				{
					success = true,
					conversationId = newConversation.ConversationId,
					otherUser = receiver
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		// GET: /Chat/GetUnreadCount - Get total unread messages count
		[HttpGet]
		public async Task<IActionResult> GetUnreadCount()
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (userId == null)
			{
				return Json(new { count = 0 });
			}

			try
			{
				var unreadCount = await _context.ChatMessages
					.Where(m => m.Conversation.User1Id == userId || m.Conversation.User2Id == userId)
					.Where(m => m.SenderId != userId && m.IsRead == false && m.IsDeleted == false)
					.CountAsync();

				return Json(new { count = unreadCount });
			}
			catch (Exception ex)
			{
				return Json(new { count = 0, error = ex.Message });
			}
		}

		// POST: /Chat/DeleteMessage - Delete a message (soft delete)
		[HttpPost]
		public async Task<IActionResult> DeleteMessage([FromBody] int messageId)
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (userId == null)
			{
				return Unauthorized(new { success = false, message = "Unauthorized" });
			}

			try
			{
				var message = await _context.ChatMessages
					.FirstOrDefaultAsync(m => m.MessageId == messageId && m.SenderId == userId);

				if (message == null)
				{
					return NotFound(new { success = false, message = "Message not found or unauthorized" });
				}

				message.IsDeleted = true;
				await _context.SaveChangesAsync();

				return Json(new { success = true, message = "Message deleted successfully" });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		// POST: /Chat/ArchiveConversation - Archive/Unarchive a conversation
		[HttpPost]
		public async Task<IActionResult> ArchiveConversation([FromBody] int conversationId)
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (userId == null)
			{
				return Unauthorized(new { success = false, message = "Unauthorized" });
			}

			try
			{
				var conversation = await _context.Conversations
					.FirstOrDefaultAsync(c => c.ConversationId == conversationId &&
											(c.User1Id == userId || c.User2Id == userId));

				if (conversation == null)
				{
					return NotFound(new { success = false, message = "Conversation not found" });
				}

				conversation.IsArchived = !conversation.IsArchived;
				await _context.SaveChangesAsync();

				return Json(new
				{
					success = true,
					isArchived = conversation.IsArchived,
					message = conversation.IsArchived.Value ? "Conversation archived" : "Conversation unarchived"
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		// GET: /Chat/GetOnlineUsers - Get list of all online users
		[HttpGet]
		public IActionResult GetOnlineUsers()
		{
			try
			{
				var onlineUserIds = ChatHub.GetOnlineUsers();

				var onlineUsers = _context.Users
					.Where(u => onlineUserIds.Contains(u.UserId))
					.Select(u => new
					{
						userId = u.UserId,
						fullName = u.FullName,
						avatar = u.Avatar,
						departmentName = u.Department != null ? u.Department.DepartmentName : "N/A",
						roleName = u.Role.RoleName
					})
					.ToList();

				return Json(new { success = true, users = onlineUsers });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		// GET: /Chat/GetConversationInfo?conversationId={id} - Get conversation details
		[HttpGet]
		public async Task<IActionResult> GetConversationInfo(int conversationId)
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (userId == null)
			{
				return Unauthorized(new { success = false, message = "Unauthorized" });
			}

			try
			{
				var conversation = await _context.Conversations
					.Where(c => c.ConversationId == conversationId &&
							  (c.User1Id == userId || c.User2Id == userId))
					.Select(c => new
					{
						conversationId = c.ConversationId,
						createdAt = c.CreatedAt,
						lastMessageAt = c.LastMessageAt,
						isArchived = c.IsArchived,
						otherUser = new
						{
							userId = c.User1Id == userId ? c.User2Id : c.User1Id,
							fullName = c.User1Id == userId ? c.User2.FullName : c.User1.FullName,
							email = c.User1Id == userId ? c.User2.Email : c.User1.Email,
							avatar = c.User1Id == userId ? c.User2.Avatar : c.User1.Avatar,
							departmentName = c.User1Id == userId ? c.User2.Department.DepartmentName : c.User1.Department.DepartmentName,
							roleName = c.User1Id == userId ? c.User2.Role.RoleName : c.User1.Role.RoleName,
							isOnline = ChatHub.GetOnlineUsers().Contains(c.User1Id == userId ? c.User2Id : c.User1Id)
						},
						messageCount = c.ChatMessages.Count(m => m.IsDeleted == false)
					})
					.FirstOrDefaultAsync();

				if (conversation == null)
				{
					return NotFound(new { success = false, message = "Conversation not found" });
				}

				return Json(new { success = true, conversation });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}
	}
}