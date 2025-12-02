using System;
using System.Collections.Generic;

namespace TMD.Models;

public partial class ChatMessage
{
    public int MessageId { get; set; }

    public int ConversationId { get; set; }

    public int SenderId { get; set; }

    public string MessageContent { get; set; } = null!;

    public DateTime? SentAt { get; set; }

    public bool? IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public bool? IsDeleted { get; set; }

    public string? AttachmentUrl { get; set; }

    public string? AttachmentType { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;
}
