// Sidebar Dropdown Toggle
document.addEventListener('DOMContentLoaded', function() {
    const sidebarLogin = document.getElementById('sidebar-login');
    const userDropdown = document.getElementById('user-dropdown');
    const loginIcon = document.getElementById('login-icon');
    const loginText = document.getElementById('login-text');
    const userName = document.getElementById('user-name');

    if (sidebarLogin && userDropdown) {
        sidebarLogin.addEventListener('click', function(e) {
            // Only toggle if user is logged in
            if (userName && !userName.classList.contains('hidden')) {
                e.preventDefault();
                userDropdown.classList.toggle('hidden');
            }
        });

        // Close dropdown when clicking outside
        document.addEventListener('click', function(e) {
            if (userDropdown && !sidebarLogin.contains(e.target) && !userDropdown.contains(e.target)) {
                userDropdown.classList.add('hidden');
            }
        });
    }

    // Smooth scroll for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            const href = this.getAttribute('href');
            if (href !== '#' && href.length > 1) {
                e.preventDefault();
                const target = document.querySelector(href);
                
                if (target) {
                    // Element exists on current page - smooth scroll
                    target.scrollIntoView({
                        behavior: 'smooth',
                        block: 'start'
                    });
                } else {
                    // Element doesn't exist - redirect to Home with hash
                    const homeUrl = '/Home';
                    window.location.href = homeUrl + href;
                }
            }
        });
    });
});

