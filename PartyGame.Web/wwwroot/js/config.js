/**
 * PartyGame Configuration
 * 
 * This file contains the configuration for connecting to the game server.
 * For LAN play, both Server and Web need to be accessible from phones on the same network.
 */

const GameConfig = {
    /**
     * Server port for LAN mode (when using 'lan' profile)
     */
    LAN_SERVER_PORT: 5000,
    
    /**
     * Server port for HTTPS LAN mode
     */
    LAN_SERVER_PORT_HTTPS: 5001,
    
    /**
     * Auto-detect server URL based on current host and known development ports.
     * When accessing via LAN IP, returns the same IP with the server port.
     */
    getServerUrl: function() {
        const currentHost = window.location.hostname;
        const currentPort = window.location.port;
        const currentProtocol = window.location.protocol;
        
        // If accessing via LAN IP address (not localhost)
        if (currentHost !== 'localhost' && currentHost !== '127.0.0.1') {
            // Use HTTP for LAN (HTTPS certificates are tricky on LAN)
            const serverPort = this.LAN_SERVER_PORT;
            const serverUrl = `http://${currentHost}:${serverPort}`;
            console.log(`LAN mode detected, using Server URL: ${serverUrl}`);
            return serverUrl;
        }
        
        // If no port or standard ports, assume same origin
        if (!currentPort || currentPort === '80' || currentPort === '443') {
            return '';
        }
        
        // Known Web project ports -> map to Server ports (localhost development)
        const portMappings = {
            // dotnet run defaults
            '5002': 'http://localhost:5000',
            '5003': 'http://localhost:5000',
            // Visual Studio defaults (check your launchSettings.json)
            '7147': 'https://localhost:7213',
            '5041': 'http://localhost:5253',
        };
        
        if (portMappings[currentPort]) {
            console.log(`Detected Web port ${currentPort}, using Server URL: ${portMappings[currentPort]}`);
            return portMappings[currentPort];
        }
        
        // Fallback: assume server is on port 5000/5001 on same host
        const fallbackUrl = currentProtocol === 'https:' 
            ? `https://${currentHost}:${this.LAN_SERVER_PORT_HTTPS}`
            : `http://${currentHost}:${this.LAN_SERVER_PORT}`;
        console.log(`Using fallback Server URL: ${fallbackUrl}`);
        return fallbackUrl;
    },
    
    /**
     * Get the URL that phones should use to join.
     * This returns the current origin, which works for both localhost and LAN.
     */
    getJoinBaseUrl: function() {
        return window.location.origin;
    },
    
    /**
     * Test if the server is reachable by calling the health endpoint.
     * Returns a promise that resolves with the health status or rejects with an error.
     */
    testServerConnection: async function() {
        const serverUrl = this.getServerUrl();
        const healthUrl = serverUrl + '/health';
        
        try {
            const response = await fetch(healthUrl, { 
                method: 'GET',
                mode: 'cors'
            });
            
            if (response.ok) {
                const data = await response.json();
                return { success: true, data, url: healthUrl };
            } else {
                return { success: false, error: `HTTP ${response.status}`, url: healthUrl };
            }
        } catch (error) {
            return { success: false, error: error.message, url: healthUrl };
        }
    }
};

// Export for use in pages
window.GameConfig = GameConfig;
