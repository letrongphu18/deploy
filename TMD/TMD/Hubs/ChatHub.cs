using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TMD.Models;
using System.Threading.Tasks;

namespace TMD.Hubs
{
	public class ChatHub : Hub
	{
		private readonly AihubSystemContext _context;
		private static readonly Dictionary<int, string> _userConnections = new();
		private static readonly Dictionary<int, DateTime> _typingUsers = new();

		public ChatHub(AihubSystemContext context)
		{
			_context = context;
		}

		// ✅ HELPER: GET USER ID FROM SESSION
		private int? GetCurrentUserId()
		{
			var httpContext = Context.GetHttpContext();
			if (httpContext == null) return null;

			var userIdStr = httpContext.Session.GetString("UserId");
			if (string.IsNullOrEmpty(userIdStr)) return null;

			if (int.TryParse(userIdStr, out int userId))
			{
				return userId;
			}

			return null;
		}

		public override async System.Threading.Tasks.Task OnConnectedAsync()
		{
			var userId = GetCurrentUserId();

			if (userId.HasValue)
			{
				_userConnections[userId.Value] = Context.ConnectionId;
				Console.WriteLine($"✅ User {userId.Value} connected with ConnectionId: {Context.ConnectionId}");

				// Notify all users that this user is online
				await Clients.All.SendAsync("UserOnline", userId.Value);
			}
			else
			{
				Console.WriteLine($"⚠️ Anonymous connection: {Context.ConnectionId}");
			}

			await base.OnConnectedAsync();
		}

		public override async System.Threading.Tasks.Task OnDisconnectedAsync(Exception? exception)
		{
			var userId = _userConnections.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
			if (userId > 0)
			{
				_userConnections.Remove(userId);
				_typingUsers.Remove(userId);

				Console.WriteLine($"❌ User {userId} disconnected");

				// Notify all users that this user is offline
				await Clients.All.SendAsync("UserOffline", userId);
			}

			await base.OnDisconnectedAsync(exception);
		}

		// ✅ SEND MESSAGE - FIXED
		public async System.Threading.Tasks.Task SendMessage(int receiverId, string message, string? attachmentUrl = null, string? attachmentType = null)
		{
			var senderId = GetCurrentUserId();

			if (!senderId.HasValue)
			{
				Console.WriteLine("❌ SendMessage failed: No userId in session");
				await Clients.Caller.SendAsync("Error", "Không thể xác định người gửi. Vui lòng đăng nhập lại.");
				return;
			}

			Console.WriteLine($"📤 SendMessage: From {senderId.Value} to {receiverId}, Message: {message}");

			try
			{
				// Get or create conversation
				var conversation = await GetOrCreateConversation(senderId.Value, receiverId);

				// Save message to database
				var chatMessage = new ChatMessage
				{
					ConversationId = conversation.ConversationId,
					SenderId = senderId.Value,
					MessageContent = message,
					SentAt = DateTime.Now,
					IsRead = false,
					IsDeleted = false,
					AttachmentUrl = attachmentUrl,
					AttachmentType = attachmentType
				};

				_context.ChatMessages.Add(chatMessage);
				conversation.LastMessageAt = DateTime.Now;
				await _context.SaveChangesAsync();

				Console.WriteLine($"✅ Message saved to database: MessageId={chatMessage.MessageId}");

				// Get sender info
				var sender = await _context.Users.FindAsync(senderId.Value);

				// Prepare message data
				var messageData = new
				{
					messageId = chatMessage.MessageId,
					conversationId = conversation.ConversationId,
					senderId = senderId.Value,
					senderName = sender?.FullName ?? "Unknown",
					senderAvatar = sender?.Avatar ?? "/images/default-avatar.png",
					message = message,
					content = message, // ✅ ADD BOTH FOR COMPATIBILITY
					attachmentUrl = attachmentUrl,
					attachmentType = attachmentType,
					sentAt = chatMessage.SentAt,
					isRead = false
				};

				// ✅ Send to receiver if online
				if (_userConnections.TryGetValue(receiverId, out string? receiverConnectionId))
				{
					Console.WriteLine($"📨 Sending to receiver ConnectionId: {receiverConnectionId}");
					await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", messageData);
				}
				else
				{
					Console.WriteLine($"⚠️ Receiver {receiverId} is offline");
				}

				// ✅ Send confirmation to sender
				await Clients.Caller.SendAsync("MessageSent", messageData);
				Console.WriteLine($"✅ MessageSent confirmation sent to caller");

				// Clear typing indicator
				_typingUsers.Remove(senderId.Value);
				await NotifyTyping(receiverId, senderId.Value, false);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ SendMessage error: {ex.Message}");
				Console.WriteLine($"Stack trace: {ex.StackTrace}");
				await Clients.Caller.SendAsync("Error", $"Lỗi khi gửi tin nhắn: {ex.Message}");
			}
		}

