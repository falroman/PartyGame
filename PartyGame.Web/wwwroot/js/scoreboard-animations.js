/**
 * PartyGame - Scoreboard Animations Module
 * TV-worthy animations for scoreboard updates using GSAP + Flip
 * 
 * Features:
 * - FLIP reorder animation when ranks change
 * - Score tick-up counter animation
 * - Delta badge pop/fade animation
 * - Winner highlight pulse glow
 */

class ScoreboardAnimations {
    constructor(containerSelector = '.scoreboard-list') {
        this.container = null;
        this.containerSelector = containerSelector;
        this.prevScores = new Map(); // playerId -> score
        this.isGsapAvailable = typeof gsap !== 'undefined';
        this.isFlipAvailable = typeof Flip !== 'undefined';
        
        // Animation settings
        this.config = {
            reorderDuration: 0.6,
            reorderStagger: 0.05,
            scoreTickDuration: 0.8,
            deltaDuration: 2.0,
            deltaScale: 1.2,
            highlightDuration: 1.5,
            highlightRepeat: 2
        };

        if (!this.isGsapAvailable) {
            console.warn('ScoreboardAnimations: GSAP not loaded, animations will be disabled');
        }
        if (!this.isFlipAvailable) {
            console.warn('ScoreboardAnimations: GSAP Flip plugin not loaded, reorder animations will be disabled');
        }
    }

    /**
     * Initialize with container element
     */
    init(container) {
        if (typeof container === 'string') {
            this.container = document.querySelector(container);
        } else {
            this.container = container;
        }
        return this;
    }

    /**
     * Apply scoreboard update with all animations
     * @param {Array} scoreboard - Array of player scores [{playerId, displayName, score, position, answeredCorrectly}]
     * @param {Object} options - Optional settings
     */
    applyUpdate(scoreboard, options = {}) {
        if (!this.container) {
            this.container = document.querySelector(this.containerSelector);
        }
        if (!this.container) {
            console.warn('ScoreboardAnimations: Container not found');
            return;
        }

        const { animate = true, highlightWinnerId = null } = options;

        // Calculate deltas
        const deltas = new Map();
        let maxDelta = 0;
        let roundWinnerId = null;

        scoreboard.forEach(player => {
            const prevScore = this.prevScores.get(player.playerId) || 0;
            const delta = player.score - prevScore;
            deltas.set(player.playerId, delta);
            
            if (delta > maxDelta) {
                maxDelta = delta;
                roundWinnerId = player.playerId;
            }
        });

        // Determine winner to highlight
        const winnerToHighlight = highlightWinnerId || (maxDelta > 0 ? roundWinnerId : null);

        // Get current state for FLIP animation
        let flipState = null;
        if (animate && this.isFlipAvailable && this.container.children.length > 0) {
            flipState = Flip.getState(this.container.children);
        }

        // Update/create rows
        this._renderRows(scoreboard, deltas);

        // Apply animations
        if (animate && this.isGsapAvailable) {
            // FLIP reorder animation
            if (flipState && this.isFlipAvailable) {
                this._animateReorder(flipState);
            }

            // Score tick-up and delta animations
            scoreboard.forEach(player => {
                const delta = deltas.get(player.playerId) || 0;
                const row = this.container.querySelector(`[data-player-id="${player.playerId}"]`);
                
                if (row) {
                    const scoreEl = row.querySelector('.score-value');
                    const prevScore = this.prevScores.get(player.playerId) || 0;
                    
                    if (delta !== 0 && scoreEl) {
                        this._animateScore(scoreEl, prevScore, player.score);
                        this._showDelta(row, delta);
                    }

                    // Winner highlight
                    if (player.playerId === winnerToHighlight && delta > 0) {
                        this._highlightWinner(row);
                    }
                }
            });
        }

        // Store current scores for next update
        scoreboard.forEach(player => {
            this.prevScores.set(player.playerId, player.score);
        });
    }

