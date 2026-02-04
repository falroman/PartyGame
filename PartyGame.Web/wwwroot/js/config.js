/**
 * PartyGame Configuration
 * 
 * This file contains the configuration for connecting to the game server.
 * Modify SERVER_URL to point to your PartyGame.Server instance.
 */

const GameConfig = {
    /**
     * The URL of the PartyGame Server (API + SignalR).
     * 
     * For local development with Visual Studio:
     * - Check the Server's launchSettings.json for the correct port
     * - Or check the Output window when running the Server
     * 
     * Examples:
     * - 'https://localhost:7213' (Visual Studio HTTPS)
     * - 'https://localhost:5001' (dotnet run HTTPS)
     * - '' (empty = same origin, for production or when Server serves the Web files)
     */
    SERVER_URL: 'https://localhost:7213',
    
    /**
     * Auto-detect server URL based on known development ports.
     * Returns empty string if running on same origin as server.
     */
    getServerUrl: function() {
        const currentPort = window.location.port;
        const currentHost = window.location.hostname;
        
        // If no port or standard ports, assume same origin
        if (!currentPort || currentPort === '80' || currentPort === '443') {
            return '';
        }
        
        // Known Web project ports -> map to Server ports
        const portMappings = {
            // dotnet run defaults
            '5002': 'https://localhost:5001',
            '5003': 'https://localhost:5001',
            // Visual Studio defaults (check your launchSettings.json)
            '7147': 'https://localhost:7213',
            '5041': 'https://localhost:7213',
        };
        
        if (portMappings[currentPort]) {
            console.log(`Detected Web port ${currentPort}, using Server URL: ${portMappings[currentPort]}`);
            return portMappings[currentPort];
        }
        
        // Fallback to configured SERVER_URL
        console.log(`Using configured SERVER_URL: ${this.SERVER_URL}`);
        return this.SERVER_URL;
    }
};

// Export for use in pages
window.GameConfig = GameConfig;
