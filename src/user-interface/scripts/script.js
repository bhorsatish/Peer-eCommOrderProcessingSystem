const API_BASE_URL = "http://localhost:5296";
//const API_BASE_URL = "https://7fxmddn4-7019.inc1.devtunnels.ms";

// Map API Role IDs to your UI Role Strings
const ROLE_MAP = {
    0: 'admin',
    1: 'backend',
    2: 'client'
};

let currentRole = null;
let currentUser = null;
let currentUserId = null;
let products = [];
let cart = [];
let orders = [];
let displayOrders = [];
let currentPage = 1;
let pageSize = 10;
let totalPages = 1;
let paginationFilters = {
    userId: null,      // For client role
    statusId: null     // For admin role
};
const STATUS_MAP = {
    1: { text: 'In Cart', class: 'processing' },
    2: { text: 'New Order - COD', class: 'processing' },
    3: { text: 'New Order - Paid', class: 'processing' },
    4: { text: 'Packaged In Warehouse', class: 'processing' },
    5: { text: 'Delivered', class: 'delivered' },
    6: { text: 'Cancellation Requested', class: 'cancelled' },
    7: { text: 'Cancellation to be paid', class: 'cancelled' },
    8: { text: 'Cancelled', class: 'cancelled' },
    9: { text: 'In Transit', class: 'shipped' },
    10: { text: 'Placed', class: 'processing' }
};

switchTab('auth');
initHome();

