/**
 * Effects Manager - Pixi.js overlay for visual effects on TV
 * Part of PartyGame Iteration 15 & 16
 * 
 * Provides fullscreen canvas overlay for:
 * - Splash screens (quiz start)
 * - Booster activation effects
 * - Particle systems
 * - Finale ceremony (confetti, winner spotlight)
 * 
 * Note: Uses Pixi.js v7 API
 */

class EffectsManager {
    constructor() {
        this.app = null;
        this.container = null;
        this.isInitialized = false;
        this.splashShown = false;
        this.activeEffects = [];
        this.initError = null;
        this.finaleActive = false;
        this.confettiParticles = [];
        this.confettiInterval = null;
    }

    /**
     * Initialize the Pixi.js application and canvas overlay
     * For Pixi.js v7.3.2, constructor takes options directly (no async init method)
     */
    async init() {
        // Check if Pixi is available
        if (typeof PIXI === 'undefined') {
            this.initError = 'Pixi.js not loaded';
            console.warn('EffectsManager: Pixi.js not loaded, effects disabled');
            return false;
        }

        try {
            // Create Pixi application with v7 API (synchronous constructor)
            this.app = new PIXI.Application({
                width: window.innerWidth,
                height: window.innerHeight,
                backgroundAlpha: 0,  // v7: use backgroundAlpha instead of transparent
                resolution: window.devicePixelRatio || 1,
                autoDensity: true,
                antialias: true
            });

            // In v7, app.view contains the canvas (not app.canvas)
            const canvas = this.app.view;
            canvas.style.position = 'fixed';
            canvas.style.top = '0';
            canvas.style.left = '0';
            canvas.style.width = '100%';
            canvas.style.height = '100%';
            canvas.style.pointerEvents = 'none';
            canvas.style.zIndex = '9999';
            canvas.id = 'effects-canvas';

            // Append to body
            document.body.appendChild(canvas);

            // Create main container
            this.container = new PIXI.Container();
            this.app.stage.addChild(this.container);

            // Handle window resize
            window.addEventListener('resize', () => this.handleResize());

            this.isInitialized = true;
            this.initError = null;
            console.log('EffectsManager: Initialized successfully');
            return true;
        } catch (error) {
            this.initError = error.message || 'Unknown error';
            console.error('EffectsManager: Failed to initialize', error);
            // Make sure we don't leave a broken canvas
            this.isInitialized = false;
            return false;
        }
    }

    /**
     * Get initialization status for diagnostics
     */
    getStatus() {
        return {
            initialized: this.isInitialized,
            error: this.initError,
            splashShown: this.splashShown,
            finaleActive: this.finaleActive,
            hasCanvas: !!document.getElementById('effects-canvas')
        };
    }

    /**
     * Handle window resize
     */
    handleResize() {
        if (!this.app || !this.app.renderer) return;
        try {
            this.app.renderer.resize(window.innerWidth, window.innerHeight);
        } catch (e) {
            console.warn('EffectsManager: Resize failed', e);
        }
    }

    /**
     * Play splash screen effect
     * @param {string} type - Type of splash ("QuizStart", "RoundStart", etc.)
     */
    playSplash(type) {
        if (!this.isInitialized) return;

        // Only show quiz start splash once per session
        if (type === 'QuizStart' && this.splashShown) {
            return;
        }

        if (type === 'QuizStart') {
            this.splashShown = true;
            this._playQuizStartSplash();
        }
    }

    /**
     * Reset splash shown state (e.g., for new game)
     */
    resetSplash() {
        this.splashShown = false;
    }

