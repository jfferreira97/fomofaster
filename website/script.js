// Smooth scroll for anchor links
document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function (e) {
        e.preventDefault();
        const target = document.querySelector(this.getAttribute('href'));
        if (target) {
            target.scrollIntoView({
                behavior: 'smooth',
                block: 'start'
            });
        }
    });
});

// Copy token address
function copyCA() {
    const input = document.getElementById('ca-input');
    input.select();
    input.setSelectionRange(0, 99999); // For mobile

    navigator.clipboard.writeText(input.value).then(() => {
        const button = document.querySelector('.token-ca button');
        const originalText = button.textContent;
        button.textContent = 'copied!';
        button.style.background = '#00ff88';

        setTimeout(() => {
            button.textContent = originalText;
            button.style.background = '';
        }, 2000);
    });
}

// Animate notification counter
function animateCounter() {
    const counter = document.getElementById('notification-count');
    const start = 10000;
    const end = Math.floor(Math.random() * 3000) + 10000;
    const duration = 2000;
    const increment = (end - start) / (duration / 16);
    let current = start;

    const timer = setInterval(() => {
        current += increment;
        if (current >= end) {
            current = end;
            clearInterval(timer);
        }
        counter.textContent = Math.floor(current).toLocaleString();
    }, 16);
}

// Run counter animation on page load
window.addEventListener('load', () => {
    animateCounter();

    // Update counter every 5 seconds
    setInterval(() => {
        animateCounter();
    }, 5000);
});

// Intersection Observer for fade-in animations
const observerOptions = {
    threshold: 0.1,
    rootMargin: '0px 0px -50px 0px'
};

const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
        if (entry.isIntersecting) {
            entry.target.style.opacity = '1';
            entry.target.style.transform = 'translateY(0)';
        }
    });
}, observerOptions);

// Apply fade-in to sections
document.addEventListener('DOMContentLoaded', () => {
    const sections = document.querySelectorAll('.feature, .flow-step, .cmd, .stack-item');
    sections.forEach(section => {
        section.style.opacity = '0';
        section.style.transform = 'translateY(20px)';
        section.style.transition = 'opacity 0.6s ease, transform 0.6s ease';
        observer.observe(section);
    });
});

// Navbar background on scroll
let lastScroll = 0;
window.addEventListener('scroll', () => {
    const nav = document.querySelector('nav');
    const currentScroll = window.pageYOffset;

    if (currentScroll > 100) {
        nav.style.background = 'rgba(10, 10, 10, 0.95)';
    } else {
        nav.style.background = 'rgba(10, 10, 10, 0.9)';
    }

    lastScroll = currentScroll;
});

// Add subtle parallax to hero
window.addEventListener('scroll', () => {
    const scrolled = window.pageYOffset;
    const hero = document.querySelector('.hero');
    if (hero && scrolled < hero.offsetHeight) {
        hero.style.transform = `translateY(${scrolled * 0.3}px)`;
        hero.style.opacity = 1 - (scrolled / hero.offsetHeight) * 0.5;
    }
});

// Random notification simulation in demo
const traders = ['@ansem', '@blknoiz06', '@frankdegods', '@loomdart', '@0xsisyphus'];
const actions = ['bought', 'sold'];
const chains = ['SOLANA', 'MONAD', 'BASE'];

function generateRandomNotification() {
    const trader = traders[Math.floor(Math.random() * traders.length)];
    const action = actions[Math.floor(Math.random() * actions.length)];
    const amount = (Math.random() * 10 + 1).toFixed(1);
    const chain = chains[Math.floor(Math.random() * chains.length)];
    const contract = Math.random().toString(36).substring(2, 7) + '...' + Math.random().toString(36).substring(2, 6);

    return {
        trader,
        action,
        amount,
        chain,
        contract
    };
}

function addDemoMessage() {
    const messagesContainer = document.querySelector('.demo-messages');
    if (!messagesContainer) return;

    const notification = generateRandomNotification();

    const msg = document.createElement('div');
    msg.className = 'msg bot';
    msg.style.opacity = '0';
    msg.style.transform = 'translateY(10px)';

    msg.innerHTML = `
        <div class="msg-header">FOMOFASTER Bot</div>
        <div class="msg-content notification">
            <strong>${notification.trader}</strong> ${notification.action} $${notification.amount}K<br>
            <code>${notification.contract}</code><br>
            <span class="chain-badge">${notification.chain}</span>
        </div>
    `;

    messagesContainer.appendChild(msg);

    // Remove oldest message if more than 5
    if (messagesContainer.children.length > 6) {
        const firstBotMsg = messagesContainer.querySelector('.msg.bot');
        if (firstBotMsg) {
            firstBotMsg.style.opacity = '0';
            setTimeout(() => firstBotMsg.remove(), 300);
        }
    }

    // Animate in
    setTimeout(() => {
        msg.style.transition = 'opacity 0.3s ease, transform 0.3s ease';
        msg.style.opacity = '1';
        msg.style.transform = 'translateY(0)';
    }, 10);
}

// Add new demo message every 4 seconds
setInterval(addDemoMessage, 4000);

// Keyboard shortcuts
document.addEventListener('keydown', (e) => {
    // Cmd/Ctrl + K to copy token address
    if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        copyCA();
    }
});
