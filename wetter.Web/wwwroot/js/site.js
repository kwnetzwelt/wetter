class Particle {
    constructor(canvas, type) {
        this.canvas = canvas;
        this.ctx = canvas.getContext('2d');
        this.type = type;
        this.opacity = 0; 
        this.fadeState = 'in'; 
        this.reset();
    }

    reset() {
        this.x = Math.random() * this.canvas.width;
        this.y = Math.random() * this.canvas.height;
        this.size = 2 + Math.random() * 3;
        this.speedX = 0;
        this.speedY = 0;
        this.maxOpacity = 0.2 + Math.random() * 0.5;
        this.color = '#fff';
        this.angle = Math.random() * Math.PI * 2;
        this.spin = Math.random() * 0.1 - 0.05;

        switch (this.type) {
            case 'rain':
                this.speedY = 25 + Math.random() * 15;
                this.speedX = (Math.random() * 2 - 1);
                this.size = 0.5 + Math.random() * 0.5;
                this.color = '#a0c4ff';
                this.y = -50;
                break;
            case 'snow':
                this.speedY = 1 + Math.random() * 2;
                this.speedX = Math.random() * 1 - 0.5;
                this.size = 2 + Math.random() * 3;
                this.y = -20;
                break;
            case 'hail':
                this.speedY = 15 + Math.random() * 5;
                this.size = 3 + Math.random() * 2;
                this.y = -20;
                break;
            case 'leaf':
                this.color = ['#e67e22', '#d35400', '#f39c12'][Math.floor(Math.random() * 3)];
                this.speedY = 1 + Math.random() * 2;
                this.speedX = Math.random() * 2 - 1;
                this.size = 10 + Math.random() * 5;
                this.y = -50;
                break;
            case 'bee':
                this.color = '#f1c40f';
                this.speedY = Math.random() * 4 - 2;
                this.speedX = Math.random() * 6 - 3;
                this.size = 4;
                this.maxOpacity = 0.8;
                break;
            case 'dandelion':
                this.speedY = 0.5 + Math.random();
                this.speedX = Math.random() * 1 - 0.5;
                this.size = 2;
                this.maxOpacity = 0.4;
                this.y = -20;
                break;
            case 'cloud':
                this.size = 100 + Math.random() * 150;
                this.speedX = 0.2 + Math.random() * 0.5;
                this.speedY = 0;
                this.maxOpacity = 0.1;
                this.x = -this.size;
                break;
            case 'star':
                this.size = Math.random() * 1.5;
                this.maxOpacity = 0.1 + Math.random() * 0.8;
                this.twinkleSpeed = 0.005 + Math.random() * 0.02;
                break;
        }
    }

    update() {
        if (this.fadeState === 'in') {
            this.opacity += 0.01;
            if (this.opacity >= this.maxOpacity) {
                this.opacity = this.maxOpacity;
                this.fadeState = 'stable';
            }
        } else if (this.fadeState === 'out') {
            this.opacity -= 0.01;
            if (this.opacity <= 0) {
                this.opacity = 0;
                return false;
            }
        }

        if (this.type === 'star') {
            if (this.fadeState === 'stable') {
                this.opacity += this.twinkleSpeed;
                if (this.opacity > this.maxOpacity || this.opacity < 0.1) this.twinkleSpeed *= -1;
            }
            return true;
        }

        this.x += this.speedX;
        this.y += this.speedY;
        this.angle += this.spin;

        if (this.type === 'bee') {
            this.speedX += Math.random() * 0.4 - 0.2;
            this.speedY += Math.random() * 0.4 - 0.2;
        }

        if (this.y > this.canvas.height + 50 || this.x > this.canvas.width + 200 || this.x < -200) {
            if (this.fadeState !== 'out') this.reset();
        }
        return true;
    }

    draw() {
        if (this.opacity <= 0) return;
        this.ctx.globalAlpha = this.opacity;
        this.ctx.fillStyle = this.color;

        switch (this.type) {
            case 'rain':
                this.ctx.beginPath();
                this.ctx.moveTo(this.x, this.y);
                this.ctx.lineTo(this.x + this.speedX * 2, this.y + 40); 
                this.ctx.strokeStyle = this.color;
                this.ctx.lineWidth = this.size;
                this.ctx.stroke();
                break;
            case 'cloud':
                this.ctx.beginPath();
                this.ctx.arc(this.x, this.y, this.size, 0, Math.PI * 2);
                this.ctx.fill();
                break;
            case 'bee':
                this.ctx.fillStyle = '#000';
                this.ctx.fillRect(this.x, this.y, this.size, this.size);
                this.ctx.fillStyle = '#f1c40f';
                this.ctx.fillRect(this.x - 2, this.y, 2, this.size);
                break;
            case 'leaf':
                this.ctx.save();
                this.ctx.translate(this.x, this.y);
                this.ctx.rotate(this.angle);
                this.ctx.beginPath();
                this.ctx.ellipse(0, 0, this.size, this.size / 2, 0, 0, Math.PI * 2);
                this.ctx.fill();
                this.ctx.restore();
                break;
            default:
                this.ctx.beginPath();
                this.ctx.arc(this.x, this.y, this.size, 0, Math.PI * 2);
                this.ctx.fill();
        }
    }
}

