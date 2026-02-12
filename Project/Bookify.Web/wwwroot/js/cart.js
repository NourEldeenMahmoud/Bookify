// Cart Management using Session (server-side) with API calls

// Add item to cart via API
async function addToCartViaAPI(roomId, checkIn, checkOut, numberOfGuests) {
    try {
        const response = await fetch('/Cart/Add', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                roomId: roomId,
                checkIn: checkIn,
                checkOut: checkOut,
                numberOfGuests: numberOfGuests
            })
        });

        if (!response.ok) {
            const errorData = await response.json().catch(() => ({ error: 'Failed to add item to cart' }));
            throw new Error(errorData.error || 'Failed to add item to cart');
        }

        const result = await response.json();
        await updateCartBadge();
        return result;
    } catch (error) {
        console.error('Error adding item to cart:', error);
        throw error;
    }
}

// Update cart item via API
async function updateCartItemViaAPI(roomId, checkIn, checkOut, numberOfGuests) {
    try {
        const response = await fetch('/Cart/Update', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                roomId: roomId,
                checkIn: checkIn,
                checkOut: checkOut,
                numberOfGuests: numberOfGuests
            })
        });

        if (!response.ok) {
            const errorData = await response.json().catch(() => ({ error: 'Failed to update cart item' }));
            throw new Error(errorData.error || 'Failed to update cart item');
        }

        const result = await response.json();
        return result;
    } catch (error) {
        console.error('Error updating cart item:', error);
        throw error;
    }
}

// Remove item from cart via API
async function removeFromCartViaAPI(roomId) {
    try {
        const response = await fetch(`/Cart/Remove?roomId=${roomId}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            }
        });

        if (!response.ok) {
            const errorData = await response.json().catch(() => ({ error: 'Failed to remove item from cart' }));
            throw new Error(errorData.error || 'Failed to remove item from cart');
        }

        const result = await response.json();
        await updateCartBadge();
        return result;
    } catch (error) {
        console.error('Error removing item from cart:', error);
        throw error;
    }
}

// Legacy localStorage functions (kept for backward compatibility if needed)
const CART_STORAGE_KEY = 'bookify_cart';

// Get cart from localStorage (legacy)
function getCart() {
    try {
        const cartJson = localStorage.getItem(CART_STORAGE_KEY);
        if (!cartJson) {
            return { items: [] };
        }
        const cart = JSON.parse(cartJson);
        return cart.items ? cart : { items: cart.items || [] };
    } catch (error) {
        console.error('Error reading cart from localStorage:', error);
        return { items: [] };
    }
}

// Save cart to localStorage (legacy)
function saveCart(cart) {
    try {
        localStorage.setItem(CART_STORAGE_KEY, JSON.stringify(cart));
        updateCartBadge();
    } catch (error) {
        console.error('Error saving cart to localStorage:', error);
    }
}

// Add item to cart (legacy - uses localStorage)
function addToCart(cartItem) {
    const cart = getCart();
    
    // Check if item already exists
    const existingIndex = cart.items.findIndex(item => item.roomId === cartItem.roomId);
    
    if (existingIndex >= 0) {
        // Update existing item
        cart.items[existingIndex] = cartItem;
    } else {
        // Add new item
        cart.items.push(cartItem);
    }
    
    saveCart(cart);
    return cart;
}

// Remove item from cart
function removeFromCart(roomId) {
    const cart = getCart();
    cart.items = cart.items.filter(item => item.roomId !== roomId);
    saveCart(cart);
    return cart;
}

// Update cart item
function updateCartItem(roomId, checkIn, checkOut, numberOfGuests) {
    const cart = getCart();
    const itemIndex = cart.items.findIndex(item => item.roomId === roomId);
    
    if (itemIndex >= 0) {
        cart.items[itemIndex].checkIn = checkIn;
        cart.items[itemIndex].checkOut = checkOut;
        cart.items[itemIndex].numberOfGuests = numberOfGuests;
        
        // Recalculate nights and subtotal
        const checkInDate = new Date(checkIn);
        const checkOutDate = new Date(checkOut);
        const nights = Math.ceil((checkOutDate - checkInDate) / (1000 * 60 * 60 * 24));
        cart.items[itemIndex].numberOfNights = nights;
        cart.items[itemIndex].subtotal = cart.items[itemIndex].pricePerNight * nights;
        
        saveCart(cart);
    }
    
    return cart;
}

// Clear cart
function clearCart() {
    localStorage.removeItem(CART_STORAGE_KEY);
    updateCartBadge();
}

// Calculate cart totals
function calculateCartTotal() {
    const cart = getCart();
    const subtotal = cart.items.reduce((sum, item) => {
        const nights = item.numberOfNights || calculateNights(item.checkIn, item.checkOut);
        return sum + (item.pricePerNight * nights);
    }, 0);
    
    const taxRate = 0.14; // 14%
    const taxAmount = subtotal * taxRate;
    const totalAmount = subtotal + taxAmount;
    
    return {
        subtotal: subtotal,
        taxRate: taxRate,
        taxAmount: taxAmount,
        totalAmount: totalAmount,
        itemCount: cart.items.length
    };
}

// Calculate nights between two dates
function calculateNights(checkIn, checkOut) {
    const checkInDate = new Date(checkIn);
    const checkOutDate = new Date(checkOut);
    return Math.ceil((checkOutDate - checkInDate) / (1000 * 60 * 60 * 24));
}

// Update cart badge in navigation (fetches count from server)
async function updateCartBadge() {
    try {
        const response = await fetch('/Cart/GetCartCount');
        if (response.ok) {
            const data = await response.json();
            const itemCount = data.count || 0;
            
            const badgeElements = document.querySelectorAll('.cart-badge, #cartBadge');
            badgeElements.forEach(badge => {
                if (itemCount > 0) {
                    badge.textContent = itemCount;
                    badge.style.display = 'inline-block';
                } else {
                    badge.style.display = 'none';
                }
            });
        }
    } catch (error) {
        console.error('Error updating cart badge:', error);
        // Fallback to localStorage if API fails
        const cart = getCart();
        const itemCount = cart.items ? cart.items.length : 0;
        
        const badgeElements = document.querySelectorAll('.cart-badge, #cartBadge');
        badgeElements.forEach(badge => {
            if (itemCount > 0) {
                badge.textContent = itemCount;
                badge.style.display = 'inline-block';
            } else {
                badge.style.display = 'none';
            }
        });
    }
}

// Initialize cart badge on page load
document.addEventListener('DOMContentLoaded', function() {
    updateCartBadge();
});

// Export functions for use in other scripts
if (typeof window !== 'undefined') {
    window.cartManager = {
        // New API-based functions (preferred)
        addToCartViaAPI,
        updateCartItemViaAPI,
        removeFromCartViaAPI,
        updateCartBadge,
        
        // Legacy localStorage functions (for backward compatibility)
        getCart,
        saveCart,
        addToCart,
        removeFromCart,
        updateCartItem,
        clearCart,
        calculateCartTotal
    };
}

