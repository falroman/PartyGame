/**
 * Effects Manager - Pixi.js overlay for visual effects on TV
 * Part of PartyGame Iteration 15
 * 
 * Provides fullscreen canvas overlay for:
 * - Splash screens (quiz start)
 * - Booster activation effects
 * - Particle systems
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
    }

    /**
     * Initialize the Pixi.js application and canvas overlay
     * For Pixi.js v7, this needs to be called and awaited
     */
    async init() {
        // Check if Pixi is available
        if (typeof PIXI === 'undefined') {
            console.warn('EffectsManager: Pixi.js not loaded, effects disabled');
            return false;
        }

        try {
            // Create Pixi application with v7 API
            this.app = new PIXI.Application();
            
            // Initialize with v7 async method
            await this.app.init({
                width: window.innerWidth,
                height: window.innerHeight,
                backgroundAlpha: 0,  // v7: use backgroundAlpha instead of transparent
                resolution: window.devicePixelRatio || 1,
                autoDensity: true,
                antialias: true
            });

            // Style the canvas as overlay (v7: use canvas instead of view)
            const canvas = this.app.canvas;
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
            console.log('EffectsManager: Initialized successfully');
            return true;
        } catch (error) {
            console.error('EffectsManager: Failed to initialize', error);
            // Make sure we don't leave a broken canvas
            this.isInitialized = false;
            return false;
        }
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

        const subtitleText = new PIXI.Text("LET'S GO! ??", subtitleStyle);
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

        const text = new PIXI.Text('?? RESET!', style);
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

        const text = new PIXI.Text('?? NOPE!', style);
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

        const text = new PIXI.Text('?? 50/50', style);
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

        const text = new PIXI.Text('?? CHAOS!', style);
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

        const text = new PIXI.Text('??? BLOCKED!', style);
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
            0: '?', 1: '??', 2: '??', 3: '??', 4: '??',
            5: '?', 6: '??', 7: '??', 8: '??', 9: '???',
            10: '??', 11: '??',
            'DoublePoints': '?', 'FiftyFifty': '??', 'BackToZero': '??',
            'Nope': '??', 'PositionSwitch': '??', 'LateLock': '?',
            'Mirror': '??', 'JuryDuty': '??', 'ChaosMode': '??',
            'Shield': '???', 'Wildcard': '??', 'Spotlight': '??'
        };
        return emojis[type] || '??';
    }

    /**
     * Clear all effects
     */
    clear() {
        if (!this.container) return;
        try {
            this.container.removeChildren();
        } catch (e) {}
    }

    /**
     * Destroy the effects manager
     */
    destroy() {
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