    /**
     * Play quiz start splash animation
     */
    _playQuizStartSplash() {
        if (!this.app || !this.container) return;
        
        const centerX = this.app.screen.width / 2;
        const centerY = this.app.screen.height / 2;

        // Create dark overlay
        const overlay = new PIXI.Graphics();
        overlay.beginFill(0x000000, 0.8);
        overlay.drawRect(0, 0, this.app.screen.width, this.app.screen.height);
        overlay.endFill();
        overlay.alpha = 0;
        this.container.addChild(overlay);

        // Create main text
        const titleStyle = new PIXI.TextStyle({
            fontFamily: 'Arial, sans-serif',
            fontSize: 120,
            fontWeight: 'bold',
            fill: ['#ffffff', '#ffd700'],
            fillGradientStops: [0, 1],
            stroke: '#000000',
            strokeThickness: 8,
            dropShadow: true,
            dropShadowColor: '#000000',
            dropShadowBlur: 10,
            dropShadowDistance: 5,
        });

        const titleText = new PIXI.Text('QUIZ TIME!', titleStyle);
        titleText.anchor.set(0.5);
        titleText.position.set(centerX, centerY);
        titleText.alpha = 0;
        titleText.scale.set(0.5);
        this.container.addChild(titleText);

        // Create subtitle
        const subtitleStyle = new PIXI.TextStyle({
            fontFamily: 'Arial, sans-serif',
            fontSize: 48,
            fontWeight: 'bold',
            fill: '#ffffff',
            dropShadow: true,
            dropShadowColor: '#000000',
            dropShadowBlur: 5,
        });

        const subtitleText = new PIXI.Text("LET'S GO! 🎉", subtitleStyle);
        subtitleText.anchor.set(0.5);
        subtitleText.position.set(centerX, centerY + 100);
        subtitleText.alpha = 0;
        this.container.addChild(subtitleText);

        // Create particles
        const particles = this._createParticleBurst(centerX, centerY, 50);

        // Animation timeline using GSAP if available, otherwise manual
        if (typeof gsap !== 'undefined') {
            const tl = gsap.timeline();
            
            tl.to(overlay, { alpha: 1, duration: 0.2 })
              .to(titleText, { alpha: 1, duration: 0.3 }, 0.1)
              .to(titleText.scale, { x: 1, y: 1, duration: 0.4, ease: 'back.out(1.7)' }, 0.1)
              .to(subtitleText, { alpha: 1, duration: 0.3 }, 0.3)
              .add(() => this._animateParticles(particles), 0.2)
              .to([overlay, titleText, subtitleText], { alpha: 0, duration: 0.4 }, '+=0.8')
              .add(() => {
                  try {
                      this.container.removeChild(overlay);
                      this.container.removeChild(titleText);
                      this.container.removeChild(subtitleText);
                      particles.forEach(p => this.container.removeChild(p));
                  } catch (e) {}
              });
        } else {
            // Simple fallback without GSAP
            overlay.alpha = 1;
            titleText.alpha = 1;
            titleText.scale.set(1);
            subtitleText.alpha = 1;

            setTimeout(() => {
                try {
                    this.container.removeChild(overlay);
                    this.container.removeChild(titleText);
                    this.container.removeChild(subtitleText);
                    particles.forEach(p => this.container.removeChild(p));
                } catch (e) {}
            }, 1500);
        }
    }

    // ============================================
    // FINALE CEREMONY EFFECTS (Iteration 16)
    // ============================================

    /**
     * Play the finale ceremony with confetti and spotlight
     * @param {object} options - { winnerName, duration }
     */
    playFinale(options = {}) {
        if (!this.isInitialized || !this.app || !this.container) return;
        if (this.finaleActive) return; // Already playing

        this.finaleActive = true;
        console.log('EffectsManager: Playing finale ceremony');

        const duration = options.duration || 8000;

        // Start continuous confetti
        this._startConfetti();

        // Play winner spotlight
        this._playWinnerSpotlight(options.winnerName);

        // Initial confetti burst
        this._playConfettiBurst(3);

        // Schedule additional bursts
        setTimeout(() => this._playConfettiBurst(2), 2000);
        setTimeout(() => this._playConfettiBurst(2), 4000);
    }

    /**
     * Stop finale effects
     */
    stopFinale() {
        this.finaleActive = false;
        this._stopConfetti();
        this.clear();
        console.log('EffectsManager: Finale stopped');
    }

    /**
     * Play confetti burst effect
     * @param {number} intensity - 1-5, affects particle count
     */
    playConfettiBurst(intensity = 3) {
        this._playConfettiBurst(intensity);
    }

