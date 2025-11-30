// Admin Panel JavaScript

document.addEventListener('DOMContentLoaded', function() {

    // Initialize Toastr
    if (typeof toastr !== 'undefined') {
        toastr.options = {
            "closeButton": true,
            "debug": false,
            "newestOnTop": true,
            "progressBar": true,
            "positionClass": "toast-top-right",
            "preventDuplicates": false,
            "onclick": null,
            "showDuration": "300",
            "hideDuration": "1000",
            "timeOut": "5000",
            "extendedTimeOut": "1000",
            "showEasing": "swing",
            "hideEasing": "linear",
            "showMethod": "fadeIn",
            "hideMethod": "fadeOut"
        };

        // Toast messages will be handled by _Layout.cshtml
    }

    // Initialize DataTables with default settings
    if ($.fn.DataTable) {
        $('.data-table').each(function() {
            const table = $(this);
            const exportButtons = table.data('export') !== false;
            
            table.DataTable({
                "pageLength": 10,
                "lengthMenu": [[10, 25, 50, 100, -1], [10, 25, 50, 100, "All"]],
                "order": [[0, "asc"]],  // تغيير من "desc" إلى "asc"
                "language": {
                    "search": "Search:",
                    "lengthMenu": "Show _MENU_ entries",
                    "info": "Showing _START_ to _END_ of _TOTAL_ entries",
                    "infoEmpty": "No entries found",
                    "infoFiltered": "(filtered from _MAX_ total entries)",
                    "paginate": {
                        "first": "First",
                        "last": "Last",
                        "next": "Next",
                        "previous": "Previous"
                    }
                },
                "dom": exportButtons ? 
                    "<'row'<'col-sm-12 col-md-6'l><'col-sm-12 col-md-6'f>>" +
                    "<'row'<'col-sm-12'tr>>" +
                    "<'row'<'col-sm-12 col-md-5'i><'col-sm-12 col-md-7'pB>>" :
                    "<'row'<'col-sm-12 col-md-6'l><'col-sm-12 col-md-6'f>>" +
                    "<'row'<'col-sm-12'tr>>" +
                    "<'row'<'col-sm-12 col-md-5'i><'col-sm-12 col-md-7'p>>",
                "buttons": exportButtons ? [
                    {
                        extend: 'csv',
                        text: '<i class="fas fa-file-csv"></i> CSV',
                        className: 'btn btn-sm btn-outline-primary'
                    },
                    {
                        extend: 'excel',
                        text: '<i class="fas fa-file-excel"></i> Excel',
                        className: 'btn btn-sm btn-outline-success'
                    },
                    {
                        extend: 'pdf',
                        text: '<i class="fas fa-file-pdf"></i> PDF',
                        className: 'btn btn-sm btn-outline-danger'
                    },
                    {
                        extend: 'print',
                        text: '<i class="fas fa-print"></i> Print',
                        className: 'btn btn-sm btn-outline-secondary'
                    }
                ] : [],
                "responsive": true,
                "autoWidth": false
            });
        });
    }

    // Confirmation dialogs with custom modal
    let confirmCallback = null;
    let confirmElement = null;

    function showConfirmModal(message, callback, element) {
        $('#adminConfirmMessage').text(message);
        confirmCallback = callback;
        confirmElement = element;
        const modalElement = document.getElementById('adminConfirmModal');
        if (modalElement && typeof bootstrap !== 'undefined') {
            const modal = new bootstrap.Modal(modalElement);
            modal.show();
        }
    }

    $('#adminConfirmBtn').on('click', function() {
        if (confirmCallback) {
            confirmCallback();
            confirmCallback = null;
            confirmElement = null;
        }
        const modalElement = document.getElementById('adminConfirmModal');
        if (modalElement && typeof bootstrap !== 'undefined') {
            const modalInstance = bootstrap.Modal.getInstance(modalElement);
            if (modalInstance) {
                modalInstance.hide();
            }
        }
    });

    // Use event delegation to handle dynamically added elements (e.g., from DataTables)
    $(document).on('click', '.confirm-delete', function(e) {
        e.preventDefault();
        console.log('Delete button clicked');
        const $element = $(this);
        let url = $element.attr('href') || $element.data('url');
        console.log('URL from href:', $element.attr('href'));
        console.log('URL from data-url:', $element.data('url'));
        console.log('data-method:', $element.data('method'));
        
        // If button is inside a form, get the form action
        if (!url) {
            const $form = $element.closest('form');
            if ($form.length > 0) {
                url = $form.attr('action');
            }
        }
        
        const message = $element.data('message') || 'Are you sure you want to delete this item?';
        
        if (!url) {
            console.error('No URL found for confirm-delete');
            return;
        }
        
        console.log('Showing confirm modal with URL:', url);
        showConfirmModal(message, function() {
            console.log('Confirm callback executed');
            // If button is inside a form, submit the form directly
            const $form = $element.closest('form');
            if ($form.length > 0) {
                console.log('Submitting form');
                $form.submit();
            } else if ($element.data('method') === 'post') {
                console.log('Creating POST form for URL:', url);
                // Create a form for POST request
                const form = $('<form>', {
                    'method': 'POST',
                    'action': url
                });
                const token = $('input[name="__RequestVerificationToken"]').val();
                console.log('AntiForgeryToken found:', token ? 'Yes' : 'No');
                form.append($('<input>', {
                    'type': 'hidden',
                    'name': '__RequestVerificationToken',
                    'value': token
                }));
                $('body').append(form);
                console.log('Submitting POST form');
                form.submit();
            } else {
                console.log('Redirecting to URL:', url);
                window.location.href = url;
            }
        }, $element);
    });

    $('.confirm-action').on('click', function(e) {
        e.preventDefault();
        const $element = $(this);
        let url = $element.attr('href') || $element.data('url');
        
        // If button is inside a form, get the form action
        if (!url) {
            const $form = $element.closest('form');
            if ($form.length > 0) {
                url = $form.attr('action');
            }
        }
        
        const message = $element.data('message') || 'Are you sure you want to perform this action?';
        
        // If still no URL, try to get it from the form's action attribute
        if (!url) {
            const $form = $element.closest('form');
            if ($form.length > 0) {
                url = $form.attr('action');
            }
        }
        
        if (!url) {
            console.error('No URL found for confirm-action');
            return;
        }
        
        showConfirmModal(message, function() {
            // If button is inside a form, submit the form directly
            const $form = $element.closest('form');
            if ($form.length > 0) {
                $form.submit();
            } else if ($element.data('method') === 'post') {
                // Create a form for POST request
                const form = $('<form>', {
                    'method': 'POST',
                    'action': url
                });
                form.append($('<input>', {
                    'type': 'hidden',
                    'name': '__RequestVerificationToken',
                    'value': $('input[name="__RequestVerificationToken"]').val()
                }));
                $('body').append(form);
                form.submit();
            } else {
                window.location.href = url;
            }
        }, $element);
    });
});

// Helper function to show toast messages
function showToast(type, message) {
    if (typeof toastr !== 'undefined') {
        toastr[type](message);
    } else {
        alert(message);
    }
}

