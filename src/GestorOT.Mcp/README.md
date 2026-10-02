# GestorOT.Mcp

Servidor MCP (stdio) que expone GestorOT a Claude. Es un **cliente HTTP de la API**: no toca la base,
así que validaciones, permisos y filtro por tenant los sigue resolviendo la API. Si la API se muda,
solo cambia `GestorOt:BaseUrl`.

## Tools

**Lectura**

| Tool | Endpoint |
|---|---|
| `get_current_user` | `GET api/auth/me` |
| `get_active_campaigns` / `list_campaigns` | `GET api/campaigns/active` / `selector` |
| `list_fields` | `GET api/fields` |
| `list_lots` | `GET api/lots` o `GET api/campaigns/{id}/lots` (sin geometría) |
| `search_labors` | `GET api/labors` o `GET api/labors/by-lot/{id}` |
| `list_work_orders` / `get_work_order` | `GET api/workorders/paged` / `{id}` |
| `list_labor_types`, `list_activities`, `list_contacts` | `GET api/catalogs/*` |
| `list_supplies` | `GET api/inventory` |
| `list_work_order_statuses` | `GET api/workorderstatuses` |

**Escritura**

| Tool | Endpoint |
|---|---|
| `create_work_order` | `POST api/workorders` (genera `OT_XXXXX` igual que la UI) |
| `create_labor` | `POST api/labors` (resuelve el `campaignLotId` a partir de campaña + lote) |
| `assign_labors_to_work_order` | `GET` + `PUT api/labors/{id}` cambiando solo `workOrderId` |

Las fechas se reciben como días (`yyyy-MM-dd`) y se mandan como medianoche de `GestorOt:TimeZone`
(default `America/Argentina/Buenos_Aires`), igual que la UI.

## Configuración

Sección `GestorOt` (appsettings, user-secrets o variables de entorno `GestorOt__*`):

- `BaseUrl`: raíz de la API (default `http://localhost:5159`).
- `Token`: JWT ya emitido, **o** `Email` + `Password` para que haga login solo y renueve ante un 401.
- `TenantId`: solo si el usuario es SuperAdmin (se manda como `X-Tenant-ID`).

Las credenciales van en user-secrets (se leen sin importar desde dónde se lance el proceso):

```bash
dotnet user-secrets --project src/GestorOT.Mcp set GestorOt:Email "usuario@empresa.com"
dotnet user-secrets --project src/GestorOT.Mcp set GestorOt:Password "..."
dotnet user-secrets --project src/GestorOT.Mcp set GestorOt:BaseUrl "https://<api>"
```

## Conectarlo

Compilar una vez con `dotnet build src/GestorOT.Mcp` y registrar el ejecutable.

**Claude Code** (CLI o pestaña Code del desktop):

```bash
claude mcp add gestorot --scope user -- "C:\Users\HWLScuffi\workspace\Gestor-OT\src\GestorOT.Mcp\bin\Debug\net10.0\GestorOT.Mcp.exe"
```

**Claude Desktop** (chat): en `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "gestorot": {
      "command": "C:\\Users\\HWLScuffi\\workspace\\Gestor-OT\\src\\GestorOT.Mcp\\bin\\Debug\\net10.0\\GestorOT.Mcp.exe"
    }
  }
}
```

## Modo remoto (claude.ai web y mobile)

`GestorOT.Mcp --http` levanta el mismo servidor por HTTP, con un OAuth mínimo delante:

1. claude.ai llama a `/mcp` sin token, recibe 401 y lee `/.well-known/oauth-protected-resource`
   y `/.well-known/oauth-authorization-server`.
2. Se registra (`/register`, sin estado: solo se aceptan los redirect de `OAuth:AllowedRedirectUris`).
3. Abre `/authorize`: pantalla de login de GestorOT. Usuario y contraseña van a `api/auth/login`.
4. `/token` (con PKCE) le entrega **el mismo JWT de GestorOT**. Cada tool reenvía ese JWT a la API,
   así que cada usuario ve y carga solo lo que sus permisos le dejan.

No hay refresh token: cuando el JWT vence (7 días) claude.ai pide login de nuevo. Los códigos de
autorización viven en memoria; un reinicio en medio de un login solo obliga a reintentar.

### Deploy

- `Dockerfile.mcp` + servicio `gestor-ot-mcp` en `docker-compose.yml`; el CI lo levanta junto con la app.
- En el `.env` del servidor: `MCP_PUBLIC_URL=https://<subdominio>` (sin barra final). Sin eso el
  contenedor no arranca, a propósito.
- Reverse proxy: un **subdominio propio** (los `/.well-known` tienen que estar en la raíz) apuntando a
  `gestorot-mcp:8080` por la red `web_traffic` (el contenedor no publica puerto en el host). Sin buffering, porque las
  respuestas pueden ser streams SSE. Con nginx:

```nginx
location / {
    proxy_pass http://gestorot-mcp:8080;
    proxy_http_version 1.1;
    proxy_buffering off;
    proxy_set_header Host $host;
}
```

- En claude.ai: Configuración → Conectores → Agregar conector personalizado → URL `https://<subdominio>/mcp`.

Probarlo local: `.claude/launch.json` tiene la config `gestorot-mcp` (puerto 8090).
