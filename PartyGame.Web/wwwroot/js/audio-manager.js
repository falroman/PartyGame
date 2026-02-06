/**
 * Audio Manager - Howler.js based audio system for TV
 * Part of PartyGame Iteration 15
 * 
 * Provides:
 * - Phase-based background music
 * - Sound effects (boosters, countdown, buzzer)
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
        
        // Countdown state
        this.lastPlayedSecond = -1;
        this.countdownEnabled = true;
    }

    /**
     * Initialize the audio manager
     * Note: Actual sound loading happens after user interaction due to autoplay policy
     */
    init() {
        if (this.isInitialized) return;

        // Check if Howler is available
        if (typeof Howl === 'undefined') {
            console.warn('AudioManager: Howler.js not loaded, audio disabled');
            return false;
        }

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

        // Sound effects
        this.sfxConfig = {
            tick: { src: `${basePath}tick.mp3`, volume: 0.5 },
            buzzer: { src: `${basePath}buzzer.mp3`, volume: 0.7 },
            booster: { src: `${basePath}booster.mp3`, volume: 0.6 },
            correct: { src: `${basePath}correct.mp3`, volume: 0.5 },
            wrong: { src: `${basePath}wrong.mp3`, volume: 0.4 },
            splash: { src: `${basePath}splash.mp3`, volume: 0.6 }
        };
    }

    /**
     * Unlock audio context after user interaction
     * Call this from a click handler
     */
    unlock() {
        if (this.audioUnlocked) return;

        try {
            // Create a silent sound to unlock audio context
            const silentSound = new Howl({
                src: ['data:audio/mp3;base64,SUQzBAAAAAAAI1RTU0UAAAAPAAADTGF2ZjU4Ljc2LjEwMAAAAAAAAAAAAAAA//tQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAWGluZwAAAA8AAAACAAABhgC7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7//////////////////////////////////////////////////////////////////8AAAAATGF2YzU4LjEzAAAAAAAAAAAAAAAAJAAAAAAAAAAAAYYoRwmHAAAAAAD/+9DEAAAH+ANoUAAAIv8AXQ4wBACAIAMN//+sREZG7v/h9Xd3d3cQBAEHd//+IAgCAIB8oc/+D4Pg+D4Ph9YPlDkJB8H/5Q5/gh//+c5znP8hyH//qqqqqgAAAAD/+9DEFQPAAADSAAAAIAAANIAAAAQAAAGkAAAAIAAANIAAAARMQU1FMy4xMDBVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV'],
                volume: 0,
                onend: () => {
                    this.audioUnlocked = true;
                    console.log('AudioManager: Audio unlocked');
                }
            });
            silentSound.play();
            this.audioUnlocked = true;
        } catch (error) {
            console.warn('AudioManager: Failed to unlock audio', error);
        }
    }

    /**
     * Set the current game phase and play appropriate music
     * @param {string} phase - Phase name
     */
    setPhase(phase) {
        if (!this.isInitialized || this.isMuted) return;
        
        // Normalize phase name
        const normalizedPhase = this._normalizePhase(phase);
        
        if (normalizedPhase === this.currentPhase) return;
        
        console.log(`AudioManager: Phase change ${this.currentPhase} -> ${normalizedPhase}`);
        this.currentPhase = normalizedPhase;
        
        // Reset countdown state on phase change
        this.lastPlayedSecond = -1;
        
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
        const trackConfig = this.bgTracks[phase];
        if (!trackConfig) return;

        // Skip if we already know this track failed to load
        if (this.loadErrors.has(trackConfig.src)) return;

        // Fade out current music
        if (this.bgMusic) {
            const oldMusic = this.bgMusic;
            if (typeof gsap !== 'undefined') {
                gsap.to(oldMusic, {
                    volume: 0,
                    duration: 0.5,
                    onComplete: () => {
                        try { oldMusic.stop(); } catch (e) {}
                    }
                });
            } else {
                try {
                    oldMusic.fade(oldMusic.volume(), 0, 500);
                    setTimeout(() => {
                        try { oldMusic.stop(); } catch (e) {}
                    }, 500);
                } catch (e) {}
            }
        }

        // Create and play new music
        try {
            this.bgMusic = new Howl({
                src: [trackConfig.src],
                loop: trackConfig.loop,
                volume: 0,
                onloaderror: (id, error) => {
                    console.warn(`AudioManager: Failed to load ${trackConfig.src} - audio files may not be available`);
                    this.loadErrors.add(trackConfig.src);
                },
                onplayerror: (id, error) => {
                    console.warn(`AudioManager: Failed to play ${trackConfig.src}`);
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
            console.warn('AudioManager: Error playing music', error);
        }
    }

    /**
     * Play a sound effect
     * @param {string} sfxName - Name of the sound effect
     */
    playSfx(sfxName) {
        if (!this.isInitialized || this.isMuted) return;

        const config = this.sfxConfig[sfxName.toLowerCase()];
        if (!config) {
            console.warn(`AudioManager: Unknown SFX "${sfxName}"`);
            return;
        }

        // Skip if we already know this sound failed to load
        if (this.loadErrors.has(config.src)) return;

        // Create sound on demand if not cached
        if (!this.sfxSounds[sfxName]) {
            try {
                this.sfxSounds[sfxName] = new Howl({
                    src: [config.src],
                    volume: config.volume * this.sfxVolume,
                    onloaderror: (id, error) => {
                        console.warn(`AudioManager: Failed to load SFX ${config.src} - audio files may not be available`);
                        this.loadErrors.add(config.src);
                    },
                    onplayerror: (id, error) => {
                        console.warn(`AudioManager: Failed to play SFX ${config.src}`);
                    }
                });
            } catch (error) {
                console.warn('AudioManager: Error creating SFX', error);
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
        if (!this.isInitialized || this.isMuted || !this.countdownEnabled) return;
        
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
     * Stop all audio
     */
    stopAll() {
        if (this.bgMusic) {
            try {
                this.bgMusic.stop();
            } catch (e) {}
            this.bgMusic = null;
        }
        
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
            Howler.unload();
        } catch (e) {}
        this.isInitialized = false;
    }
}

// Export for use
window.AudioManager = AudioManager;