    /**
     * Internal confetti burst
     */
    _playConfettiBurst(intensity = 3) {
        if (!this.isInitialized || !this.app || !this.container) return;

        const particleCount = intensity * 40;
        const screenWidth = this.app.screen.width;
        const screenHeight = this.app.screen.height;

        const colors = [
            0xffd700, // Gold
            0xff6b6b, // Red
            0x4ecdc4, // Teal
            0x45b7d1, // Blue
            0xf9ca24, // Yellow
            0x6c5ce7, // Purple
            0x00b894, // Green
            0xfd79a8, // Pink
            0xe17055, // Orange
        ];

        const particles = [];

        for (let i = 0; i < particleCount; i++) {
            const particle = new PIXI.Graphics();
            const color = colors[Math.floor(Math.random() * colors.length)];
            
            // Random confetti shapes
            const shapeType = Math.floor(Math.random() * 3);
            if (shapeType === 0) {
                // Rectangle (ribbon)
                particle.beginFill(color);
                particle.drawRect(-8, -3, 16, 6);
                particle.endFill();
            } else if (shapeType === 1) {
                // Circle
                particle.beginFill(color);
                particle.drawCircle(0, 0, 5 + Math.random() * 3);
                particle.endFill();
            } else {
                // Star-ish shape
                particle.beginFill(color);
                particle.drawPolygon([0, -8, 3, 0, 0, 8, -3, 0]);
                particle.endFill();
            }

            // Start position - spread across top
            const startX = Math.random() * screenWidth;
            const startY = -20 - Math.random() * 100;
            particle.position.set(startX, startY);
            particle.rotation = Math.random() * Math.PI * 2;
            
            this.container.addChild(particle);
            particles.push(particle);

            // Animate falling
            if (typeof gsap !== 'undefined') {
                const duration = 2 + Math.random() * 2;
                const endX = startX + (Math.random() - 0.5) * 300;
                const endY = screenHeight + 50;
                const swayAmount = (Math.random() - 0.5) * 200;

                gsap.to(particle, {
                    y: endY,
                    x: endX,
                    rotation: particle.rotation + (Math.random() - 0.5) * 10,
                    duration: duration,
                    ease: 'none',
                    onComplete: () => {
                        try { this.container.removeChild(particle); } catch (e) {}
                    }
                });

                // Sway motion
                gsap.to(particle, {
                    x: `+=${swayAmount}`,
                    duration: duration / 3,
                    yoyo: true,
                    repeat: 2,
                    ease: 'sine.inOut'
                });
            } else {
                // Simple fallback
                setTimeout(() => {
                    try { this.container.removeChild(particle); } catch (e) {}
                }, 3000);
            }
        }
    }

    /**
     * Start continuous confetti stream
     */
    _startConfetti() {
        if (this.confettiInterval) return;

        this.confettiInterval = setInterval(() => {
            if (!this.finaleActive) {
                this._stopConfetti();
                return;
            }
            this._spawnConfettiParticle();
        }, 50); // Spawn particle every 50ms
    }

    /**
     * Stop continuous confetti
     */
    _stopConfetti() {
        if (this.confettiInterval) {
            clearInterval(this.confettiInterval);
            this.confettiInterval = null;
        }
    }

    /**
     * Spawn a single confetti particle
     */
    _spawnConfettiParticle() {
        if (!this.app || !this.container) return;

        const colors = [0xffd700, 0xff6b6b, 0x4ecdc4, 0x45b7d1, 0xf9ca24, 0x6c5ce7];
        const color = colors[Math.floor(Math.random() * colors.length)];

        const particle = new PIXI.Graphics();
        particle.beginFill(color);
        particle.drawRect(-6, -2, 12, 4);
        particle.endFill();

        const startX = Math.random() * this.app.screen.width;
        particle.position.set(startX, -10);
        particle.rotation = Math.random() * Math.PI * 2;
        particle.alpha = 0.8 + Math.random() * 0.2;

        this.container.addChild(particle);

        if (typeof gsap !== 'undefined') {
            const duration = 3 + Math.random() * 2;
            gsap.to(particle, {
                y: this.app.screen.height + 20,
                x: startX + (Math.random() - 0.5) * 150,
                rotation: particle.rotation + (Math.random() - 0.5) * 8,
                duration: duration,
                ease: 'none',
                onComplete: () => {
                    try { this.container.removeChild(particle); } catch (e) {}
                }
            });
        } else {
            setTimeout(() => {
                try { this.container.removeChild(particle); } catch (e) {}
            }, 4000);
        }
    }