		// ✅ MARK AS READ
		public async System.Threading.Tasks.Task MarkAsRead(int conversationId)
		{
			var userId = GetCurrentUserId();

			if (!userId.HasValue)
			{
				Console.WriteLine("❌ MarkAsRead failed: No userId in session");
				return;
			}

			try
			{
				var messages = await _context.ChatMessages
					.Where(m => m.ConversationId == conversationId
						&& m.SenderId != userId.Value
						&& m.IsRead == false)
					.ToListAsync();

				if (messages.Count > 0)
				{
					Console.WriteLine($"📖 Marking {messages.Count} messages as read in conversation {conversationId}");

					foreach (var msg in messages)
					{
						msg.IsRead = true;
						msg.ReadAt = DateTime.Now;
					}

					await _context.SaveChangesAsync();

					// Notify sender that messages were read
					var conversation = await _context.Conversations.FindAsync(conversationId);
					if (conversation != null)
					{
						int otherUserId = conversation.User1Id == userId.Value ? conversation.User2Id : conversation.User1Id;

						if (_userConnections.TryGetValue(otherUserId, out string? connectionId))
						{
							await Clients.Client(connectionId).SendAsync("MessagesRead", conversationId, userId.Value);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ MarkAsRead error: {ex.Message}");
				await Clients.Caller.SendAsync("Error", $"Lỗi khi đánh dấu đã đọc: {ex.Message}");
			}
		}

		// ✅ TYPING INDICATOR
		public async System.Threading.Tasks.Task NotifyTyping(int receiverId, int senderId, bool isTyping)
		{
			if (isTyping)
			{
				_typingUsers[senderId] = DateTime.Now;
			}
			else
			{
				_typingUsers.Remove(senderId);
			}

			if (_userConnections.TryGetValue(receiverId, out string? connectionId))
			{
				await Clients.Client(connectionId).SendAsync("UserTyping", senderId, isTyping);
			}
		}

		// ✅ GET ONLINE STATUS
		public System.Threading.Tasks.Task<bool> IsUserOnline(int userId)
		{
			return System.Threading.Tasks.Task.FromResult(_userConnections.ContainsKey(userId));
		}

		// ✅ HELPER: GET OR CREATE CONVERSATION
		private async System.Threading.Tasks.Task<Conversation> GetOrCreateConversation(int user1Id, int user2Id)
		{
			int minId = Math.Min(user1Id, user2Id);
			int maxId = Math.Max(user1Id, user2Id);

			var conversation = await _context.Conversations
				.FirstOrDefaultAsync(c =>
					(c.User1Id == minId && c.User2Id == maxId) ||
					(c.User1Id == maxId && c.User2Id == minId));

			if (conversation == null)
			{
				conversation = new Conversation
				{
					User1Id = minId,
					User2Id = maxId,
					CreatedAt = DateTime.Now,
					LastMessageAt = DateTime.Now,
					IsArchived = false
				};

				_context.Conversations.Add(conversation);
				await _context.SaveChangesAsync();

				Console.WriteLine($"✅ Created new conversation: {conversation.ConversationId} between {user1Id} and {user2Id}");
			}

			return conversation;
		}

		// ✅ GET ALL ONLINE USERS (PUBLIC METHOD)
		public static List<int> GetOnlineUsers()
		{
			return _userConnections.Keys.ToList();
		}

		// ✅ GET ONLINE USER COUNT
		public System.Threading.Tasks.Task<int> GetOnlineUserCount()
		{
			return System.Threading.Tasks.Task.FromResult(_userConnections.Count);
		}

		// ✅ GET USER CONNECTION ID
		public System.Threading.Tasks.Task<string?> GetUserConnectionId(int userId)
		{
			_userConnections.TryGetValue(userId, out string? connectionId);
			return System.Threading.Tasks.Task.FromResult(connectionId);
		}
	}
}