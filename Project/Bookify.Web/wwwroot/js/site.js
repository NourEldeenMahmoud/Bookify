// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Rooms Pagination with AJAX (No Reload)
$(document).ready(function() {
    let isLoading = false;
    
    // Handle pagination clicks
    $(document).on('click', '.pagination-link', function(e) {
        e.preventDefault();

        // If there is an active search handled by Index.cshtml (SearchRooms),
        // don't run this fallback pagination (to keep filters applied).
        if (window.currentSearchParams && Object.keys(window.currentSearchParams).length > 0) {
            return;
        }
        
        if (isLoading) return;
        
        const page = $(this).data('page');
        if (!page) return;
        
        isLoading = true;
        
        // Store current absolute scroll position
        const currentScrollPosition = $(window).scrollTop();
        
        // Get rooms container and pagination
        const $roomsContainer = $('#rooms-container');
        const $roomsSection = $('#rooms');
        
        // Add loading class to clicked link
        $('.pagination-link').removeClass('loading');
        $(this).addClass('loading');
        
        // Fade out current content smoothly
        $roomsContainer.css({
            'opacity': '0',
            'transform': 'translateY(8px)',
            'transition': 'opacity 0.2s cubic-bezier(0.4, 0, 0.2, 1), transform 0.2s cubic-bezier(0.4, 0, 0.2, 1)'
        });
        
        // After fade out, show loading and make request
        setTimeout(function() {
            $roomsContainer.html('<div class="col-12 text-center py-5"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div></div>');
            $roomsContainer.css({
                'opacity': '0.5',
                'transform': 'translateY(0)',
                'transition': 'opacity 0.15s ease'
            });
            
            // Make AJAX request
            $.ajax({
                url: '/Home/GetRoomsPartial',
                type: 'GET',
                data: { page: page, pageSize: 8 },
                success: function(response) {
                    // Replace rooms section with new content
                    $roomsSection.html(response);
                    
                    // Get new container
                    const $newRoomsContainer = $('#rooms-container');
                    
                    // Set initial state for fade in
                    $newRoomsContainer.css({
                        'opacity': '0',
                        'transform': 'translateY(8px)',
                        'transition': 'opacity 0.3s cubic-bezier(0.4, 0, 0.2, 1), transform 0.3s cubic-bezier(0.4, 0, 0.2, 1)'
                    });
                    
                    // Restore scroll position immediately
                    $(window).scrollTop(currentScrollPosition);
                    
                    // Force reflow to ensure transition works
                    requestAnimationFrame(function() {
                        requestAnimationFrame(function() {
                            $newRoomsContainer.css({
                                'opacity': '1',
                                'transform': 'translateY(0)'
                            });
                            
                            // Remove loading class after animation
                            setTimeout(function() {
                                $('.pagination-link').removeClass('loading');
                                isLoading = false;
                            }, 300);
                        });
                    });
                },
                error: function(xhr, status, error) {
                    console.error('Error loading rooms:', error);
                    // Restore opacity on error
                    $roomsContainer.css({
                        'opacity': '1',
                        'transform': 'translateY(0)'
                    });
                    alert('Error loading the page.');
                    $('.pagination-link').removeClass('loading');
                    isLoading = false;
                }
            });
        }, 200);
    });
});