    /**
     * Play winner spotlight effect
     * @param {string} winnerName - Name to display (optional)
     */
    _playWinnerSpotlight(winnerName) {
        if (!this.app || !this.container) return;

        const centerX = this.app.screen.width / 2;
        const topY = this.app.screen.height * 0.15;

        // Create spotlight glow (radial gradient effect)
        const spotlight = new PIXI.Graphics();
        spotlight.beginFill(0xffd700, 0.15);
        spotlight.drawCircle(centerX, this.app.screen.height * 0.4, 400);
        spotlight.endFill();
        spotlight.beginFill(0xffd700, 0.1);
        spotlight.drawCircle(centerX, this.app.screen.height * 0.4, 600);
        spotlight.endFill();
        spotlight.alpha = 0;
        this.container.addChild(spotlight);

        // Dynamically calculate font size based on screen width
        const baseFontSize = Math.min(80, this.app.screen.width / 15);

        // Create "WE HAVE A WINNER!" text
        const titleStyle = new PIXI.TextStyle({
            fontFamily: 'Arial, sans-serif',
            fontSize: baseFontSize,
            fontWeight: 'bold',
            fill: ['#ffd700', '#ffaa00'],
            stroke: '#000000',
            strokeThickness: Math.max(4, baseFontSize / 15),
            dropShadow: true,
            dropShadowColor: '#000000',
            dropShadowBlur: 15,
            dropShadowDistance: 5,
        });

        const titleText = new PIXI.Text('🎉 WE HAVE A WINNER! 🎉', titleStyle);
        titleText.anchor.set(0.5); // Center anchor point
        titleText.position.set(centerX, topY);
        titleText.alpha = 0;
        titleText.scale.set(0.5);
        this.container.addChild(titleText);

        // Update position on window resize
        const handleResize = () => {
            if (!this.app || !titleText.parent) return;
            const newCenterX = this.app.screen.width / 2;
            const newTopY = this.app.screen.height * 0.15;
            titleText.position.set(newCenterX, newTopY);
            spotlight.clear();
            spotlight.beginFill(0xffd700, 0.15);
            spotlight.drawCircle(newCenterX, this.app.screen.height * 0.4, 400);
            spotlight.endFill();
            spotlight.beginFill(0xffd700, 0.1);
            spotlight.drawCircle(newCenterX, this.app.screen.height * 0.4, 600);
            spotlight.endFill();
        };

        // Add resize listener
        window.addEventListener('resize', handleResize);

        // Animate
        if (typeof gsap !== 'undefined') {
            const tl = gsap.timeline({
                onComplete: () => {
                    window.removeEventListener('resize', handleResize);
                }
            });

            tl.to(spotlight, { alpha: 1, duration: 0.5 })
              .to(titleText, { alpha: 1, duration: 0.4 }, 0.2)
              .to(titleText.scale, { x: 1, y: 1, duration: 0.5, ease: 'back.out(1.5)' }, 0.2)
              .to(titleText, {
                  y: topY - 10,
                  duration: 1.5,
                  yoyo: true,
                  repeat: -1,
                  ease: 'sine.inOut'
              }, 0.7);

            // Pulse the spotlight
            gsap.to(spotlight, {
                alpha: 0.7,
                duration: 1,
                yoyo: true,
                repeat: -1,
                ease: 'sine.inOut'
            });

            // Store references for cleanup
            this.activeEffects.push({ spotlight, titleText, tl, handleResize });
        } else {
            spotlight.alpha = 1;
            titleText.alpha = 1;
            titleText.scale.set(1);
            this.activeEffects.push({ spotlight, titleText, handleResize });
        }
    }

    /**
     * Play podium reveal animation
     * Called by the DOM to sync with UI animations
     */
    playPodiumReveal() {
        if (!this.isInitialized) return;
        // Trigger an extra confetti burst when podium reveals
        setTimeout(() => this._playConfettiBurst(2), 500);
    }

    /**
     * Play booster activation effect
     * @param {string} boosterType - Type of booster
     * @param {object} options - Options like playerName, targetName
     */
    playBooster(boosterType, options = {}) {
        if (!this.isInitialized || !this.app || !this.container) return;

        const centerX = this.app.screen.width / 2;
        const centerY = this.app.screen.height / 3;

        switch (boosterType) {
            case 'DoublePoints':
            case 0:
                this._playDoublePointsEffect(centerX, centerY, options);
                break;
            case 'BackToZero':
            case 2:
                this._playBackToZeroEffect(centerX, centerY, options);
                break;
            case 'Nope':
            case 3:
                this._playNopeEffect(centerX, centerY, options);
                break;
            case 'FiftyFifty':
            case 1:
                this._playFiftyFiftyEffect(centerX, centerY, options);
                break;
            case 'ChaosMode':
            case 8:
                this._playChaosModeEffect(centerX, centerY, options);
                break;
            case 'Shield':
            case 9:
                this._playShieldEffect(centerX, centerY, options);
                break;
            default:
                this._playGenericBoosterEffect(centerX, centerY, boosterType, options);
        }
    }