async function handleAuth(event, action) {
    event.preventDefault();
         
    const isLogin = action === 'login';
    const username = document.getElementById(`${action}-username`).value;
    const password = document.getElementById(`${action}-password`).value;
    const endpoint = isLogin ? '/users/authenticate' : '/users/add';

    try {
        const response = await fetch(`${API_BASE_URL}${endpoint}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, password })
        });

        if (response.ok) {
            const data = await response.json();
                         
            if (isLogin) {
                showToast(data.message || "Authentication successful");
                currentUser = data.username;
                currentRole = ROLE_MAP[data.roleId];
                currentUserId = data.id || data.userId;
                
                document.getElementById('main-header').style.display = 'flex';
                applyRoleUI();
                
                // ROUTING LOGIC
                if (currentRole === 'client') {
                    paginationFilters.userId = currentUserId;
                    paginationFilters.statusId = null;
                    
                    await initHome(); // Must load products first
                    await loadClientCart(currentUserId); // Fills the cart with Status 1
                    
                    currentPage = 1;
                    await renderOrders(1);
                    switchTab('home'); 
                } 
                else if (currentRole === 'admin' || currentRole === 'backend') {
                    paginationFilters.userId = null;
                    paginationFilters.statusId = 2; // Default to Processing (2) instead of Cart (1)
                    
                    currentPage = 1;
                    await renderOrders(1);
                    switchTab('orders'); 
                }
            } else {
                showToast("Registration successful! Please login.");
                toggleAuthMode('login'); 
            }
        } else {
            showToast(isLogin ? "Invalid credentials." : "Error creating account.");
        }
    } catch (error) {
        console.error("Auth error:", error);
        showToast("Network error.");
    }
}

function toggleAuthMode(mode) {
    if (mode === 'login') {
        document.getElementById('login-panel').style.display = 'block';
        document.getElementById('register-panel').style.display = 'none';
    } else {
        document.getElementById('login-panel').style.display = 'none';
        document.getElementById('register-panel').style.display = 'block';
    }
}

function logout() {
    currentRole = null;
    currentUser = null;
    
    // 1. Reset data in memory
    paginationFilters = { userId: null, statusId: null };
    orders = [];
    cart = [];

    document.getElementById('main-header').style.display = 'none';

    // 2. Clear login forms
    document.getElementById('login-username').value = '';
    document.getElementById('login-password').value = '';

    // 3. Clear UI containers (prevents next user from seeing old HTML)
    document.getElementById('orders-container').innerHTML = '';
    document.getElementById('cart-items-container').innerHTML = '';
    document.getElementById('cart-count').innerText = '0';
    document.getElementById('cart-total').innerText = '0.00';

    switchTab('auth');
    showToast("Logged out successfully.");
}

async function initHome() {
    const grid = document.getElementById('product-grid');
    grid.innerHTML = "<p>Loading products...</p>";

    try {
        // Triggering the API call to get the JSON result
        const response = await fetch(`${API_BASE_URL}/products`);
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        products = await response.json(); // Update the products variable with API data
        
        if (products.length === 0) {
            grid.innerHTML = "<p>No products available at the moment.</p>";
            return;
        }

        renderProductGrid();
    } catch (error) {
        console.error("Failed to fetch products:", error);
        grid.innerHTML = "<p style='color: var(--danger);'>Error loading products. Please ensure the API is running at https://localhost:7019</p>";
    }
}

function renderProductGrid(list = products) {
    const grid = document.getElementById('product-grid');

    if (list.length === 0) {
        grid.innerHTML = "<p>No products match your search.</p>";
        return;
    }

    grid.innerHTML = list.map(p => `
        <div class="product-card">
            <div class="product-image" style="font-size: 40px; display:flex; justify-content:center; align-items:center;">
                ${p.icon || '📦'}
            </div>
            <div class="product-title">${p.name}</div>
            <div class="product-price">$${p.price.toFixed(2)}</div>
            <button class="btn-primary" onclick="addToCart(${p.id})">Add to Cart</button>
        </div>
    `).join('');
}

function filterProducts() {
    const query = document.getElementById('search-input').value.trim().toLowerCase();

    if (!query) {
        renderProductGrid();
        return;
    }

    const filtered = products.filter(p => p.name.toLowerCase().includes(query));
    renderProductGrid(filtered);
}

function simulateRoleLogin(role) {
    currentRole = role;
    applyRoleUI();
    // Redirect staff away from the shopping front-end
    if (currentRole !== 'client') switchTab('orders');
    else switchTab('home');
}

function switchTab(tabId) {
    document.querySelectorAll('.nav-link').forEach(link => link.classList.remove('active'));
    const activeTabButton = document.getElementById(`tab-${tabId}`);
    if (activeTabButton) activeTabButton.classList.add('active');

    document.querySelectorAll('.view-section').forEach(view => view.classList.remove('active'));
    document.getElementById(`view-${tabId}`).classList.add('active');

    if (tabId === 'cart') renderCart();
    if (tabId === 'orders') renderOrders(); 
}

function showToast(message) {
    const container = document.getElementById('toast-container');
    const toast = document.createElement('div');
    toast.className = 'toast';
    toast.innerText = message;
    container.appendChild(toast);
    setTimeout(() => { toast.remove(); }, 3000);
}

async function renderOrders(pageNumber = 1) {
    const container = document.getElementById('orders-container');
    container.innerHTML = "<p>Fetching orders...</p>";
    
    try {
        let apiUrl = '';

        if (currentRole === 'client') {
            apiUrl = `${API_BASE_URL}/orders/user/${paginationFilters.userId}?pageNumber=${pageNumber}&pageSize=${pageSize}`;
        } else if (currentRole === 'admin' || currentRole === 'backend') {
            const statusIdToFetch = paginationFilters.statusId || 2; // Default to 2 (Processing)
            
            if (paginationFilters.userId) {
                apiUrl = `${API_BASE_URL}/orders/user/${paginationFilters.userId}?pageNumber=${pageNumber}&pageSize=${pageSize}`;
            } else {
                apiUrl = `${API_BASE_URL}/orders/status/${statusIdToFetch}?pageNumber=${pageNumber}&pageSize=${pageSize}`;
            }

            const statusDropdown = document.getElementById('filter-status-id');
            if (statusDropdown) statusDropdown.value = statusIdToFetch;
        }

        const response = await fetch(apiUrl, { headers: { 'Authorization': `Bearer ROLE_${currentRole}` } });
        if (!response.ok) throw new Error("API Offline");

        const result = await response.json();
        orders = result.data || result; 
        
        if (result.pageNumber) {
            currentPage = result.pageNumber;
            totalPages = result.totalPages;
            pageSize = result.pageSize;
        }
    } catch (error) {
        console.warn("Order fetch failed:", error);
        orders = [];
    }

    // --- CRITICAL FIX: Hide Status 1 (Cart Items) from the Order View ---
    displayOrders = orders;
    if (currentRole === 'client') {
        displayOrders = orders.filter(order => order.statusId > 1);
    }

    if (!displayOrders || displayOrders.length === 0) {
        container.innerHTML = "<p>No orders found.</p>";
        renderPaginationControls();
        return;
    }

    container.innerHTML = displayOrders.map(order => {
        let actionControls = '';
        
        // Client buttons
        if (currentRole === 'client') {
            actionControls = `
                <button class="btn-info" onclick="trackOrder('${order.orderId}')">Track Order</button>
                ${(order.statusId === 2 || order.statusId === 3 || order.statusId === 10) ? `<button class="btn-danger" onclick="cancelOrder('${order.orderId}')">Cancel Order</button>` : ''}
            `;
        } 
        // Admin/Backend buttons
        else if (currentRole === 'admin' || currentRole === 'backend') {
            let nextStatusId = null;
            let buttonText = "";

            // Determine the next valid status based on current status and payment mode
            if (order.statusId === 2 || order.statusId === 3) {
                nextStatusId = 4;
                buttonText = "Advance to Packaged";
            } else if (order.statusId === 4) {
                nextStatusId = 9;
                buttonText = "Advance to In Transit";
            } else if (order.statusId === 9) {
                nextStatusId = 5;
                buttonText = "Advance to Delivered";
            } else if (order.statusId === 6) {
                if (order.paymentMode === 1) {
                    nextStatusId = 7;
                    buttonText = "Advance to Cancelled (To be paid)";
                } else {
                    nextStatusId = 8;
                    buttonText = "Advance to Cancelled";
                }
            } else if (order.statusId === 7) {
                nextStatusId = 8;
                buttonText = "Advance to Cancelled";
            }

            // Render the appropriate control
            if (order.statusId === 5 || order.statusId === 8) {
                actionControls = `<span style="font-weight: 600; color: var(--text-muted);">Final State Reached</span>`;
            } else if (nextStatusId !== null) {
                actionControls = `
                    <button class="btn-primary" onclick="updateOrderStatus('${order.orderId}', ${nextStatusId})">
                        ${buttonText}
                    </button>
                `;
            } else {
                // Fallback for states like 10 (System Approval) if visible to admins
                actionControls = `<span style="font-weight: 600; color: var(--secondary);">Pending User Action</span>`;
            }
        }

        // 1. Cross-reference the product details using the Product ID
        const product = products.find(p => p.id === order.productId) || { name: 'Unknown Product', price: 0, icon: '📦' };
        
        // 2. Map human-readable statuses and payment modes
        const statusInfo = STATUS_MAP[order.statusId] || { text: 'Processing', class: 'processing' };
        const paymentText = order.paymentMode === 1 ? 'Online Payment' : 'Cash on Delivery';
        
        // 3. Format dates dynamically based on the current status
        let dateLabel = 'Placed';
        let dateValue = order.orderedOn || order.addedOn;
        if (order.statusId === 5 && order.deliveredOn) { 
            dateLabel = 'Delivered'; 
            dateValue = order.deliveredOn; 
        } else if (order.statusId >= 6 && order.statusId <= 8 && order.cancelledOn) { 
            dateLabel = 'Cancelled'; 
            dateValue = order.cancelledOn; 
        }
        
        const formattedDate = dateValue ? new Date(dateValue).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) : 'Pending';

        // 4. Return the new HTML block WITHOUT the Order ID
        return `
            <div class="order-item" id="order-box-${order.id}">
                <div class="order-item-content">
                    <div class="order-product-icon">${product.icon}</div>
                    <div class="order-details-wrapper">
                        
                        <div class="order-header-row">
                            <h3 class="order-product-name">
                                ${product.name}
                                <span class="status ${statusInfo.class}">${statusInfo.text}</span>
                            </h3>
                            <span class="order-price">$${product.price.toFixed(2)}</span>
                        </div>
                        
                        <div class="order-meta-grid">
                            <div class="meta-column">
                                <p><strong>Date ${dateLabel}:</strong> ${formattedDate}</p>
                                <p><strong>Payment:</strong> ${paymentText}</p>
                            </div>
                            <div class="meta-column">
                                <p><strong>Deliver To:</strong> ${order.orderedFor || 'N/A'}</p>
                                <p><strong>Address:</strong> ${order.deliveryAddress ? order.deliveryAddress.replace(/ \(Payment: .*\)/, '') : 'N/A'}</p>
                            </div>
                        </div>

                    </div>
                </div>
                <div class="order-actions">
                    ${actionControls}
                </div>
            </div>
        `;
    }).join('');

    renderPaginationControls();
}

function renderPaginationControls() {
    const container = document.getElementById('orders-container');
    let paginationHTML = `
        <div style="display: flex; justify-content: center; align-items: center; gap: 10px; margin-top: 20px; padding-top: 20px; border-top: 1px solid var(--border);">
            ${currentPage > 1 ? `<button class="btn-primary" onclick="renderOrders(${currentPage - 1})">← Previous</button>` : ''}
            
            <span style="font-weight: bold; padding: 0 15px;">
                Page <strong>${currentPage}</strong> of <strong>${totalPages}</strong>
            </span>
            
            ${currentPage < totalPages ? `<button class="btn-primary" onclick="renderOrders(${currentPage + 1})">Next →</button>` : ''}
        </div>
    `;
    
    container.innerHTML += paginationHTML;
}

function getCurrentUserId() {
    return paginationFilters.userId;
}

function setStatusFilter(statusId) {
    paginationFilters.statusId = statusId;
    currentPage = 1;
    renderOrders(1);
}

async function addToCart(productId) {
    const product = products.find(p => p.id === productId);
    if (!product) {
        showToast("Could not find that product. Please refresh and try again.");
        return;
    }

    if (!paginationFilters.userId) {
        showToast("Please log in as a customer before adding items to your cart.");
        return;
    }

    const orderDate = new Date().toISOString();

    const orderPayload = {
        userId: paginationFilters.userId,
        productId: productId,
        statusId: 1,
        lastUpdatedByUserId: paginationFilters.userId,
        lastUpdatedOn: orderDate,
        addedOn: orderDate,
        orderedFor: currentUser,
        deliveryAddress: "" // Will be updated during checkout
    };

    try {
        const response = await fetch(`${API_BASE_URL}/orders/add`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(orderPayload)
        });

        if (response.ok) {
            const savedOrder = await response.json();
            // Store the DB Order ID in the local cart object for later removal
            cart.push({ ...product, dbOrderId: savedOrder.order.id });
            document.getElementById('cart-count').innerText = cart.length;
            showToast(`Added ${product.name} to cart and saved to DB`);
        } else {
            const errorBody = await response.json().catch(() => null);
            console.error("Failed to save cart item:", response.status, errorBody);
            showToast(errorBody?.message || `Error saving to cart (HTTP ${response.status}).`);
        }
    } catch (error) {
        console.error("Failed to sync cart to DB:", error);
        showToast("Error saving to cart. Check that the API is reachable.");
    }
}

function renderCart() {
    const container = document.getElementById('cart-items-container');
    if (cart.length === 0) {
        container.innerHTML = '<p style="color:var(--text-muted); text-align:center; padding: 20px;">Your cart is empty.</p>';
        document.getElementById('cart-total').innerText = '0.00';
        return;
    }

    let total = 0;
    container.innerHTML = cart.map((item, index) => {
        total += item.price;
        return `
            <div class="cart-item">
                <div style="display:flex; align-items:center; gap: 10px;">
                    <span style="font-size:24px;">${item.icon}</span>
                    <strong>${item.name}</strong>
                </div>
                <div>
                    <span style="font-weight:600; color:var(--primary);">$${item.price.toFixed(2)}</span>
                    <button class="btn-outline" style="margin-left: 15px;" onclick="removeFromCart(${index})">Remove</button>
                </div>
            </div>
        `;
    }).join('');
    
    document.getElementById('cart-total').innerText = total.toFixed(2);
}

async function removeFromCart(index) {
    const itemToRemove = cart[index];
    
    try {
        // Trigger API to delete the record from the DB
        const response = await fetch(`${API_BASE_URL}/orders/${itemToRemove.dbOrderId}`, {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ROLE_${currentRole}` }
        });

        if (response.ok) {
            cart.splice(index, 1);
            document.getElementById('cart-count').innerText = cart.length;
            renderCart();
            showToast("Item removed from cart and database.");
        } else {
            showToast("Failed to remove item from database.");
        }
    } catch (error) {
        console.error("Delete error:", error);
        showToast("Network error while removing item.");
    }
}