    /**
     * Render scoreboard rows (idempotent - reuses existing DOM nodes)
     */
    _renderRows(scoreboard, deltas) {
        const existingRows = new Map();
        
        // Index existing rows
        Array.from(this.container.children).forEach(row => {
            const playerId = row.dataset.playerId;
            if (playerId) {
                existingRows.set(playerId, row);
            }
        });

        // Sort by position
        const sortedScoreboard = [...scoreboard].sort((a, b) => a.position - b.position);

        // Create document fragment for efficient DOM updates
        const fragment = document.createDocumentFragment();

        sortedScoreboard.forEach(player => {
            let row = existingRows.get(player.playerId);
            
            if (row) {
                // Update existing row
                this._updateRow(row, player);
                existingRows.delete(player.playerId);
            } else {
                // Create new row
                row = this._createRow(player);
            }
            
            fragment.appendChild(row);
        });

        // Remove rows for players no longer in scoreboard
        existingRows.forEach(row => row.remove());

        // Clear and append all rows
        this.container.innerHTML = '';
        this.container.appendChild(fragment);
    }

    /**
     * Create a new scoreboard row
     */
    _createRow(player) {
        const row = document.createElement('li');
        row.className = 'scoreboard-item';
        row.dataset.playerId = player.playerId;
        
        this._updateRow(row, player);
        return row;
    }

    /**
     * Update row content
     */
    _updateRow(row, player) {
        // Position class
        row.className = 'scoreboard-item';
        if (player.position === 1) row.classList.add('first');
        else if (player.position === 2) row.classList.add('second');
        else if (player.position === 3) row.classList.add('third');

        // Use Unicode escape sequences to ensure proper emoji rendering
        const positionEmoji = player.position === 1 ? '\u{1F451}' : // 👑 crown
                             player.position === 2 ? '\u{1F948}' : // 🥈 silver medal
                             player.position === 3 ? '\u{1F949}' : // 🥉 bronze medal
                             `#${player.position}`;

        const avatarSrc = this._getPlayerAvatarSrc(player);
        const initials = this._getInitials(player.displayName);

        row.innerHTML = `
            <div class="scoreboard-position">${positionEmoji}</div>
            <div class="scoreboard-player">
                <div class="player-avatar">
                    <img src="${avatarSrc}" 
                         alt="${this._escapeHtml(player.displayName)}"
                         style="width: 100%; height: 100%; object-fit: cover; border-radius: 50%;"
                         onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';">
                    <span style="display: none;">${initials}</span>
                </div>
                <div class="scoreboard-name">${this._escapeHtml(player.displayName)}</div>
            </div>
            <div class="score-value" data-score="${player.score}">${player.score}</div>
        `;
    }

    /**
     * FLIP reorder animation
     */
    _animateReorder(flipState) {
        if (!this.isFlipAvailable) return;

        Flip.from(flipState, {
            duration: this.config.reorderDuration,
            ease: 'power2.out',
            stagger: this.config.reorderStagger,
            absolute: true,
            onEnter: elements => {
                gsap.fromTo(elements, 
                    { opacity: 0, scale: 0.8 },
                    { opacity: 1, scale: 1, duration: 0.4 }
                );
            },
            onLeave: elements => {
                gsap.to(elements, { opacity: 0, scale: 0.8, duration: 0.3 });
            }
        });
    }

    /**
     * Animate score counter tick-up
     */
    _animateScore(scoreElement, fromValue, toValue) {
        if (!this.isGsapAvailable) {
            scoreElement.textContent = toValue;
            return;
        }

        const obj = { value: fromValue };
        gsap.to(obj, {
            value: toValue,
            duration: this.config.scoreTickDuration,
            ease: 'power2.out',
            onUpdate: () => {
                scoreElement.textContent = Math.round(obj.value);
            }
        });
    }