    /**
     * Double Points effect - glowing "×2" burst
     */
    _playDoublePointsEffect(x, y, options) {
        const style = new PIXI.TextStyle({
            fontFamily: 'Arial, sans-serif',
            fontSize: 200,
            fontWeight: 'bold',
            fill: ['#ffd700', '#ffaa00'],
            stroke: '#000000',
            strokeThickness: 10,
            dropShadow: true,
            dropShadowColor: '#ff6600',
            dropShadowBlur: 20,
            dropShadowDistance: 0,
        });

        const text = new PIXI.Text('×2', style);
        text.anchor.set(0.5);
        text.position.set(x, y);
        text.alpha = 0;
        text.scale.set(0.3);
        this.container.addChild(text);

        // Glow effect
        const glow = new PIXI.Graphics();
        glow.beginFill(0xffd700, 0.3);
        glow.drawCircle(x, y, 150);
        glow.endFill();
        glow.alpha = 0;
        this.container.addChild(glow);

        if (typeof gsap !== 'undefined') {
            const tl = gsap.timeline();
            tl.to(glow, { alpha: 0.6, duration: 0.2 })
              .to(text, { alpha: 1, duration: 0.2 }, 0)
              .to(text.scale, { x: 1.2, y: 1.2, duration: 0.3, ease: 'back.out(2)' }, 0)
              .to(text.scale, { x: 1, y: 1, duration: 0.2 }, 0.3)
              .to([text, glow], { alpha: 0, duration: 0.3 }, '+=0.5')
              .add(() => {
                  try {
                      this.container.removeChild(text);
                      this.container.removeChild(glow);
                  } catch (e) {}
              });
        } else {
            text.alpha = 1;
            text.scale.set(1);
            glow.alpha = 0.6;
            setTimeout(() => {
                try {
                    this.container.removeChild(text);
                    this.container.removeChild(glow);
                } catch (e) {}
            }, 1200);
        }
    }

    /**
     * Back to Zero effect - shatter particles
     */
    _playBackToZeroEffect(x, y, options) {
        const style = new PIXI.TextStyle({
            fontFamily: 'Arial, sans-serif',
            fontSize: 100,
            fontWeight: 'bold',
            fill: '#ff4444',
            stroke: '#000000',
            strokeThickness: 6,
        });

        const text = new PIXI.Text('💥 RESET!', style);
        text.anchor.set(0.5);
        text.position.set(x, y);
        text.alpha = 0;
        this.container.addChild(text);

        // Create shatter particles
        const particles = [];
        for (let i = 0; i < 20; i++) {
            const particle = new PIXI.Graphics();
            particle.beginFill(0xff4444);
            particle.drawRect(-5, -5, 10, 10);
            particle.endFill();
            particle.position.set(x, y);
            particle.alpha = 0;
            this.container.addChild(particle);
            particles.push(particle);
        }

        if (typeof gsap !== 'undefined') {
            const tl = gsap.timeline();
            tl.to(text, { alpha: 1, duration: 0.2 })
              .to(text.scale, { x: 1.3, y: 1.3, duration: 0.1 }, 0)
              .to(text.scale, { x: 1, y: 1, duration: 0.1 }, 0.1);

            particles.forEach((p, i) => {
                const angle = (i / particles.length) * Math.PI * 2;
                const distance = 100 + Math.random() * 100;
                tl.to(p, { alpha: 1, duration: 0.1 }, 0.1)
                  .to(p, {
                      x: x + Math.cos(angle) * distance,
                      y: y + Math.sin(angle) * distance,
                      alpha: 0,
                      rotation: Math.random() * 10,
                      duration: 0.6,
                      ease: 'power2.out'
                  }, 0.2);
            });

            tl.to(text, { alpha: 0, duration: 0.3 }, '+=0.2')
              .add(() => {
                  try {
                      this.container.removeChild(text);
                      particles.forEach(p => this.container.removeChild(p));
                  } catch (e) {}
              });
        } else {
            text.alpha = 1;
            setTimeout(() => {
                try {
                    this.container.removeChild(text);
                    particles.forEach(p => this.container.removeChild(p));
                } catch (e) {}
            }, 1200);
        }
    }