async function placeOrder(event) {
    event.preventDefault();

    if (cart.length === 0) {
        showToast("Your cart is empty!");
        return;
    }

    const name = document.getElementById('name').value;
    const address = document.getElementById('address').value;
    const paymentMethod = document.getElementById('payment-method').value;
    const submitBtn = document.getElementById('order-submit-btn');

    submitBtn.disabled = true;
    submitBtn.innerText = "Processing...";

    const orderDate = new Date().toISOString();
    let successCount = 0;

    // Iterate through each item in the cart to trigger the API individually
    for (const item of cart) {
        const orderPayload = {
			userId: paginationFilters.userId,
            productId: item.id,
            statusId: 10,
            lastUpdatedByUserId: paginationFilters.userId,
			paymentMode: (paymentMethod === "COD") ? 0 : 1,
            lastUpdatedOn: orderDate,
            orderedFor: name,
            deliveryAddress: `${address} (Payment: ${paymentMethod})`
        };

        try {
            const response = await fetch(`${API_BASE_URL}/orders/${item.dbOrderId}/status`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(orderPayload)
            });

            if (response.ok) {
                successCount++;
            }
        } catch (error) {
            console.error("Order failed for product:", item.id, error);
        }
    }

    if (successCount === cart.length) {
        showToast(`Successfully placed ${successCount} orders via ${paymentMethod}!`);
        cart = []; // Clear cart
        document.getElementById('cart-count').innerText = '0';
        document.getElementById('shipping-form').reset();
        switchTab('orders'); // Redirect to order history
    } else {
        showToast(`Order partially failed. ${successCount}/${cart.length} items placed.`);
    }

    submitBtn.disabled = false;
    submitBtn.innerText = "Confirm & Place Order";
}

