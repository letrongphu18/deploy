// ============================================
// 🔔 REALTIME NOTIFICATION CLIENT
// ============================================

class NotificationManager {
    constructor() {
        this.connection = null;
        this.isConnected = false;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
    }

    // ✅ Initialize connection
    async init() {
        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl("/notificationHub", {
                    transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
                    skipNegotiation: false
                })
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .configureLogging(signalR.LogLevel.Information)
                .build();

            this.setupEventHandlers();
            await this.start();
        } catch (error) {
            console.error("❌ SignalR init error:", error);
        }
    }

    // ✅ Setup event listeners
    setupEventHandlers() {
        // 1. Receive notifications
        this.connection.on("ReceiveNotification", (notification) => {
            console.log("🔔 New notification:", notification);
            this.displayNotification(notification);
            this.updateUnreadCount();
        });

        // 2. Test response
        this.connection.on("TestResponse", (message) => {
            console.log("🧪 Test response:", message);
            alert("SignalR Test: " + message);
        });

        // 3. Connection state changes
        this.connection.onreconnecting((error) => {
            console.warn("⚠️ Reconnecting...", error);
            this.isConnected = false;
        });

        this.connection.onreconnected((connectionId) => {
            console.log("✅ Reconnected:", connectionId);
            this.isConnected = true;
            this.reconnectAttempts = 0;
        });

        this.connection.onclose((error) => {
            console.error("❌ Connection closed:", error);
            this.isConnected = false;
            this.attemptReconnect();
        });
    }

    // ✅ Start connection
    async start() {
        try {
            await this.connection.start();
            this.isConnected = true;
            console.log("✅ SignalR connected:", this.connection.connectionId);

            // Test connection
            await this.testConnection();
        } catch (error) {
            console.error("❌ Start error:", error);
            setTimeout(() => this.start(), 5000);
        }
    }

    // ✅ Test connection
    async testConnection() {
        try {
            await this.connection.invoke("TestConnection", "Hello from client");
        } catch (error) {
            console.error("❌ Test connection failed:", error);
        }
    }

    // ✅ Reconnect logic
    async attemptReconnect() {
        if (this.reconnectAttempts < this.maxReconnectAttempts) {
            this.reconnectAttempts++;
            console.log(`🔄 Reconnect attempt ${this.reconnectAttempts}/${this.maxReconnectAttempts}`);
            setTimeout(() => this.start(), 3000 * this.reconnectAttempts);
        } else {
            console.error("❌ Max reconnect attempts reached");
        }
    }

    // ✅ Display notification (Bootstrap Toast)
    displayNotification(notification) {
        const toast = `
            <div class="toast" role="alert" data-bs-autohide="true" data-bs-delay="5000">
                <div class="toast-header bg-${this.getTypeClass(notification.type)}">
                    <i class="fas fa-bell me-2"></i>
                    <strong class="me-auto">${notification.title}</strong>
                    <small>${this.formatTime(notification.time)}</small>
                    <button type="button" class="btn-close" data-bs-dismiss="toast"></button>
                </div>
                <div class="toast-body">
                    ${notification.message}
                    ${notification.link ? `<a href="${notification.link}" class="d-block mt-2">Xem chi tiết →</a>` : ''}
                </div>
            </div>
        `;

        const container = document.getElementById('toast-container') || this.createToastContainer();
        container.insertAdjacentHTML('beforeend', toast);

        const toastElement = container.lastElementChild;
        const bsToast = new bootstrap.Toast(toastElement);
        bsToast.show();

        // Remove after hidden
        toastElement.addEventListener('hidden.bs.toast', () => toastElement.remove());
    }

    // ✅ Create toast container if not exists
    createToastContainer() {
        const container = document.createElement('div');
        container.id = 'toast-container';
        container.className = 'toast-container position-fixed top-0 end-0 p-3';
        container.style.zIndex = '9999';
        document.body.appendChild(container);
        return container;
    }

    // ✅ Update unread count badge
    async updateUnreadCount() {
        try {
            const response = await fetch('/Staff/GetMyNotifications?take=1');
            const data = await response.json();
            if (data.success) {
                const badge = document.getElementById('unread-count');
                if (badge && data.unreadCount > 0) {
                    badge.textContent = data.unreadCount;
                    badge.classList.remove('d-none');
                } else if (badge) {
                    badge.classList.add('d-none');
                }
            }
        } catch (error) {
            console.error("❌ Update count error:", error);
        }
    }

    // ✅ Helper methods
    getTypeClass(type) {
        const map = {
            'success': 'success',
            'error': 'danger',
            'warning': 'warning',
            'info': 'info'
        };
        return map[type] || 'info';
    }

    formatTime(time) {
        const date = new Date(time);
        const now = new Date();
        const diff = Math.floor((now - date) / 1000);

        if (diff < 60) return 'Vừa xong';
        if (diff < 3600) return `${Math.floor(diff / 60)} phút trước`;
        if (diff < 86400) return `${Math.floor(diff / 3600)} giờ trước`;
        return date.toLocaleDateString('vi-VN');
    }
}

// ✅ Auto-initialize when DOM ready
document.addEventListener('DOMContentLoaded', () => {
    if (typeof signalR !== 'undefined') {
        window.notificationManager = new NotificationManager();
        window.notificationManager.init();
    } else {
        console.error("❌ SignalR library not loaded!");
    }
});