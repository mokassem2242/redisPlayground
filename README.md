# Redis Playground – CRUD with cache-aside

Simple CRUD API to learn **Redis caching** with the **cache-aside** pattern in .NET 9. **SQL Server** is the durable store; **Redis** is the cache.

## Prerequisites

- **Redis** – cache
- **SQL Server** (or **LocalDB**) – durable database

## Run Redis

**Docker (recommended):**
```bash
docker run -d -p 6379:6379 --name redis redis:latest
```

If the `redis` container already exists: `docker start redis`.

## SQL Server / connection string

Default connection string (LocalDB):

```
Server=(localdb)\mssqllocaldb;Database=RedisPlayground;Trusted_Connection=True;TrustServerCertificate=True;
```

- **LocalDB** is installed with Visual Studio. If you use full SQL Server, set the connection string in `appsettings.json` or `appsettings.Development.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.;Database=RedisPlayground;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

On first run (Development), the app creates the database and `Items` table automatically.

## Run the app

**Stop any running instance** (so the build can copy files), then:

```bash
dotnet run
```

API base: `https://localhost:7164` (or the port in `Properties/launchSettings.json`). Swagger: `https://localhost:7164/swagger`.

## API – Items CRUD

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/items` | List all items (from Redis or SQL Server) |
| `GET` | `/api/items/{id}` | Get one item (from Redis or SQL Server) |
| `POST` | `/api/items` | Create in SQL Server, invalidate list cache, cache new item |
| `PUT` | `/api/items/{id}` | Update in SQL Server, invalidate list, update item cache |
| `DELETE` | `/api/items/{id}` | Delete from SQL Server, remove from cache |

Use `redisPlayground.http` or the Postman collection in `postman/` to call these endpoints.

## What you’re learning

- **Cache-aside:** Read: check Redis first; on miss, load from SQL Server and store in Redis. Write: update SQL Server, then invalidate or update the related cache keys.
- **Durable store:** SQL Server holds the data; Redis only caches it.
- **Cache keys:**  
  - List: `RedisPlayground:items:all`  
  - Single: `RedisPlayground:item:{id}`
- **TTL:** Cached entries expire after 5 minutes.

To see cache keys: `redis-cli KEYS RedisPlayground:*`.
