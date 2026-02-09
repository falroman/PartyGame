/**
 * Audio Manager - Howler.js based audio system for TV
 * Part of PartyGame Iteration 15 & 16
 * 
 * Provides:
 * - Phase-based background music
 * - Sound effects (boosters, countdown, buzzer)
 * - Victory/Finale audio
 * - Mute/unmute control
 * - Autoplay policy handling
 * 
 * Note: Gracefully degrades if audio files are missing
 */

class AudioManager {
    constructor() {
        this.isInitialized = false;
        this.isMuted = false;
        this.currentPhase = null;
        this.bgMusic = null;
        this.sfxSounds = {};
        this.musicVolume = 0.3;
        this.sfxVolume = 0.6;
        this.audioUnlocked = false;
        this.loadErrors = new Set();
        this.loadAttempts = new Set();
        
        // Countdown state
        this.lastPlayedSecond = -1;
        this.countdownEnabled = true;
        
        // Finale state
        this.finaleActive = false;
        this.victoryLoop = null;
        
        // Track if Howler is available
        this.howlerAvailable = false;
    }

    /**
     * Initialize the audio manager
     * Note: Actual sound loading happens after user interaction due to autoplay policy
     */
    init() {
        if (this.isInitialized) return true;

        // Check if Howler is available
        if (typeof Howl === 'undefined') {
            console.warn('AudioManager: Howler.js not loaded, audio disabled');
            this.howlerAvailable = false;
            return false;
        }

        this.howlerAvailable = true;
        
        // Setup sound effects (they load on demand)
        this._setupSounds();

        this.isInitialized = true;
        console.log('AudioManager: Initialized (waiting for user interaction to unlock audio)');
        
        return true;
    }

    /**
     * Setup all sound references
     */
    _setupSounds() {
        const basePath = '/assets/audio/';

        // Background music loops for each phase
        this.bgTracks = {
            intro: { src: `${basePath}intro_loop.mp3`, loop: true },
            categorySelection: { src: `${basePath}category_loop.mp3`, loop: true },
            question: { src: `${basePath}question_loop.mp3`, loop: true },
            answering: { src: `${basePath}tension_loop.mp3`, loop: true },
            reveal: { src: `${basePath}reveal_sting.mp3`, loop: false },
            scoreboard: { src: `${basePath}scoreboard_loop.mp3`, loop: true },
            finished: { src: `${basePath}victory_sting.mp3`, loop: false }
        };

        // Victory/Finale specific tracks
        this.victoryTracks = {
            sting: { src: `${basePath}victory_sting.mp3`, volume: 0.7 },
            loop: { src: `${basePath}victory_loop.mp3`, volume: 0.25, loop: true },
            applause: { src: `${basePath}applause.mp3`, volume: 0.4 }
        };

        // Sound effects
        this.sfxConfig = {
            tick: { src: `${basePath}tick.mp3`, volume: 0.5 },
            buzzer: { src: `${basePath}buzzer.mp3`, volume: 0.7 },
            booster: { src: `${basePath}booster.mp3`, volume: 0.6 },
            correct: { src: `${basePath}correct.mp3`, volume: 0.5 },
            wrong: { src: `${basePath}wrong.mp3`, volume: 0.4 },
            splash: { src: `${basePath}splash.mp3`, volume: 0.6 },
            fanfare: { src: `${basePath}fanfare.mp3`, volume: 0.6 },
            drumroll: { src: `${basePath}drumroll.mp3`, volume: 0.5 },
            tada: { src: `${basePath}tada.mp3`, volume: 0.6 }
        };
    }

    /**
     * Unlock audio context after user interaction
     * Call this from a click handler
     */
    unlock() {
        if (this.audioUnlocked) return;
        if (!this.howlerAvailable) return;

        try {
            // Create a silent sound to unlock audio context
            const silentSound = new Howl({
                src: ['data:audio/mp3;base64,SUQzBAAAAAAAI1RTU0UAAAAPAAADTGF2ZjU4Ljc2LjEwMAAAAAAAAAAAAAAA//tQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAWGluZwAAAA8AAAACAAABhgC7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7//////////////////////////////////////////////////////////////////8AAAAATGF2YzU4LjEzAAAAAAAAAAAAAAAAJAAAAAAAAAAAAYYoRwmHAAAAAAD/+9DEAAAH+ANoUAAAIv8AXQ4wBACAIAMN//+sREZG7v/h9Xd3d3cQBAEHd//+IAgCAIB8oc/+D4Pg+D4Ph9YPlDkJB8H/5Q5/gh//+c5znP8hyH//qqqqqgAAAAD/+9DEFQPAAADSAAAAIAAANIAAAAQAAAGkAAAAIAAANIAAAARMQU1FMy4xMDBVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV'],
                volume: 0,
                onend: () => {
                    console.log('AudioManager: Audio unlocked via silent sound');
                },
                onloaderror: () => {
                    // Fallback - still mark as unlocked
                    console.log('AudioManager: Audio unlock attempted (no fallback sound)');
                }
            });
            silentSound.play();
            this.audioUnlocked = true;
        } catch (error) {
            console.warn('AudioManager: Failed to unlock audio', error);
            // Still mark as unlocked to allow attempts
            this.audioUnlocked = true;
        }
    }