    /**
     * Nope effect - big stamp
     */
    _playNopeEffect(x, y, options) {
        const style = new PIXI.TextStyle({
            fontFamily: 'Arial, sans-serif',
            fontSize: 180,
            fontWeight: 'bold',
            fill: '#ff0000',
            stroke: '#000000',
            strokeThickness: 12,
            dropShadow: true,
            dropShadowColor: '#000000',
            dropShadowBlur: 10,
        });

        const text = new PIXI.Text('🚫 NOPE!', style);
        text.anchor.set(0.5);
        text.position.set(x, y);
        text.alpha = 0;
        text.scale.set(3);
        text.rotation = -0.2;
        this.container.addChild(text);

        // Smoke puff graphics
        const smoke = new PIXI.Graphics();
        smoke.beginFill(0x888888, 0.5);
        smoke.drawCircle(x, y, 200);
        smoke.endFill();
        smoke.alpha = 0;
        smoke.scale.set(0.5);
        this.container.addChild(smoke);

        if (typeof gsap !== 'undefined') {
            const tl = gsap.timeline();
            tl.to(smoke, { alpha: 0.5, duration: 0.1 })
              .to(smoke.scale, { x: 1.5, y: 1.5, duration: 0.3 }, 0)
              .to(smoke, { alpha: 0, duration: 0.3 }, 0.2)
              .to(text, { alpha: 1, duration: 0.1 }, 0)
              .to(text.scale, { x: 1, y: 1, duration: 0.2, ease: 'back.out(2)' }, 0)
              .to(text, { alpha: 0, duration: 0.3 }, '+=0.6')
              .add(() => {
                  try {
                      this.container.removeChild(text);
                      this.container.removeChild(smoke);
                  } catch (e) {}
              });
        } else {
            text.alpha = 1;
            text.scale.set(1);
            setTimeout(() => {
                try {
                    this.container.removeChild(text);
                    this.container.removeChild(smoke);
                } catch (e) {}
            }, 1200);
        }
    }

    /**
     * 50/50 effect
     */
    _playFiftyFiftyEffect(x, y, options) {
        const style = new PIXI.TextStyle({
            fontFamily: 'Arial, sans-serif',
            fontSize: 120,
            fontWeight: 'bold',
            fill: '#00aaff',
            stroke: '#000000',
            strokeThickness: 8,
        });

        const text = new PIXI.Text('🔀 50/50', style);
        text.anchor.set(0.5);
        text.position.set(x, y);
        text.alpha = 0;
        this.container.addChild(text);

        this._animateTextPop(text, 1000);
    }

    /**
     * Chaos Mode effect
     */
    _playChaosModeEffect(x, y, options) {
        const style = new PIXI.TextStyle({
            fontFamily: 'Arial, sans-serif',
            fontSize: 100,
            fontWeight: 'bold',
            fill: ['#ff00ff', '#00ffff', '#ffff00'],
            stroke: '#000000',
            strokeThickness: 6,
        });

        const text = new PIXI.Text('🌀 CHAOS!', style);
        text.anchor.set(0.5);
        text.position.set(x, y);
        text.alpha = 0;
        this.container.addChild(text);

        if (typeof gsap !== 'undefined') {
            const tl = gsap.timeline();
            tl.to(text, { alpha: 1, duration: 0.2 })
              .to(text, { rotation: 0.1, duration: 0.1, yoyo: true, repeat: 5 }, 0)
              .to(text, { alpha: 0, duration: 0.3 }, '+=0.3')
              .add(() => { try { this.container.removeChild(text); } catch (e) {} });
        } else {
            text.alpha = 1;
            setTimeout(() => { try { this.container.removeChild(text); } catch (e) {} }, 1200);
        }
    }

    /**
     * Shield block effect
     */
    _playShieldEffect(x, y, options) {
        const style = new PIXI.TextStyle({
            fontFamily: 'Arial, sans-serif',
            fontSize: 120,
            fontWeight: 'bold',
            fill: '#3498db',
            stroke: '#000000',
            strokeThickness: 8,
        });

        const text = new PIXI.Text('🛡️ BLOCKED!', style);
        text.anchor.set(0.5);
        text.position.set(x, y);
        text.alpha = 0;
        this.container.addChild(text);

        this._animateTextPop(text, 1000);
    }