function applyRoleUI() {
    const tabHome = document.getElementById('tab-home');
    const tabCart = document.getElementById('tab-cart');
    const adminFilters = document.getElementById('admin-filters'); // New filter UI

    if (currentRole === 'admin' || currentRole === 'backend') {
        tabHome.style.display = 'none';
        tabCart.style.display = 'none';
        if (adminFilters) adminFilters.style.display = 'flex'; // Show filters
    } else {
        tabHome.style.display = 'block';
        tabCart.style.display = 'flex';
        if (adminFilters) adminFilters.style.display = 'none'; // Hide filters for clients
    }

    if (document.getElementById('view-orders').classList.contains('active')) {
        renderOrders();
    }
}

function applyAdminFilters() {
    const userId = document.getElementById('filter-user-id').value;
    const statusId = document.getElementById('filter-status-id').value;

    paginationFilters.userId = userId ? parseInt(userId) : null;
    paginationFilters.statusId = statusId ? parseInt(statusId) : null;

    currentPage = 1;
    renderOrders(1);
}

function clearAdminFilters() {
    document.getElementById('filter-user-id').value = '';
    document.getElementById('filter-status-id').value = '';

    paginationFilters.userId = null;
    paginationFilters.statusId = null;

    currentPage = 1;
    renderOrders(1);
}