    /**
     * Show delta badge with pop/fade animation
     */
    _showDelta(rowElement, delta) {
        if (!this.isGsapAvailable || delta === 0) return;

        // Remove existing delta badge
        const existingBadge = rowElement.querySelector('.delta-badge');
        if (existingBadge) existingBadge.remove();

        // Create delta badge
        const badge = document.createElement('span');
        badge.className = `delta-badge ${delta > 0 ? 'positive' : 'negative'}`;
        badge.textContent = delta > 0 ? `+${delta}` : `${delta}`;
        
        // Position badge next to score
        const scoreEl = rowElement.querySelector('.score-value');
        if (scoreEl) {
            scoreEl.parentElement.style.position = 'relative';
            scoreEl.insertAdjacentElement('afterend', badge);
        }

        // Animate badge
        gsap.fromTo(badge,
            { 
                opacity: 0, 
                scale: 0.5, 
                y: -10 
            },
            {
                opacity: 1,
                scale: this.config.deltaScale,
                y: 0,
                duration: 0.3,
                ease: 'back.out(1.7)',
                onComplete: () => {
                    gsap.to(badge, {
                        opacity: 0,
                        y: -20,
                        duration: 0.5,
                        delay: this.config.deltaDuration - 0.8,
                        ease: 'power2.in',
                        onComplete: () => badge.remove()
                    });
                }
            }
        );
    }

    /**
     * Highlight winner with pulse glow
     */
    _highlightWinner(rowElement) {
        if (!this.isGsapAvailable) return;

        rowElement.classList.add('winner-highlight');

        gsap.fromTo(rowElement,
            { 
                boxShadow: '0 0 0 0 rgba(255, 215, 0, 0)' 
            },
            {
                boxShadow: '0 0 30px 10px rgba(255, 215, 0, 0.6)',
                duration: this.config.highlightDuration / 2,
                ease: 'power2.inOut',
                yoyo: true,
                repeat: this.config.highlightRepeat,
                onComplete: () => {
                    rowElement.classList.remove('winner-highlight');
                    gsap.set(rowElement, { boxShadow: 'none' });
                }
            }
        );

        // Scale pulse
        gsap.to(rowElement, {
            scale: 1.02,
            duration: 0.2,
            ease: 'power2.out',
            yoyo: true,
            repeat: 1
        });
    }

    /**
     * Animate new player entering scoreboard
     */
    animateEnter(rowElement) {
        if (!this.isGsapAvailable) return;

        gsap.fromTo(rowElement,
            { opacity: 0, x: -50, scale: 0.9 },
            { opacity: 1, x: 0, scale: 1, duration: 0.5, ease: 'power2.out' }
        );
    }

    /**
     * Clear previous scores (call when starting new game)
     */
    reset() {
        this.prevScores.clear();
    }

    /**
     * Utility: Get initials from name
     */
    _getInitials(name) {
        if (!name) return '?';
        const parts = name.trim().split(' ');
        if (parts.length >= 2) {
            return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
        }
        return name.substring(0, 2).toUpperCase();
    }

    /**
     * Utility: Get avatar source URL for a player
     */
    _getPlayerAvatarSrc(player) {
        // AvatarKind: 0 = Preset, 1 = Uploaded
        if (player.avatarKind === 0 && player.avatarPresetId) {
            // Preset avatar - use SVG from assets
            return `/assets/avatars/jelly/${player.avatarPresetId}.png`;
        } else if (player.avatarKind === 1 && player.avatarUrl) {
            // Uploaded avatar - use URL
            return player.avatarUrl;
        } else {
            // Fallback - use colored circle with initials (data URI)
            const initials = this._getInitials(player.displayName);
            const colors = [
                '#e74c3c', '#3498db', '#2ecc71', '#f39c12', 
                '#9b59b6', '#1abc9c', '#e67e22', '#16a085'
            ];
            const color = colors[Math.abs(this._hashCode(player.playerId)) % colors.length];
            
            return `data:image/svg+xml,${encodeURIComponent(`
                <svg width="96" height="96" xmlns="http://www.w3.org/2000/svg">
                    <circle cx="48" cy="48" r="48" fill="${color}"/>
                    <text x="48" y="64" font-size="36" fill="white" text-anchor="middle" font-weight="bold" font-family="Arial">${initials}</text>
                </svg>
            `)}`;
        }
    }

    /**
     * Utility: Hash code for consistent color assignment
     */
    _hashCode(str) {
        let hash = 0;
        for (let i = 0; i < str.length; i++) {
            hash = ((hash << 5) - hash) + str.charCodeAt(i);
            hash |= 0;
        }
        return hash;
    }

    /**
     * Utility: Escape HTML
     */
    _escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// Export for use
window.ScoreboardAnimations = ScoreboardAnimations;