    /**
     * Generic booster effect
     */
    _playGenericBoosterEffect(x, y, boosterType, options) {
        const emoji = this._getBoosterEmoji(boosterType);
        const style = new PIXI.TextStyle({
            fontFamily: 'Arial, sans-serif',
            fontSize: 100,
            fontWeight: 'bold',
            fill: '#ffffff',
            stroke: '#000000',
            strokeThickness: 6,
        });

        const text = new PIXI.Text(`${emoji} BOOST!`, style);
        text.anchor.set(0.5);
        text.position.set(x, y);
        text.alpha = 0;
        this.container.addChild(text);

        this._animateTextPop(text, 1000);
    }

    /**
     * Helper: Animate text pop in and out
     */
    _animateTextPop(text, duration) {
        if (typeof gsap !== 'undefined') {
            const tl = gsap.timeline();
            tl.to(text, { alpha: 1, duration: 0.2 })
              .to(text.scale, { x: 1.2, y: 1.2, duration: 0.2, ease: 'back.out(2)' }, 0)
              .to(text.scale, { x: 1, y: 1, duration: 0.15 }, 0.2)
              .to(text, { alpha: 0, duration: 0.3 }, `+=${duration / 1000 - 0.5}`)
              .add(() => { try { this.container.removeChild(text); } catch (e) {} });
        } else {
            text.alpha = 1;
            setTimeout(() => { try { this.container.removeChild(text); } catch (e) {} }, duration);
        }
    }

    /**
     * Create particle burst
     */
    _createParticleBurst(x, y, count) {
        const particles = [];
        if (!this.container) return particles;
        
        const colors = [0xffd700, 0xff6b6b, 0x4ecdc4, 0x45b7d1, 0xf9ca24];

        for (let i = 0; i < count; i++) {
            const particle = new PIXI.Graphics();
            const color = colors[Math.floor(Math.random() * colors.length)];
            const size = 5 + Math.random() * 10;
            
            particle.beginFill(color);
            particle.drawCircle(0, 0, size);
            particle.endFill();
            
            particle.position.set(x, y);
            particle.alpha = 0;
            this.container.addChild(particle);
            particles.push(particle);
        }

        return particles;
    }

    /**
     * Animate particle burst
     */
    _animateParticles(particles) {
        if (typeof gsap === 'undefined') return;

        particles.forEach((p, i) => {
            const angle = (i / particles.length) * Math.PI * 2 + Math.random() * 0.5;
            const distance = 150 + Math.random() * 200;
            const duration = 0.6 + Math.random() * 0.4;

            gsap.to(p, { alpha: 1, duration: 0.1 });
            gsap.to(p, {
                x: p.x + Math.cos(angle) * distance,
                y: p.y + Math.sin(angle) * distance,
                alpha: 0,
                rotation: Math.random() * 10,
                duration: duration,
                ease: 'power2.out',
                delay: Math.random() * 0.2
            }); 
        });
    }

    /**
     * Get emoji for booster type
     */
    _getBoosterEmoji(type) {
        const emojis = {
            0: '⚡', 1: '🔀', 2: '💥', 3: '🚫', 4: '🔄',
            5: '⏱️', 6: '🪞', 7: '⚖️', 8: '🌀', 9: '🛡️',
            10: '🃏', 11: '🎯',
            'DoublePoints': '⚡', 'FiftyFifty': '🔀', 'BackToZero': '💥',
            'Nope': '🚫', 'PositionSwitch': '🔄', 'LateLock': '⏱️',
            'Mirror': '🪞', 'JuryDuty': '⚖️', 'ChaosMode': '🌀',
            'Shield': '🛡️', 'Wildcard': '🃏', 'Spotlight': '🎯'
        };
        return emojis[type] || '🎁';
    }

    /**
     * Clear all effects
     */
    clear() {
        if (!this.container) return;
        try {
            // Kill any active GSAP animations
            if (typeof gsap !== 'undefined') {
                this.activeEffects.forEach(effect => {
                    if (effect.tl) effect.tl.kill();
                    if (effect.handleResize) {
                        window.removeEventListener('resize', effect.handleResize);
                    }
                });
            }
            this.activeEffects = [];
            this.container.removeChildren();
        } catch (e) {}
    }

    /**
     * Destroy the effects manager
     */
    destroy() {
        this.stopFinale();
        if (this.app) {
            try {
                this.app.destroy(true, { children: true, texture: true, baseTexture: true });
            } catch (e) {}
            this.app = null;
        }
        this.isInitialized = false;
    }
}

// Export for use
window.EffectsManager = EffectsManager;