async function loadClientCart(userId) {
    try {
        // Fetch user's items
        const response = await fetch(`${API_BASE_URL}/orders/user/${userId}?pageNumber=1&pageSize=100`, {
            headers: { 'Authorization': `Bearer ROLE_${currentRole}` }
        });
        
        if (response.ok) {
            const result = await response.json();
            const allUserItems = result.data || result;
            
            // 1. Filter out only Status 1 items for the Cart
            const cartRecords = allUserItems.filter(order => order.statusId === 1);
            
            // 2. Map the DB records to visual product data
            cart = cartRecords.map(record => {
                const productInfo = products.find(p => p.id === record.productId) || {};
                return {
                    id: productInfo.id || record.productId,
                    name: productInfo.name || `Product #${record.productId}`,
                    price: productInfo.price || 0,
                    icon: productInfo.icon || '📦',
                    dbOrderId: record.orderId // Keep this so we can delete it from DB if removed
                };
            });
            
            // Update UI badge
            document.getElementById('cart-count').innerText = cart.length;
        }
    } catch (error) {
        console.error("Failed to load cart from DB:", error);
    }
}

function formatDateTime(dateString) {
    if (!dateString) return null;
    return new Date(dateString).toLocaleString(undefined, { 
        year: 'numeric', month: 'short', day: 'numeric', 
        hour: '2-digit', minute:'2-digit' 
    });
}

