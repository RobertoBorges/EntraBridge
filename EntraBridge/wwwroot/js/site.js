// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Mobile sidebar toggle functionality
document.addEventListener('DOMContentLoaded', function() {
    // Add a toggle button for mobile
    const header = document.querySelector('.app-header .container-fluid');
    if (header) {
        const toggleButton = document.createElement('button');
        toggleButton.className = 'navbar-toggler d-md-none';
        toggleButton.setAttribute('type', 'button');
        toggleButton.setAttribute('aria-expanded', 'false');
        toggleButton.setAttribute('aria-label', 'Toggle navigation');
        toggleButton.innerHTML = '<i class="fas fa-bars text-white"></i>';
        
        // Insert at the beginning of the header
        header.insertBefore(toggleButton, header.firstChild);
        
        // Toggle sidebar on click
        toggleButton.addEventListener('click', function() {
            const sidebar = document.getElementById('sidebarMenu');
            if (sidebar) {
                sidebar.classList.toggle('show');
                this.setAttribute('aria-expanded', sidebar.classList.contains('show'));
            }
        });
        
        // Close sidebar when clicking outside on mobile
        document.addEventListener('click', function(event) {
            const sidebar = document.getElementById('sidebarMenu');
            const toggleBtn = document.querySelector('.navbar-toggler');
            
            if (sidebar && sidebar.classList.contains('show') && 
                !sidebar.contains(event.target) && 
                !toggleBtn.contains(event.target)) {
                sidebar.classList.remove('show');
                toggleBtn.setAttribute('aria-expanded', 'false');
            }
        });
    }
    
    // Active link highlighting
    const currentPath = window.location.pathname;
    const navLinks = document.querySelectorAll('.nav-link');
    
    navLinks.forEach(link => {
        const href = link.getAttribute('href');
        if (href && (href === currentPath || (currentPath.endsWith('/') && href === currentPath.slice(0, -1)))) {
            link.classList.add('active');
        }
    });
});
