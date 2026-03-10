# Postman collection

## Import

1. Open Postman.
2. **Import** → **File** → choose `Redis Playground - Items CRUD.postman_collection.json`.
3. The collection appears in your sidebar.

## Variables

- **baseUrl**: `https://localhost:7164` (change if your app runs on another port).
- **itemId**: Leave empty at first. After **Create item**, copy the `id` from the response and set it in the collection variables (or in an environment) so **Get by ID**, **Update**, and **Delete** use it.

To edit: click the collection → **Variables** tab.

## Order to try

1. **Get all items** (empty at first).
2. **Create item** → copy the returned `id`.
3. Set **itemId** to that id.
4. **Get item by ID** (cache hit on second call).
5. **Update item** then **Get item by ID** again.
6. **Delete item**.

If your API uses HTTP only, set `baseUrl` to `http://localhost:5109`.