function buildTimelineStep(title, dateStr, statusClass) {
    const formattedDate = dateStr ? formatDateTime(dateStr) : '<span style="color:#aaa; font-style:italic;">Pending</span>';
    return `
        <div class="timeline-step ${statusClass}">
            <div class="step-title">${title}</div>
            <div class="step-date">${formattedDate}</div>
        </div>
    `;
}

function closeTrackModal() {
    document.getElementById('trackOrderModal').classList.remove('active');
}

function trackOrder(orderId) {
    // 1. Find the specific order in your existing local array
    const order = displayOrders.find(o => o.orderId == orderId); 
    if (!order) {
        alert("Order details not found.");
        return;
    }

    const modal = document.getElementById('trackOrderModal');
    const body = document.getElementById('trackOrderBody');

    // 2. Format top-level metadata
    const lastUpdated = formatDateTime(order.lastUpdatedOn) || 'N/A';
    const paymentModeText = order.paymentMode === 1 ? 'Online Payment' : 'Cash on Delivery';
    const deliveryAddress = order.deliveryAddress ? order.deliveryAddress.replace(/ \(Payment: .*\)/, '') : 'N/A';

    // 3. Start building the modal content (Meta Box)
    let contentHtml = `
        <div class="track-meta-box">
            <p><strong>Last Updated:</strong> ${lastUpdated}</p>
            <p><strong>Deliver To:</strong> ${order.orderedFor || 'N/A'}</p>
            <p><strong>Address:</strong> ${deliveryAddress}</p>
            <p><strong>Payment Mode:</strong> ${paymentModeText}</p>
        </div>
    `;

    // 4. Check if the order is pending system approval (Status 10)
    if (order.statusId === 10) {
        // Render an approval banner instead of the timeline
        contentHtml += `
            <div style="padding: 20px; text-align: center; background: #fff8e1; color: #b78103; border: 1px solid #ffe082; border-radius: 8px; font-weight: 600; margin-top: 15px;">
                <span style="font-size: 18px; display: block; margin-bottom: 5px;">⏳</span>
                Order pending for system approval.
            </div>
        `;
    } else {
        // 5. Build the sequential timeline for all other statuses
        let timelineHtml = '<div class="tracking-timeline">';

        // Step A: Order Placed
        timelineHtml += buildTimelineStep('Order Placed', order.orderedOn, 'completed');

        // Step B: Packaged
        const hasPackagedDate = !!order.packagedOn;
        timelineHtml += buildTimelineStep('Packaged', order.packagedOn, hasPackagedDate ? 'completed' : 'pending');

        // Step C & D: Branching logic based on Delivery vs Cancellation
        if (order.statusId >= 6 && order.statusId <= 8) {
            
            // Cancellation branch
            timelineHtml += buildTimelineStep('Cancellation Requested', order.cancelledOn, 'cancelled');
            
            // Only show CancellationPaidOn if it exists
            if (order.cancellationPaidOn) {
                timelineHtml += buildTimelineStep('Refund Processed', order.cancellationPaidOn, 'completed');
            }

        } else {
            // Delivery branch
            const isDelivered = !!order.deliveredOn;
            timelineHtml += buildTimelineStep('Delivered', order.deliveredOn, isDelivered ? 'completed' : 'pending');
        }

        timelineHtml += '</div>';
        
        // Append the timeline to the content
        contentHtml += timelineHtml;
    }

    // 6. Inject HTML into the modal body and display it
    body.innerHTML = contentHtml;
    modal.classList.add('active');
}

