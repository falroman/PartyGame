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
            onGameStarted: () => {},
            onQuizStateUpdated: () => {},
            onAutoplayStatusUpdated: () => {},
            onBoosterActivated: () => {},
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

        this.connection.on('GameStarted', (gameSession) => {
            console.log('GameStarted:', gameSession);
            this.callbacks.onGameStarted(gameSession);
        });

        this.connection.on('QuizStateUpdated', (quizState) => {
            console.log('QuizStateUpdated:', quizState);
            this.callbacks.onQuizStateUpdated(quizState);
        });

        this.connection.on('AutoplayStatusUpdated', (status) => {
            console.log('AutoplayStatusUpdated:', status);
            this.callbacks.onAutoplayStatusUpdated(status);
        });

        this.connection.on('BoosterActivated', (event) => {
            console.log('BoosterActivated:', event);
            this.callbacks.onBoosterActivated(event);
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
    async joinRoom(roomCode, playerId, displayName, avatarPresetId = null) {
        if (!this.connection) {
            throw new Error('Not connected');
        }
        this.roomCode = roomCode.toUpperCase();
        this.playerId = playerId;
        this.isHost = false;
        
        // Store for reconnection
        localStorage.setItem('partyGame_displayName', displayName);
        if (avatarPresetId) {
            localStorage.setItem('partyGame_avatarPresetId', avatarPresetId);
        }
        
        await this.connection.invoke('JoinRoom', this.roomCode, playerId, displayName, avatarPresetId);
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
     * Set the locked state of the room (host only)
     */
    async setRoomLocked(isLocked) {
        if (!this.connection || !this.roomCode) {
            throw new Error('Not connected or no room');
        }
        if (!this.isHost) {
            throw new Error('Only the host can lock/unlock the room');
        }
        await this.connection.invoke('SetRoomLocked', this.roomCode, isLocked);
    }

    /**
     * Toggle the locked state of the room (host only)
     */
    async toggleRoomLocked(currentState) {
        await this.setRoomLocked(!currentState);
    }

    /**
     * Start the game (host only)
     * @param {string} gameType - The type of game to start (e.g., "Quiz")
     */
    async startGame(gameType = 'Quiz') {
        if (!this.connection || !this.roomCode) {
            throw new Error('Not connected or no room');
        }
        if (!this.isHost) {
            throw new Error('Only the host can start the game');
        }
        await this.connection.invoke('StartGame', this.roomCode, gameType);
    }

    /**
     * Submit answer for current quiz question (player only)
     * @param {string} optionKey - The selected option (A, B, C, D)
     */
    async submitAnswer(optionKey) {
        if (!this.connection || !this.roomCode) {
            throw new Error('Not connected or no room');
        }
        if (!this.playerId) {
            throw new Error('No player ID set');
        }
        await this.connection.invoke('SubmitAnswer', this.roomCode, this.playerId, optionKey);
    }

    /**
     * Submit dictionary answer (player only)
     * @param {number} optionIndex - The selected option index (0-3)
     */
    async submitDictionaryAnswer(optionIndex) {
        if (!this.connection || !this.roomCode) {
            throw new Error('Not connected or no room');
        }
        if (!this.playerId) {
            throw new Error('No player ID set');
        }
        await this.connection.invoke('SubmitDictionaryAnswer', this.roomCode, this.playerId, optionIndex);
    }

    /**
     * Submit ranking vote (player only)
     * @param {string} votedForPlayerId - The GUID of the player to vote for
     */
    async submitRankingVote(votedForPlayerId) {
        if (!this.connection || !this.roomCode) {
            throw new Error('Not connected or no room');
        }
        if (!this.playerId) {
            throw new Error('No player ID set');
        }
        await this.connection.invoke('SubmitRankingVote', this.roomCode, this.playerId, votedForPlayerId);
    }

    /**
     * Select category for current round (round leader only)
     * @param {string} category - The selected category name
     */
    async selectCategory(category) {
        if (!this.connection || !this.roomCode) {
            throw new Error('Not connected or no room');
        }
        if (!this.playerId) {
            throw new Error('No player ID set');
        }
        await this.connection.invoke('SelectCategory', this.roomCode, this.playerId, category);
    }

    /**
     * Advance to next question (host only)
     */
    async nextQuestion() {
        if (!this.connection || !this.roomCode) {
            throw new Error('Not connected or no room');
        }
        if (!this.isHost) {
            throw new Error('Only the host can advance the game');
        }
        await this.connection.invoke('NextQuestion', this.roomCode);
    }

    /**
     * Add server-side bots (host only)
     */
    async addBots(count = null) {
        if (!this.connection || !this.roomCode) {
            throw new Error('Not connected or no room');
        }
        if (!this.isHost) {
            throw new Error('Only the host can add bots');
        }
        await this.connection.invoke('AddBots', this.roomCode, count);
    }

    /**
     * Start autoplay loop (host only)
     */
    async startAutoplay(count = null) {
        if (!this.connection || !this.roomCode) {
            throw new Error('Not connected or no room');
        }
        if (!this.isHost) {
            throw new Error('Only the host can start autoplay');
        }
        await this.connection.invoke('StartAutoplay', this.roomCode, count);
    }

    /**
     * Stop autoplay loop (host only)
     */
    async stopAutoplay() {
        if (!this.connection || !this.roomCode) {
            throw new Error('Not connected or no room');
        }
        if (!this.isHost) {
            throw new Error('Only the host can stop autoplay');
        }
        await this.connection.invoke('StopAutoplay', this.roomCode);
    }

    /**
     * Reset room back to lobby (host only)
     * Keeps all players but clears game state
     */
    async resetToLobby() {
        if (!this.connection || !this.roomCode) {
            throw new Error('Not connected or no room');
        }
        if (!this.isHost) {
            throw new Error('Only the host can reset to lobby');
        }
        await this.connection.invoke('ResetToLobby', this.roomCode);
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

    /**
     * Activate the player's booster
     * @param {number} boosterType - The booster type enum value
     * @param {string|null} targetPlayerId - Optional target player ID (GUID string)
     */
    async activateBooster(boosterType, targetPlayerId = null) {
        if (!this.connection || !this.roomCode) {
            throw new Error('Not connected or no room');
        }
        if (!this.playerId) {
            throw new Error('No player ID set');
        }
        await this.connection.invoke('ActivateBooster', this.roomCode, this.playerId, boosterType, targetPlayerId);
    }
}

/**
 * Generate a UUID v4 (works in all browsers and non-secure contexts)
 * Fallback for when crypto.randomUUID() is not available
 */
function generateUUID() {
    // Try native crypto.randomUUID first (only works in secure contexts)
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
        try {
            return crypto.randomUUID();
        } catch (e) {
            // Fall through to fallback
        }
    }
    
    // Fallback using crypto.getRandomValues (works in all modern browsers)
    if (typeof crypto !== 'undefined' && typeof crypto.getRandomValues === 'function') {
        return ([1e7]+-1e3+-4e3+-8e3+-1e11).replace(/[018]/g, c =>
            (c ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> c / 4).toString(16)
        );
    }
    
    // Last resort fallback using Math.random (less secure but works everywhere)
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
        const r = Math.random() * 16 | 0;
        const v = c === 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
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
            playerId = generateUUID();
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
            'NOT_HOST': 'Only the host can perform this action.',
            'CONNECTION_FAILED': 'Failed to connect to server. Please try again.',
            'INVALID_STATE': 'This action cannot be performed in the current game state.',
            'NOT_ENOUGH_PLAYERS': 'At least 2 players are required to start the game.',
            'NOT_ROUND_LEADER': 'Only the round leader can select a category.',
            'INVALID_CATEGORY': 'Invalid category selection.',
            'ROUND_ALREADY_STARTED': 'The round has already started.',
            'FEATURE_DISABLED': 'This feature is disabled in the current environment.',
            'BOOSTER_NOT_OWNED': 'You don\'t have this booster.',
            'BOOSTER_ALREADY_USED': 'Your booster has already been used.',
            'BOOSTER_INVALID_PHASE': 'Booster cannot be used in this phase.',
            'BOOSTER_INVALID_TARGET': 'Invalid target for this booster.',
            'BOOSTER_INVALID': 'Cannot use booster right now.'
        };
        
        if (error && error.code && errorMessages[error.code]) {
            return errorMessages[error.code];
        }
        
        return error?.message || 'An unexpected error occurred.';
    },

    /**
     * Booster type enum mapping (matches BoosterType C# enum)
     */
    BoosterTypes: {
        DoublePoints: 0,
        FiftyFifty: 1,
        BackToZero: 2,
        Nope: 3,
        PositionSwitch: 4,
        LateLock: 5,
        Mirror: 6,
        ChaosMode: 8,
        Shield: 9
    },

    /**
     * Get booster emoji by type
     */
    getBoosterEmoji(boosterType) {
        const emojis = {
            0: '?', // DoublePoints
            1: '??', // FiftyFifty
            2: '??', // BackToZero
            3: '??', // Nope
            4: '??', // PositionSwitch
            5: '?', // LateLock
            6: '??', // Mirror
            8: '???', // ChaosMode
            9: '???'  // Shield
        };
        return emojis[boosterType] || '??';
    }
};

// Export for use in pages
window.GameClient = GameClient;
window.GameUtils = GameUtils;
