const state = {
  materials: [],
  products: [],
  orders: [],
  lines: [],
  lowStockOnly: false,
  selectedOrderId: null
};

document.addEventListener("DOMContentLoaded", () => {
  wireEvents();
  loadDashboard();
});

function wireEvents() {
  document.getElementById("toggleLowStock")?.addEventListener("click", async () => {
    state.lowStockOnly = !state.lowStockOnly;
    document.getElementById("toggleLowStock").textContent = state.lowStockOnly
      ? "Показать все"
      : "Только низкий запас";
    await loadMaterials();
  });

  document.getElementById("materialForm")?.addEventListener("submit", createMaterial);
  document.getElementById("productForm")?.addEventListener("submit", createProduct);
  document.getElementById("orderForm")?.addEventListener("submit", createOrder);
  document.getElementById("addRecipeRow")?.addEventListener("click", () => addRecipeRow());
  document.getElementById("productSearch")?.addEventListener("input", renderProducts);
  document.getElementById("categoryFilter")?.addEventListener("input", renderProducts);
  document.getElementById("orderStatusFilter")?.addEventListener("change", loadOrders);
  document.getElementById("orderDateFilter")?.addEventListener("change", loadOrders);

  ["orderProductId", "orderQuantity", "orderLineId"].forEach((id) => {
    document.getElementById(id)?.addEventListener("change", previewCalculation);
    document.getElementById(id)?.addEventListener("input", previewCalculation);
  });

  addRecipeRow();
}

async function loadDashboard() {
  await Promise.all([loadMaterials(), loadProducts(), loadLines(), loadOrders()]);
  updateMetrics();
}