    /**
     * Check if a file has already failed to load
     */
    _hasLoadFailed(src) {
        return this.loadErrors.has(src);
    }

    /**
     * Mark a file as having failed to load
     */
    _markLoadFailed(src) {
        this.loadErrors.add(src);
        console.warn(`AudioManager: Marked ${src} as failed - won't retry`);
    }

    /**
     * Set the current game phase and play appropriate music
     * @param {string} phase - Phase name
     */
    setPhase(phase) {
        if (!this.isInitialized || !this.howlerAvailable || this.isMuted) return;
        
        // Normalize phase name
        const normalizedPhase = this._normalizePhase(phase);
        
        if (normalizedPhase === this.currentPhase) return;
        
        console.log(`AudioManager: Phase change ${this.currentPhase} -> ${normalizedPhase}`);
        this.currentPhase = normalizedPhase;
        
        // Reset countdown state on phase change
        this.lastPlayedSecond = -1;
        
        // Special handling for finale
        if (normalizedPhase === 'finished') {
            this._playFinaleAudio();
            return;
        }
        
        // Stop any finale audio if transitioning away
        if (this.finaleActive) {
            this._stopFinaleAudio();
        }
        
        // Fade out current music and play new track
        this._transitionMusic(normalizedPhase);
    }

    /**
     * Normalize phase to track name
     */
    _normalizePhase(phase) {
        const phaseMap = {
            0: 'categorySelection',
            1: 'question',
            2: 'answering',
            3: 'reveal',
            4: 'scoreboard',
            5: 'finished',
            6: 'question',  // DictionaryWord
            7: 'answering', // DictionaryAnswering
            8: 'question',  // RankingPrompt
            9: 'answering', // RankingVoting
            10: 'reveal',   // RankingReveal
            'CategorySelection': 'categorySelection',
            'Question': 'question',
            'Answering': 'answering',
            'Reveal': 'reveal',
            'Scoreboard': 'scoreboard',
            'Finished': 'finished',
            'DictionaryWord': 'question',
            'DictionaryAnswering': 'answering',
            'RankingPrompt': 'question',
            'RankingVoting': 'answering',
            'RankingReveal': 'reveal'
        };
        return phaseMap[phase] || 'categorySelection';
    }

    /**
     * Transition between music tracks
     */
    _transitionMusic(phase) {
        if (!this.howlerAvailable) return;
        
        const trackConfig = this.bgTracks[phase];
        if (!trackConfig) return;

        // Skip if we already know this track failed to load
        if (this._hasLoadFailed(trackConfig.src)) return;

        // Fade out current music
        if (this.bgMusic) {
            const oldMusic = this.bgMusic;
            try {
                if (typeof gsap !== 'undefined') {
                    gsap.to(oldMusic, {
                        volume: 0,
                        duration: 0.5,
                        onComplete: () => {
                            try { oldMusic.stop(); } catch (e) {}
                        }
                    });
                } else {
                    oldMusic.fade(oldMusic.volume(), 0, 500);
                    setTimeout(() => {
                        try { oldMusic.stop(); } catch (e) {}
                    }, 500);
                }
            } catch (e) {
                console.warn('AudioManager: Error fading out music', e);
            }
        }

        // Create and play new music
        try {
            this.bgMusic = new Howl({
                src: [trackConfig.src],
                loop: trackConfig.loop,
                volume: 0,
                html5: true, // Use HTML5 audio for better compatibility
                onloaderror: (id, error) => {
                    this._markLoadFailed(trackConfig.src);
                },
                onplayerror: (id, error) => {
                    console.warn(`AudioManager: Failed to play ${trackConfig.src} - ${error}`);
                }
            });

            this.bgMusic.play();

            // Fade in
            if (typeof gsap !== 'undefined') {
                gsap.to(this.bgMusic, {
                    volume: this.musicVolume,
                    duration: 0.5
                });
            } else {
                this.bgMusic.fade(0, this.musicVolume, 500);
            }
        } catch (error) {
            console.warn('AudioManager: Error creating music Howl', error);
        }
    }