class EffectManager {
    constructor() {
        this.particles = [];
        this.canvas = null;
        this.ctx = null;
        this.isAnimating = false;
    }

    tryInit() {
        const canvas = document.getElementById('particles-canvas');
        if (!canvas) return false;
        
        if (this.canvas !== canvas) {
            this.canvas = canvas;
            this.ctx = this.canvas.getContext('2d');
            this.resize();
            this.particles.forEach(p => {
                p.canvas = this.canvas;
                p.ctx = this.ctx;
            });
        }
        
        if (!this.isAnimating) {
            this.isAnimating = true;
            this.animate();
        }
        return true;
    }

    resize() {
        if (!this.canvas) return;
        this.canvas.width = window.innerWidth;
        this.canvas.height = window.innerHeight;
    }

    setEffects(weatherCode, month, isDay) {
        if (!this.tryInit()) {
            setTimeout(() => this.setEffects(weatherCode, month, isDay), 500);
            return;
        }

        const requiredTypes = new Set();
        if (!isDay) requiredTypes.add('star');
        if (weatherCode > 0) requiredTypes.add('cloud');
        
        if (weatherCode >= 95) { requiredTypes.add('rain'); requiredTypes.add('hail'); }
        else if (weatherCode >= 80 || (weatherCode >= 61 && weatherCode <= 65) || (weatherCode >= 51 && weatherCode <= 55)) requiredTypes.add('rain');
        else if (weatherCode >= 71 && weatherCode <= 77) requiredTypes.add('snow');

        if (month >= 3 && month <= 8) { requiredTypes.add('bee'); requiredTypes.add('dandelion'); }
        else if (month >= 9 && month <= 11) requiredTypes.add('leaf');

        this.particles.forEach(p => {
            if (!requiredTypes.has(p.type)) p.fadeState = 'out';
        });

        const activeTypes = new Set(this.particles.filter(p => p.fadeState !== 'out').map(p => p.type));
        requiredTypes.forEach(type => {
            if (!activeTypes.has(type)) {
                let count = 0;
                if (type === 'star') count = 150;
                else if (type === 'cloud') count = 6;
                else if (type === 'rain') count = 100;
                else if (type === 'hail') count = 20;
                else if (type === 'snow') count = 80;
                else if (type === 'bee') count = 5;
                else if (type === 'dandelion') count = 15;
                else if (type === 'leaf') count = 25;

                for (let i = 0; i < count; i++) {
                    this.particles.push(new Particle(this.canvas, type));
                }
            }
        });
    }

    animate() {
        if (!this.ctx) {
            this.isAnimating = false;
            return;
        }
        this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
        this.particles = this.particles.filter(p => {
            const alive = p.update();
            if (alive) p.draw();
            return alive;
        });
        requestAnimationFrame(() => this.animate());
    }
}

window.effectManager = new EffectManager();
window.addEventListener('resize', () => { if (window.effectManager) window.effectManager.resize(); });