async function api(url, options = {}) {
  const response = await fetch(url, {
    headers: { "Content-Type": "application/json" },
    ...options
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `HTTP ${response.status}`);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

async function loadMaterials() {
  state.materials = await api(`/api/materials${state.lowStockOnly ? "?low_stock=true" : ""}`);
  renderMaterials();
  refreshRecipeMaterialOptions();
  updateMetrics();
}

async function loadProducts() {
  state.products = await api("/api/products");
  fillProductSelect();
  renderProducts();
}

async function loadOrders() {
  const params = new URLSearchParams();
  const status = document.getElementById("orderStatusFilter")?.value;
  const date = document.getElementById("orderDateFilter")?.value;

  if (status) {
    params.set("status", status);
  }
  if (date) {
    params.set("date", date);
  }

  const suffix = params.toString() ? `?${params.toString()}` : "";
  state.orders = await api(`/api/orders${suffix}`);
  renderOrders();
  await refreshOrderDetails();
  updateMetrics();
}

async function loadLines() {
  state.lines = await api("/api/lines");
  fillLineSelect();
  await renderLines();
  updateMetrics();
}

function updateMetrics() {
  const lowStockCount = state.materials.filter((m) => Number(m.quantity) <= Number(m.minimalStock)).length;
  const activeOrdersCount = state.orders.filter((o) => ["Pending", "InProgress"].includes(o.status)).length;
  const availableLinesCount = state.lines.filter((l) => l.status === "Active" && !l.currentWorkOrderId).length;

  setText("materialsCount", state.materials.length);
  setText("lowStockCount", lowStockCount);
  setText("activeOrdersCount", activeOrdersCount);
  setText("availableLinesCount", availableLinesCount);
}

function renderMaterials() {
  const tbody = document.getElementById("materialsTable");
  tbody.innerHTML = "";

  if (!state.materials.length) {
    tbody.innerHTML = `<tr><td colspan="5" class="empty-state">Материалы не найдены.</td></tr>`;
    return;
  }

  tbody.innerHTML = state.materials.map((material) => {
    const low = Number(material.quantity) <= Number(material.minimalStock);
    return `
      <tr>
        <td>${escapeHtml(material.name)}</td>
        <td><span class="stock-pill ${low ? "low" : "ok"}">${material.quantity}</span></td>
        <td>${escapeHtml(material.unitOfMeasure)}</td>
        <td>${material.minimalStock}</td>
        <td>
          <button class="btn btn-sm btn-outline-light" type="button" onclick="replenishMaterial(${material.id}, ${Number(material.quantity)})">Пополнить</button>
        </td>
      </tr>
    `;
  }).join("");
}

function renderProducts() {
  const search = document.getElementById("productSearch")?.value?.toLowerCase() ?? "";
  const category = document.getElementById("categoryFilter")?.value?.toLowerCase() ?? "";
  const tbody = document.getElementById("productsTable");

  const filtered = state.products.filter((product) => {
    const nameMatch = product.name.toLowerCase().includes(search);
    const categoryMatch = product.category.toLowerCase().includes(category);
    return nameMatch && categoryMatch;
  });

  if (!filtered.length) {
    tbody.innerHTML = `<tr><td colspan="4" class="empty-state">Под фильтр ничего не попало.</td></tr>`;
    return;
  }

  tbody.innerHTML = filtered.map((product) => `
    <tr>
      <td>${escapeHtml(product.name)}</td>
      <td>${escapeHtml(product.category)}</td>
      <td>${product.productionTimePerUnit}</td>
      <td><button class="btn btn-sm btn-outline-light" type="button" onclick="showProductMaterials(${product.id})">Состав</button></td>
    </tr>
  `).join("");
}

function renderOrders() {
  const tbody = document.getElementById("ordersTable");

  if (!state.orders.length) {
    tbody.innerHTML = `<tr><td colspan="7" class="empty-state">Заказы пока не созданы.</td></tr>`;
    return;
  }

  tbody.innerHTML = state.orders.map((order) => {
    const availableLineOptions = state.lines
      .filter((line) => line.status === "Active" && (!line.currentWorkOrderId || line.id === order.productionLineId))
      .map((line) => `<option value="${line.id}" ${line.id === order.productionLineId ? "selected" : ""}>${escapeHtml(line.name)}</option>`)
      .join("");

    return `
      <tr>
        <td><button class="btn btn-link p-0 text-info" type="button" onclick="selectOrder(${order.id})">#${order.id}</button></td>
        <td>${escapeHtml(order.productName)}</td>
        <td>${order.quantity}</td>
        <td><span class="status-pill ${order.status}">${order.status}</span></td>
        <td>${formatDate(order.estimatedEndDate)}</td>
        <td>
          <div class="progress-track">
            <div class="progress-bar-ui" style="width:${order.progressPercent}%"></div>
          </div>
          <small class="muted">${order.progressPercent}%</small>
        </td>
        <td>
          <div class="order-actions">
            <select id="order-line-${order.id}" class="form-select form-select-sm">
              <option value="">Линия</option>
              ${availableLineOptions}
            </select>
            ${order.status !== "InProgress" && order.status !== "Completed" && order.status !== "Cancelled"
              ? `<button class="btn btn-sm btn-outline-light" type="button" onclick="startOrder(${order.id})">Запустить</button>`
              : ""}
            ${order.status === "InProgress"
              ? `<button class="btn btn-sm btn-outline-light" type="button" onclick="completeOrder(${order.id})">Завершить</button>`
              : ""}
            ${order.status !== "Completed" && order.status !== "Cancelled"
              ? `<button class="btn btn-sm btn-outline-light" type="button" onclick="cancelOrder(${order.id})">Отменить</button>`
              : ""}
          </div>
        </td>
      </tr>
    `;
  }).join("");
}

async function renderLines() {
  const container = document.getElementById("linesGrid");

  if (!state.lines.length) {
    container.innerHTML = `<div class="empty-state">Линии не найдены.</div>`;
    return;
  }

  const scheduleData = await Promise.all(state.lines.map((line) => api(`/api/lines/${line.id}/schedule`)));

  container.innerHTML = scheduleData.map((lineSchedule) => {
    const lineState = state.lines.find((line) => line.id === lineSchedule.id);
    const currentOrder = state.orders.find((order) =>
      order.id === lineState?.currentWorkOrderId ||
      (order.productionLineId === lineSchedule.id && order.status === "InProgress")
    );
    const orderLabel = currentOrder ? `${escapeHtml(currentOrder.productName)} · ${currentOrder.progressPercent}%` : "Свободна";
    const scheduleItems = lineSchedule.orders.length
      ? lineSchedule.orders.map((order) => `
        <div class="schedule-item">
          <h4>#${order.id} · ${escapeHtml(order.productName)}</h4>
          <div class="line-meta">
            <span>${order.quantity} шт.</span>
            <span>${order.status}</span>
            <span>До ${formatDate(order.estimatedEndDate)}</span>
          </div>
          <div class="schedule-row">
            <input id="reschedule-${order.id}" class="form-control" type="datetime-local" value="${toDateTimeLocal(order.startDate)}" />
            <button class="btn btn-sm btn-outline-light" type="button" onclick="rescheduleOrder(${order.id}, ${lineSchedule.id})">Перенести</button>
          </div>
        </div>
      `).join("")
      : `<div class="empty-state">На линии пока нет назначенных заказов.</div>`;

    return `
      <article class="line-card">
        <div class="line-head">
          <div>
            <h3 class="line-title">${escapeHtml(lineSchedule.name)}</h3>
            <p class="panel-tag">${lineSchedule.status}</p>
          </div>
          <span class="status-pill ${lineSchedule.status === "Active" ? "Completed" : "Cancelled"}">${lineSchedule.status}</span>
        </div>
        <div class="line-meta">
          <span>Текущий заказ: ${orderLabel}</span>
          <span>Эффективность: ${lineSchedule.efficiencyFactor}</span>
        </div>
        <div class="line-actions">
          <select id="line-status-${lineSchedule.id}" class="form-select form-select-sm">
            <option value="Active" ${lineSchedule.status === "Active" ? "selected" : ""}>Active</option>
            <option value="Stopped" ${lineSchedule.status === "Stopped" ? "selected" : ""}>Stopped</option>
          </select>
          <input id="line-efficiency-${lineSchedule.id}" class="form-control form-control-sm" type="number" min="0.5" max="2" step="0.1" value="${lineSchedule.efficiencyFactor}" />
          <button class="btn btn-sm btn-outline-light" type="button" onclick="saveLine(${lineSchedule.id})">Сохранить линию</button>
        </div>
        <div class="schedule-list">${scheduleItems}</div>
      </article>
    `;
  }).join("");
}

async function createMaterial(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const formData = new FormData(form);

  await api("/api/materials", {
    method: "POST",
    body: JSON.stringify({
      name: formData.get("name"),
      quantity: Number(formData.get("quantity")),
      unit: formData.get("unit"),
      minStock: Number(formData.get("minStock"))
    })
  });

  form.reset();
  await loadMaterials();
}

async function replenishMaterial(id, currentQuantity) {
  const value = prompt("Введите новое количество на складе", `${currentQuantity}`);
  if (value === null) {
    return;
  }

  await api(`/api/materials/${id}/stock`, {
    method: "PUT",
    body: JSON.stringify({ amount: Number(value) })
  });

  await loadMaterials();
}

async function createProduct(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const formData = new FormData(form);

  const product = await api("/api/products", {
    method: "POST",
    body: JSON.stringify({
      name: formData.get("name"),
      category: formData.get("category"),
      prodTime: Number(formData.get("prodTime")),
      minimalStock: Number(formData.get("minimalStock") || 0),
      description: formData.get("description"),
      specFirst: formData.get("specFirst")
    })
  });

  const materials = Array.from(document.querySelectorAll(".recipe-row")).map((row) => ({
    materialId: Number(row.querySelector("[data-field='materialId']").value),
    quantityNeeded: Number(row.querySelector("[data-field='quantityNeeded']").value)
  })).filter((item) => item.materialId && item.quantityNeeded > 0);

  if (materials.length) {
    await api(`/api/products/${product.id}/materials`, {
      method: "POST",
      body: JSON.stringify(materials)
    });
  }

  form.reset();
  document.getElementById("recipeRows").innerHTML = "";
  addRecipeRow();
  await Promise.all([loadProducts(), loadMaterials()]);
}

function addRecipeRow() {
  const container = document.getElementById("recipeRows");

  const row = document.createElement("div");
  row.className = "recipe-row";
  row.innerHTML = `
    <select class="form-select" data-field="materialId"></select>
    <input class="form-control" data-field="quantityNeeded" type="number" min="0" step="0.01" placeholder="Расход на 1 ед." />
    <button class="btn btn-outline-light" type="button">Удалить</button>
  `;

  row.querySelector("button").addEventListener("click", () => row.remove());
  container.appendChild(row);
  refreshRecipeMaterialOptions();
}

function refreshRecipeMaterialOptions() {
  const options = state.materials.length
    ? `<option value="">Материал</option>${state.materials
        .map((material) => `<option value="${material.id}">${escapeHtml(material.name)} (${escapeHtml(material.unitOfMeasure)})</option>`)
        .join("")}`
    : `<option value="">Сначала добавь материалы</option>`;

  document.querySelectorAll(".recipe-row [data-field='materialId']").forEach((select) => {
    const currentValue = select.value;
    select.innerHTML = options;
    if (currentValue) {
      select.value = currentValue;
    }
  });
}

function fillProductSelect() {
  const select = document.getElementById("orderProductId");
  select.innerHTML = `<option value="">Выбери продукт</option>${state.products
    .map((product) => `<option value="${product.id}">${escapeHtml(product.name)} · ${escapeHtml(product.category)}</option>`)
    .join("")}`;
}

function fillLineSelect() {
  const select = document.getElementById("orderLineId");
  const availableLines = state.lines.filter((line) => line.status === "Active" && !line.currentWorkOrderId);
  select.innerHTML = `<option value="">Без линии (Pending)</option>${availableLines
    .map((line) => `<option value="${line.id}">${escapeHtml(line.name)} · eff ${line.efficiencyFactor}</option>`)
    .join("")}`;
}

async function previewCalculation() {
  const productId = Number(document.getElementById("orderProductId").value);
  const quantity = Number(document.getElementById("orderQuantity").value);
  const lineIdValue = document.getElementById("orderLineId").value;

  if (!productId || !quantity) {
    setText("calculationPreview", "Расчёт времени появится после выбора продукта и количества.");
    return;
  }

  const payload = {
    productId,
    quantity,
    lineId: lineIdValue ? Number(lineIdValue) : null
  };

  const result = await api("/api/calculate/production", {
    method: "POST",
    body: JSON.stringify(payload)
  });

  setText("calculationPreview", `Расчёт: ${result.productionTimeMinutes} мин. при коэффициенте ${result.efficiencyFactor}.`);
}

async function createOrder(event) {
  event.preventDefault();
  const formData = new FormData(event.currentTarget);

  try {
    await api("/api/orders", {
      method: "POST",
      body: JSON.stringify({
        productId: Number(formData.get("productId")),
        quantity: Number(formData.get("quantity")),
        lineId: formData.get("lineId") ? Number(formData.get("lineId")) : null
      })
    });
  } catch (error) {
    alert(parseApiError(error));
    return;
  }

  event.currentTarget.reset();
  setText("calculationPreview", "Расчёт времени появится после выбора продукта и количества.");
  await Promise.all([loadOrders(), loadLines(), loadMaterials()]);
}

async function startOrder(id) {
  const lineId = Number(document.getElementById(`order-line-${id}`).value);
  if (!lineId) {
    alert("Выбери линию для запуска заказа.");
    return;
  }

  await api(`/api/orders/${id}/status`, {
    method: "PUT",
    body: JSON.stringify({ status: "InProgress", lineId })
  });

  await Promise.all([loadOrders(), loadLines()]);
}

async function completeOrder(id) {
  await api(`/api/orders/${id}/status`, {
    method: "PUT",
    body: JSON.stringify({ status: "Completed" })
  });

  await Promise.all([loadOrders(), loadLines()]);
}

async function cancelOrder(id) {
  await api(`/api/orders/${id}/status`, {
    method: "PUT",
    body: JSON.stringify({ status: "Cancelled" })
  });

  await Promise.all([loadOrders(), loadLines()]);
}

async function selectOrder(id) {
  state.selectedOrderId = id;
  await refreshOrderDetails();
}

async function refreshOrderDetails() {
  const host = document.getElementById("orderDetails");
  if (!state.selectedOrderId) {
    host.innerHTML = `<div class="empty-state">Детали заказа пока не выбраны.</div>`;
    return;
  }

  const details = await api(`/api/orders/${state.selectedOrderId}/details`);
  host.innerHTML = `
    <div class="details-section">
      <strong>Заказ #${details.id}</strong>
      <div class="line-meta">
        <span>${escapeHtml(details.product.name)}</span>
        <span>${details.quantity} шт.</span>
        <span>${details.status}</span>
        <span>${details.progressPercent}%</span>
      </div>
    </div>
    <div class="details-section">
      <strong>Линия</strong>
      <div class="line-meta">
        <span>${details.productionLine ? escapeHtml(details.productionLine.name) : "Не назначена"}</span>
        <span>${details.productionLine ? details.productionLine.efficiencyFactor : "-"}</span>
      </div>
    </div>
    <div class="details-section">
      <strong>Материалы</strong>
      <div class="schedule-list">
        ${details.materials.map((material) => `
          <div class="line-meta">
            <span>${escapeHtml(material.materialName)}</span>
            <span>${material.requiredQuantity} ${escapeHtml(material.unitOfMeasure)}</span>
          </div>
        `).join("") || '<div class="empty-state">Для продукта не задана спецификация материалов.</div>'}
      </div>
    </div>
  `;
}

async function showProductMaterials(productId) {
  const materials = await api(`/api/products/${productId}/materials`);
  const text = materials.length
    ? materials.map((item) => `${item.materialName}: ${item.quantityNeeded}`).join("\n")
    : "Материалы не назначены.";
  alert(text);
}

async function saveLine(id) {
  const status = document.getElementById(`line-status-${id}`).value;
  const efficiencyFactor = Number(document.getElementById(`line-efficiency-${id}`).value);

  await api(`/api/lines/${id}`, {
    method: "PUT",
    body: JSON.stringify({ status, efficiencyFactor })
  });

  await Promise.all([loadLines(), loadOrders()]);
}

async function rescheduleOrder(orderId, lineId) {
  const value = document.getElementById(`reschedule-${orderId}`).value;
  if (!value) {
    return;
  }

  await api(`/api/orders/${orderId}/schedule`, {
    method: "PUT",
    body: JSON.stringify({ startDate: new Date(value).toISOString(), lineId })
  });

  await Promise.all([loadOrders(), loadLines()]);
}

function toDateTimeLocal(value) {
  const date = new Date(value);
  const timezoneOffset = date.getTimezoneOffset() * 60000;
  return new Date(date - timezoneOffset).toISOString().slice(0, 16);
}

function formatDate(value) {
  return new Date(value).toLocaleString("ru-RU");
}

function setText(id, value) {
  const element = document.getElementById(id);
  if (element) {
    element.textContent = value;
  }
}

function escapeHtml(value) {
  return `${value ?? ""}`
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function parseApiError(error) {
  try {
    const payload = JSON.parse(error.message);
    if (payload.message && payload.shortages) {
      return `${payload.message}\n${payload.shortages
        .map((item) => `${item.materialName}: нужно ${item.requiredQuantity}, доступно ${item.availableQuantity} ${item.unitOfMeasure}`)
        .join("\n")}`;
    }
  } catch {
    return error.message;
  }

  return error.message;
}
