//using Microsoft.AspNetCore.SignalR;
//using Microsoft.EntityFrameworkCore;
//using AIHUBOS.Models;
//using System.Security.Claims;
//using TMD.Models;

//namespace AIHUBOS.Hubs;

//public class ChatHub : Hub
//{
//	private readonly AihubosContext _context;
//	private static readonly Dictionary<int, string> _userConnections = new();

//	public ChatHub(AihubosContext context)
//	{
//		_context = context;
//	}

//	// Khi user connect
//	public override async Task OnConnectedAsync()
//	{
//		var userId = GetUserId();
//		if (userId.HasValue)
//		{
//			_userConnections[userId.Value] = Context.ConnectionId;

//			// Notify others that user is online
//			await Clients.All.SendAsync("UserOnline", userId.Value);
//		}

//		await base.OnConnectedAsync();
//	}

//	// Khi user disconnect
//	public override async Task OnDisconnectedAsync(Exception? exception)
//	{
//		var userId = GetUserId();
//		if (userId.HasValue)
//		{
//			_userConnections.Remove(userId.Value);

//			// Notify others that user is offline
//			await Clients.All.SendAsync("UserOffline", userId.Value);
//		}

//		await base.OnDisconnectedAsync(exception);
//	}

//	// Gửi tin nhắn
//	public async Task SendMessage(int receiverId, string message, string? attachmentUrl = null, string? attachmentType = null)
//	{
//		try
//		{
//			var senderId = GetUserId();
//			if (!senderId.HasValue)
//			{
//				await Clients.Caller.SendAsync("Error", "Unauthorized");
//				return;
//			}

//			// Tìm hoặc tạo conversation
//			var conversation = await GetOrCreateConversation(senderId.Value, receiverId);

//			// Tạo message
//			var chatMessage = new ChatMessage
//			{
//				ConversationId = conversation.ConversationId,
//				SenderId = senderId.Value,
//				MessageContent = message,
//				SentAt = DateTime.Now,
//				IsRead = false,
//				AttachmentUrl = attachmentUrl,
//				AttachmentType = attachmentType
//			};

//			_context.ChatMessages.Add(chatMessage);

//			// Update LastMessageAt
//			conversation.LastMessageAt = DateTime.Now;

//			await _context.SaveChangesAsync();

//			// Load sender info
//			var sender = await _context.Users
//				.Where(u => u.UserId == senderId.Value)
//				.Select(u => new { u.UserId, u.FullName, u.Avatar })
//				.FirstOrDefaultAsync();

//			// Prepare message data
//			var messageData = new
//			{
//				messageId = chatMessage.MessageId,
//				conversationId = conversation.ConversationId,
//				senderId = senderId.Value,
//				senderName = sender?.FullName,
//				senderAvatar = sender?.Avatar,
//				receiverId = receiverId,
//				message = message,
//				sentAt = chatMessage.SentAt,
//				attachmentUrl = attachmentUrl,
//				attachmentType = attachmentType
//			};

//			// Gửi cho receiver (nếu online)
//			if (_userConnections.TryGetValue(receiverId, out var receiverConnectionId))
//			{
//				await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", messageData);
//			}

//			// Confirm cho sender
//			await Clients.Caller.SendAsync("MessageSent", messageData);
//		}
//		catch (Exception ex)
//		{
//			await Clients.Caller.SendAsync("Error", ex.Message);
//		}
//	}

//	// Đánh dấu tin nhắn đã đọc
//	public async Task MarkAsRead(int conversationId)
//	{
//		try
//		{
//			var userId = GetUserId();
//			if (!userId.HasValue) return;

//			var messages = await _context.ChatMessages
//				.Where(m => m.ConversationId == conversationId
//					&& m.SenderId != userId.Value
//					&& !m.IsRead)
//				.ToListAsync();

//			foreach (var msg in messages)
//			{
//				msg.IsRead = true;
//				msg.ReadAt = DateTime.Now;
//			}

//			await _context.SaveChangesAsync();

//			// Notify sender that messages were read
//			var senderIds = messages.Select(m => m.SenderId).Distinct();
//			foreach (var senderId in senderIds)
//			{
//				if (_userConnections.TryGetValue(senderId, out var connectionId))
//				{
//					await Clients.Client(connectionId).SendAsync("MessagesRead", conversationId, userId.Value);
//				}
//			}
//		}
//		catch (Exception ex)
//		{
//			await Clients.Caller.SendAsync("Error", ex.Message);
//		}
//	}

//	// Typing indicator
//	public async Task Typing(int receiverId, bool isTyping)
//	{
//		var senderId = GetUserId();
//		if (!senderId.HasValue) return;

//		if (_userConnections.TryGetValue(receiverId, out var connectionId))
//		{
//			await Clients.Client(connectionId).SendAsync("UserTyping", senderId.Value, isTyping);
//		}
//	}

//	// Helper methods
//	private int? GetUserId()
//	{
//		var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//		if (int.TryParse(userIdClaim, out var userId))
//		{
//			return userId;
//		}
//		return null;
//	}

//	private async Task<Conversation> GetOrCreateConversation(int user1Id, int user2Id)
//	{
//		// Đảm bảo User1Id < User2Id
//		var minUserId = Math.Min(user1Id, user2Id);
//		var maxUserId = Math.Max(user1Id, user2Id);

//		var conversation = await _context.Conversations
//			.FirstOrDefaultAsync(c =>
//				(c.User1Id == minUserId && c.User2Id == maxUserId) ||
//				(c.User1Id == maxUserId && c.User2Id == minUserId));

//		if (conversation == null)
//		{
//			conversation = new Conversation
//			{
//				User1Id = minUserId,
//				User2Id = maxUserId,
//				CreatedAt = DateTime.Now
//			};

//			_context.Conversations.Add(conversation);
//			await _context.SaveChangesAsync();
//		}

//		return conversation;
//	}

//	// Check if user is online
//	public async Task CheckUserStatus(int userId)
//	{
//		var isOnline = _userConnections.ContainsKey(userId);
//		await Clients.Caller.SendAsync("UserStatus", userId, isOnline);
//	}
//}