    // ============================================
    // FINALE AUDIO (Iteration 16)
    // ============================================

    /**
     * Play finale/victory audio sequence
     */
    _playFinaleAudio() {
        if (!this.howlerAvailable || this.finaleActive) return;
        
        this.finaleActive = true;
        console.log('AudioManager: Starting finale audio sequence');

        // Stop current background music
        if (this.bgMusic) {
            try {
                if (typeof gsap !== 'undefined') {
                    gsap.to(this.bgMusic, {
                        volume: 0,
                        duration: 0.3,
                        onComplete: () => { try { this.bgMusic.stop(); } catch (e) {} }
                    });
                } else {
                    this.bgMusic.fade(this.bgMusic.volume(), 0, 300);
                    setTimeout(() => { try { this.bgMusic.stop(); } catch (e) {} }, 300);
                }
            } catch (e) {}
        }

        // Play victory sting
        this._playVictorySting();

        // Play applause after a short delay
        setTimeout(() => {
            if (this.finaleActive) {
                this._playApplause();
            }
        }, 300);

        // Start victory loop after sting (approximately)
        setTimeout(() => {
            if (this.finaleActive) {
                this._startVictoryLoop();
            }
        }, 2000);
    }

    /**
     * Play victory sting (one-shot)
     */
    _playVictorySting() {
        const config = this.victoryTracks.sting;
        if (this._hasLoadFailed(config.src)) return;

        try {
            const sting = new Howl({
                src: [config.src],
                volume: config.volume,
                html5: false,
                onloaderror: () => this._markLoadFailed(config.src)
            });
            sting.play();
        } catch (e) {
            console.warn('AudioManager: Error playing victory sting', e);
        }
    }

    /**
     * Start victory background loop
     */
    _startVictoryLoop() {
        const config = this.victoryTracks.loop;
        if (this._hasLoadFailed(config.src)) return;

        try {
            this.victoryLoop = new Howl({
                src: [config.src],
                volume: 0,
                loop: true,
                html5: true,
                onloaderror: () => this._markLoadFailed(config.src)
            });
            this.victoryLoop.play();

            // Fade in
            if (typeof gsap !== 'undefined') {
                gsap.to(this.victoryLoop, { volume: config.volume, duration: 1 });
            } else {
                this.victoryLoop.fade(0, config.volume, 1000);
            }
        } catch (e) {
            console.warn('AudioManager: Error starting victory loop', e);
        }
    }

    /**
     * Play applause sound effect
     */
    _playApplause() {
        const config = this.victoryTracks.applause;
        if (this._hasLoadFailed(config.src)) return;

        try {
            const applause = new Howl({
                src: [config.src],
                volume: config.volume,
                html5: true,
                onloaderror: () => this._markLoadFailed(config.src)
            });
            applause.play();
        } catch (e) {
            console.warn('AudioManager: Error playing applause', e);
        }
    }

    /**
     * Stop finale audio
     */
    _stopFinaleAudio() {
        this.finaleActive = false;
        
        if (this.victoryLoop) {
            try {
                if (typeof gsap !== 'undefined') {
                    gsap.to(this.victoryLoop, {
                        volume: 0,
                        duration: 0.5,
                        onComplete: () => { try { this.victoryLoop.stop(); this.victoryLoop = null; } catch (e) {} }
                    });
                } else {
                    this.victoryLoop.fade(this.victoryLoop.volume(), 0, 500);
                    setTimeout(() => { try { this.victoryLoop.stop(); this.victoryLoop = null; } catch (e) {} }, 500);
                }
            } catch (e) {}
        }
    }

    /**
     * Play finale audio (public method for manual triggering)
     */
    playFinale() {
        if (this.isMuted) return;
        this._playFinaleAudio();
    }

    /**
     * Stop finale audio (public method)
     */
    stopFinale() {
        this._stopFinaleAudio();
    }

    /**
     * Play a sound effect
     * @param {string} sfxName - Name of the sound effect
     */
    playSfx(sfxName) {
        if (!this.isInitialized || !this.howlerAvailable || this.isMuted) return;

        const config = this.sfxConfig[sfxName.toLowerCase()];
        if (!config) {
            console.warn(`AudioManager: Unknown SFX "${sfxName}"`);
            return;
        }

        // Skip if we already know this sound failed to load
        if (this._hasLoadFailed(config.src)) return;

        // Create sound on demand if not cached
        if (!this.sfxSounds[sfxName]) {
            try {
                this.sfxSounds[sfxName] = new Howl({
                    src: [config.src],
                    volume: config.volume * this.sfxVolume,
                    html5: false, // Use Web Audio for SFX for lower latency
                    onloaderror: (id, error) => {
                        this._markLoadFailed(config.src);
                        delete this.sfxSounds[sfxName]; // Remove failed sound
                    },
                    onplayerror: (id, error) => {
                        console.warn(`AudioManager: Failed to play SFX ${config.src}`);
                    }
                });
            } catch (error) {
                console.warn('AudioManager: Error creating SFX Howl', error);
                return;
            }
        }

        try {
            this.sfxSounds[sfxName].play();
        } catch (error) {
            console.warn('AudioManager: Error playing SFX', error);
        }
    }