window.addEventListener('click', function(event) {
    const modal = document.getElementById('trackOrderModal');
    if (event.target === modal) {
        closeTrackModal();
    }
});

async function updateOrderStatus(orderId, newStatusId) {
    // Disable the button to prevent double-clicks during the API call
    const buttonElement = event.target;
    const originalText = buttonElement.innerText;
    buttonElement.disabled = true;
    buttonElement.innerText = "Updating...";

    try {
        // Construct the payload based on what your API expects for a status update.
        // Adjust these fields if your C# backend requires more data in the DTO.
        const updatePayload = {
            statusId: parseInt(newStatusId),
            lastUpdatedOn: new Date().toISOString(),
			lastUpdatedByUserId: currentUserId,
        };

        const response = await fetch(`${API_BASE_URL}/orders/${orderId}/status`, {
            method: 'PUT',
            headers: { 
                'Content-Type': 'application/json',
                'Authorization': `Bearer ROLE_${currentRole}` 
            },
            body: JSON.stringify(updatePayload)
        });

        if (response.ok) {
            showToast("Order status updated successfully.");
            // Refresh the current page of orders to reflect the new state
            renderOrders(currentPage); 
        } else {
            showToast("Failed to update order status. Please try again.");
            buttonElement.disabled = false;
            buttonElement.innerText = originalText;
        }
    } catch (error) {
        console.error("Failed to update order status:", error);
        showToast("Network error while updating status.");
        buttonElement.disabled = false;
        buttonElement.innerText = originalText;
    }
}

async function cancelOrder(orderId) {
    // 1. Find the specific order in the local array to check its payment mode
    // We check both id and orderId just to be safe depending on your API structure
    const order = displayOrders.find(o => o.orderId == orderId);
    
    if (!order) {
        showToast("Order details not found.");
        return;
    }

    // 2. Confirm the cancellation with the user
    if (!confirm("Are you sure you want to cancel this order?")) {
        return;
    }

    // 3. Determine new status based on payment mode
    let newStatusId;
    if (order.paymentMode == 1 || order.statusId == 4) {
        newStatusId = 6;
    } else {
        newStatusId = 8; 
    }

    try {
        const updatePayload = {
            statusId: newStatusId,
            lastUpdatedOn: new Date().toISOString(),
			lastUpdatedByUserId: currentUserId,
        };

        // 4. Trigger the update API
        const response = await fetch(`${API_BASE_URL}/orders/${order.id || order.orderId}/status`, {
            method: 'PUT',
            headers: { 
                'Content-Type': 'application/json',
                'Authorization': `Bearer ROLE_${currentRole}` 
            },
            body: JSON.stringify(updatePayload)
        });

        if (response.ok) {
            showToast("Order cancelled successfully.");
            // Refresh the order list to reflect the new state
            renderOrders(currentPage); 
        } else {
            showToast("Failed to cancel the order. Please try again.");
        }
    } catch (error) {
        console.error("Failed to cancel order:", error);
        showToast("Network error while cancelling the order.");
    }
}