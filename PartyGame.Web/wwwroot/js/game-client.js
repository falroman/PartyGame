/**
 * PartyGame - SignalR Client Manager
 * Handles connection to the game hub and manages state
 */

class GameClient {
    constructor(serverUrl) {
        this.serverUrl = serverUrl || '';
        this.connection = null;
        this.roomCode = null;
        this.playerId = null;
        this.isHost = false;
        this.callbacks = {
            onConnected: () => {},
            onDisconnected: () => {},
            onReconnecting: () => {},
            onLobbyUpdated: () => {},
            onError: () => {}
        };
    }

    /**
     * Initialize SignalR connection
     */
    async connect() {
        const hubUrl = this.serverUrl + '/hub/game';
        
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Information)
            .build();

        // Set up event handlers
        this.connection.on('LobbyUpdated', (roomState) => {
            console.log('LobbyUpdated:', roomState);
            this.callbacks.onLobbyUpdated(roomState);
        });

        this.connection.on('Error', (error) => {
            console.error('Server error:', error);
            this.callbacks.onError(error);
        });

        this.connection.onreconnecting((error) => {
            console.log('Reconnecting...', error);
            this.callbacks.onReconnecting(error);
        });

        this.connection.onreconnected((connectionId) => {
            console.log('Reconnected:', connectionId);
            // Re-register after reconnect
            if (this.roomCode) {
                if (this.isHost) {
                    this.registerHost(this.roomCode);
                } else if (this.playerId) {
                    const name = localStorage.getItem('partyGame_displayName') || 'Player';
                    this.joinRoom(this.roomCode, this.playerId, name);
                }
            }
            this.callbacks.onConnected();
        });

        this.connection.onclose((error) => {
            console.log('Connection closed:', error);
            this.callbacks.onDisconnected(error);
        });

        // Start connection
        try {
            await this.connection.start();
            console.log('Connected to SignalR hub');
            this.callbacks.onConnected();
        } catch (error) {
            console.error('Failed to connect:', error);
            this.callbacks.onError({ code: 'CONNECTION_FAILED', message: 'Failed to connect to server' });
            throw error;
        }
    }

    /**
     * Register as host for a room
     */
    async registerHost(roomCode) {
        if (!this.connection) {
            throw new Error('Not connected');
        }
        this.roomCode = roomCode.toUpperCase();
        this.isHost = true;
        await this.connection.invoke('RegisterHost', this.roomCode);
    }

    /**
     * Join a room as a player
     */
    async joinRoom(roomCode, playerId, displayName) {
        if (!this.connection) {
            throw new Error('Not connected');
        }
        this.roomCode = roomCode.toUpperCase();
        this.playerId = playerId;
        this.isHost = false;
        
        // Store for reconnection
        localStorage.setItem('partyGame_displayName', displayName);
        
        await this.connection.invoke('JoinRoom', this.roomCode, playerId, displayName);
    }

    /**
     * Leave the current room
     */
    async leaveRoom() {
        if (!this.connection || !this.roomCode || !this.playerId) {
            return;
        }
        await this.connection.invoke('LeaveRoom', this.roomCode, this.playerId);
        this.roomCode = null;
    }

    /**
     * Disconnect from the hub
     */
    async disconnect() {
        if (this.connection) {
            await this.connection.stop();
        }
    }

    /**
     * Set callback functions
     */
    on(event, callback) {
        if (this.callbacks.hasOwnProperty(event)) {
            this.callbacks[event] = callback;
        }
    }
}

/**
 * Utility functions
 */
const GameUtils = {
    /**
     * Get or create a player ID (stored in localStorage)
     */
    getPlayerId() {
        let playerId = localStorage.getItem('partyGame_playerId');
        if (!playerId) {
            playerId = crypto.randomUUID();
            localStorage.setItem('partyGame_playerId', playerId);
        }
        return playerId;
    },

    /**
     * Get stored display name
     */
    getStoredName() {
        return localStorage.getItem('partyGame_displayName') || '';
    },

    /**
     * Get stored room code
     */
    getStoredRoomCode() {
        return localStorage.getItem('partyGame_roomCode') || '';
    },

    /**
     * Store room code
     */
    setStoredRoomCode(code) {
        if (code) {
            localStorage.setItem('partyGame_roomCode', code.toUpperCase());
        } else {
            localStorage.removeItem('partyGame_roomCode');
        }
    },

    /**
     * Create room via REST API
     */
    async createRoom(serverUrl) {
        const response = await fetch((serverUrl || '') + '/api/rooms', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
        });
        
        if (!response.ok) {
            throw new Error('Failed to create room');
        }
        
        return await response.json();
    },

    /**
     * Get room state via REST API
     */
    async getRoomState(serverUrl, roomCode) {
        const response = await fetch((serverUrl || '') + `/api/rooms/${roomCode}`);
        
        if (response.status === 404) {
            return null;
        }
        
        if (!response.ok) {
            throw new Error('Failed to get room state');
        }
        
        return await response.json();
    },

    /**
     * Generate QR code URL (using external service)
     */
    getQRCodeUrl(url, size = 200) {
        return `https://api.qrserver.com/v1/create-qr-code/?size=${size}x${size}&data=${encodeURIComponent(url)}`;
    },

    /**
     * Get initials from name for avatar
     */
    getInitials(name) {
        if (!name) return '?';
        const parts = name.trim().split(' ');
        if (parts.length >= 2) {
            return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
        }
        return name.substring(0, 2).toUpperCase();
    },

    /**
     * Format error message for display
     */
    formatError(error) {
        const errorMessages = {
            'ROOM_NOT_FOUND': 'Room not found. Please check the code and try again.',
            'ROOM_LOCKED': 'This room is locked and not accepting new players.',
            'ROOM_FULL': 'This room is full.',
            'NAME_INVALID': 'Please enter a valid name (1-20 characters).',
            'NAME_TAKEN': 'This name is already taken. Please choose another.',
            'ALREADY_HOST': 'You are already hosting another room.',
            'CONNECTION_FAILED': 'Failed to connect to server. Please try again.'
        };
        
        if (error && error.code && errorMessages[error.code]) {
            return errorMessages[error.code];
        }
        
        return error?.message || 'An unexpected error occurred.';
    }
};

// Export for use in pages
window.GameClient = GameClient;
window.GameUtils = GameUtils;