    /**
     * Handle countdown tick sounds
     * @param {number} remainingSeconds - Seconds remaining
     */
    handleCountdown(remainingSeconds) {
        if (!this.isInitialized || !this.howlerAvailable || this.isMuted || !this.countdownEnabled) return;
        
        // Only play during answering phase
        if (this.currentPhase !== 'answering') return;
        
        // Avoid duplicate ticks for same second
        if (remainingSeconds === this.lastPlayedSecond) return;
        
        // Play tick for last 10 seconds
        if (remainingSeconds <= 10 && remainingSeconds > 0) {
            this.lastPlayedSecond = remainingSeconds;
            this.playSfx('tick');
        }
        
        // Play buzzer at 0
        if (remainingSeconds === 0 && this.lastPlayedSecond !== 0) {
            this.lastPlayedSecond = 0;
            this.playSfx('buzzer');
        }
    }

    /**
     * Play splash sound effect
     */
    playSplash() {
        this.playSfx('splash');
    }

    /**
     * Play booster activation sound
     */
    playBooster() {
        this.playSfx('booster');
    }

    /**
     * Play correct answer sound
     */
    playCorrect() {
        this.playSfx('correct');
    }

    /**
     * Play wrong answer sound
     */
    playWrong() {
        this.playSfx('wrong');
    }

    /**
     * Play fanfare sound effect
     */
    playFanfare() {
        this.playSfx('fanfare');
    }

    /**
     * Play drumroll sound effect
     */
    playDrumroll() {
        this.playSfx('drumroll');
    }

    /**
     * Play tada sound effect
     */
    playTada() {
        this.playSfx('tada');
    }

    /**
     * Set mute state
     * @param {boolean} muted - Whether to mute
     */
    mute(muted) {
        this.isMuted = muted;
        
        if (this.bgMusic) {
            try {
                if (muted) {
                    this.bgMusic.pause();
                } else {
                    this.bgMusic.play();
                }
            } catch (e) {}
        }
        
        if (this.victoryLoop) {
            try {
                if (muted) {
                    this.victoryLoop.pause();
                } else {
                    this.victoryLoop.play();
                }
            } catch (e) {}
        }
        
        console.log(`AudioManager: ${muted ? 'Muted' : 'Unmuted'}`);
    }

    /**
     * Toggle mute state
     */
    toggleMute() {
        this.mute(!this.isMuted);
        return this.isMuted;
    }

    /**
     * Set music volume
     * @param {number} volume - Volume 0-1
     */
    setMusicVolume(volume) {
        this.musicVolume = Math.max(0, Math.min(1, volume));
        if (this.bgMusic) {
            try {
                this.bgMusic.volume(this.musicVolume);
            } catch (e) {}
        }
        if (this.victoryLoop) {
            try {
                this.victoryLoop.volume(this.musicVolume * 0.8);
            } catch (e) {}
        }
    }

    /**
     * Set SFX volume
     * @param {number} volume - Volume 0-1
     */
    setSfxVolume(volume) {
        this.sfxVolume = Math.max(0, Math.min(1, volume));
    }

    /**
     * Enable/disable countdown sounds
     * @param {boolean} enabled 
     */
    setCountdownEnabled(enabled) {
        this.countdownEnabled = enabled;
    }

    /**
     * Get load error count (for diagnostics)
     */
    getLoadErrorCount() {
        return this.loadErrors.size;
    }

    /**
     * Stop all audio
     */
    stopAll() {
        if (this.bgMusic) {
            try {
                this.bgMusic.stop();
            } catch (e) {}
            this.bgMusic = null;
        }
        
        this._stopFinaleAudio();
        
        Object.values(this.sfxSounds).forEach(sound => {
            try {
                if (sound) sound.stop();
            } catch (e) {}
        });
        
        this.currentPhase = null;
        this.lastPlayedSecond = -1;
    }

    /**
     * Destroy the audio manager
     */
    destroy() {
        this.stopAll();
        try {
            if (typeof Howler !== 'undefined') {
                Howler.unload();
            }
        } catch (e) {}
        this.isInitialized = false;
    }
}

// Export for use
window.AudioManager = AudioManager;
