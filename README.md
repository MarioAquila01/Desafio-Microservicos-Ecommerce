# Desafio Técnico — Microserviços (DIO / Avanade)

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF%20Core-SQLite-0D597F?logo=sqlite&logoColor=white)](https://learn.microsoft.com/ef/core/)
[![YARP](https://img.shields.io/badge/API%20Gateway-YARP-0C6EFC)](https://microsoft.github.io/reverse-proxy/)
[![RabbitMQ](https://img.shields.io/badge/Messaging-RabbitMQ-FF6600?logo=rabbitmq&logoColor=white)](https://www.rabbitmq.com/)
[![OpenAPI/Swagger](https://img.shields.io/badge/OpenAPI-Swagger-85EA2D?logo=swagger&logoColor=black)](https://swagger.io/)
[![License: MIT](https://img.shields.io/badge/License-MIT-black.svg)](LICENSE)
[![CI](https://github.com/MarioAquila01/Desafio-Microservicos-Ecommerce/actions/workflows/ci.yml/badge.svg)](https://github.com/MarioAquila01/Desafio-Microservicos-Ecommerce/actions)



> **Status:** Em processo de desenvolvimento — *Desafio do Bootcamp DIO / Desafio técnico Avanade*  
> **Repositório:** https://github.com/MarioAquila01/Desafio-Microservicos-Ecommerce

Arquitetura de e-commerce em **.NET 8** com dois microsserviços (**Estoque** e **Vendas**), **API Gateway (YARP)**, **JWT**, **RabbitMQ** (eventos assíncronos) e **EF Core + SQLite**.

Fluxo principal: **criar produto → criar pedido → validar estoque → publicar evento → consumir evento e decrementar estoque**.


---

## Sumário
- [Arquitetura](#arquitetura)
- [Stack](#-stack)
- [Pré-requisitos](#-pré-requisitos)
- [Configuração](#-configuração)
- [Subir localmente](#-subir-localmente)
- [Autenticação (JWT)](#-autenticação-jwt)
- [Fluxo de Teste (E2E)](#-fluxo-de-teste-end-to-end)
- [Endpoints](#-endpoints-principais)
- [Banco & Migrações](#-banco--migrações)
- [Troubleshooting](#-troubleshooting)
- [Roadmap curto](#-roadmap-curto)
- [CI / Automação](#-ci--automação)
- [Contribuição](#-contribuição)
- [Licença](#-licença)

---

## Arquitetura

```

Client ──▶ Gateway (YARP + JWT)
/inventory/*  ──▶ Inventory.Api (EF Core + SQLite)
▲
│ (evento "sales.order_confirmed" via RabbitMQ)
└──── Sales.Api  ──▶ RabbitMQ
/sales/*

````

- **Gateway**: ponto único de entrada (roteamento com YARP + autenticação JWT).
- **Inventory.Api**: CRUD de produtos, consulta de disponibilidade, consumo de evento para reduzir estoque.
- **Sales.Api**: criação/consulta de pedidos, validação de estoque via Gateway, publicação do evento.
- **RabbitMQ**: mensageria para integração assíncrona entre serviços.

---

## 🧰 Stack
- .NET 8, C#, ASP.NET Core (Web API)
- YARP Reverse Proxy (API Gateway)
- JWT (Microsoft.IdentityModel.Tokens)
- Entity Framework Core + SQLite
- RabbitMQ.Client
- Swashbuckle (Swagger/OpenAPI)

---

## ✅ Pré-requisitos
- **.NET SDK 8**
- **Docker** (para o RabbitMQ)
- PowerShell (Windows) ou bash (Linux/macOS)

---

## ⚙️ Configuração
Variáveis via `appsettings.json` ou **ambiente**:

- `Jwt:Key` (mín. **32** chars em dev)
- `RabbitMQ:Host`, `RabbitMQ:Port`, `RabbitMQ:User`, `RabbitMQ:Pass`
- `Gateway/BaseAddress` (HttpClient nomeado `"inventory"` no Sales)

**PowerShell**
```powershell
$env:Jwt__Key = "dev-secret-CHANGE-ME-32chars-min-123456"

# Sales → RabbitMQ
$env:RabbitMQ__Host = "127.0.0.1"
$env:RabbitMQ__Port = "5673"
$env:RabbitMQ__User = "guest"
$env:RabbitMQ__Pass = "guest"
````

**bash**

```bash
export Jwt__Key="dev-secret-CHANGE-ME-32chars-min-123456"
export RabbitMQ__Host=127.0.0.1
export RabbitMQ__Port=5673
export RabbitMQ__User=guest
export RabbitMQ__Pass=guest
```

---

## 🚀 Subir localmente

### 1) RabbitMQ (Docker)

```powershell
docker run -d --name rabbit_5673 `
  -p 5673:5672 -p 15673:15672 rabbitmq:3-management
```

* UI: [http://localhost:15673](http://localhost:15673) (user/pass: `guest`/`guest`)
* Checar portas: `Test-NetConnection 127.0.0.1 -Port 5673`

> Opcional: criar usuário próprio

```powershell
docker exec -it rabbit_5673 rabbitmqctl add_user mario 123456
docker exec -it rabbit_5673 rabbitmqctl set_user_tags mario administrator
docker exec -it rabbit_5673 rabbitmqctl set_permissions -p / mario ".*" ".*" ".*"
```

### 2) Serviços (cada um em **um terminal**)

**Gateway**

```powershell
dotnet run --project gateway --urls http://localhost:8080
```

**Inventory**

```powershell
dotnet run --project services/Inventory.Api --urls http://localhost:5001
```

**Sales**

```powershell
dotnet run --project services/Sales.Api --urls http://localhost:5002
```

### 3) Swagger

* Inventory: [http://localhost:5001/swagger](http://localhost:5001/swagger)
* Sales: [http://localhost:5002/swagger](http://localhost:5002/swagger)
* Via **Gateway**:

  * Inventory: [http://localhost:8080/inventory/swagger](http://localhost:8080/inventory/swagger)
  * Sales: [http://localhost:8080/sales/swagger](http://localhost:8080/sales/swagger)

<p align="center">
  <img src="docs/images/swagger-inventory.png" alt="Swagger Inventory" width="420"/>
  <img src="docs/images/swagger-sales.png" alt="Swagger Sales" width="420"/>
</p>

---

## 🔐 Autenticação (JWT)

**Gerar token (Gateway)**

```powershell
Invoke-RestMethod "http://localhost:8080/auth/token" `
  -Method POST -ContentType "application/json" `
  -Body '{"userName":"mario","role":"seller"}'
```

Resposta:

```json
{ "access_token": "<JWT>" }
```

Use: `Authorization: Bearer <JWT>`.

---

## 🧪 Fluxo de Teste (end-to-end)

> O **Gateway** remove os prefixos (`PathRemovePrefix`), então os controllers usam rotas **sem** `/inventory` e **sem** `/sales`:
>
> * Inventory → `[Route("products")]`
> * Sales → `[Route("orders")]`

**1) Criar produto (Inventory via Gateway)**

```powershell
$token = (Invoke-RestMethod "http://localhost:8080/auth/token" `
  -Method POST -ContentType "application/json" `
  -Body '{"userName":"mario","role":"seller"}').access_token

$prod = Invoke-RestMethod "http://localhost:8080/inventory/products" `
  -Method POST -Headers @{ Authorization = "Bearer $token" } `
  -ContentType "application/json; charset=utf-8" `
  -Body '{"name":"Teclado","description":"Mecânico","price":199.9,"stock":5}'

$productId = $prod.id
```

**2) Criar pedido (Sales via Gateway)**

```powershell
$body  = @{ productId = $productId; quantity = 2 } | ConvertTo-Json
$order = Invoke-RestMethod "http://localhost:8080/sales/orders" `
  -Method POST -Headers @{ Authorization = "Bearer $token" } `
  -ContentType "application/json; charset=utf-8" -Body $body
```

**3) Conferir estoque (esperado: 5 → 3)**

```powershell
$check = Invoke-RestMethod ("http://localhost:8080/inventory/products/{0}" -f $productId) `
  -Headers @{ Authorization = "Bearer $token" }
$check.stock
```

---

## 🔗 Endpoints (principais)

### Gateway

* `POST /auth/token` → emite JWT
* Proxy:

  * `/inventory/*` → Inventory.Api
  * `/sales/*` → Sales.Api

### Inventory.Api

* `GET /products` *(público para listagem)*
* `POST /products` *(role `seller`)*
* `GET /products/{id}`
* `GET /products/{id}/availability?quantity={q}`

### Sales.Api *(JWT exigido; `POST /orders` exige role `seller`)*

* `POST /orders`
* `GET /orders`
* `GET /orders/{id}`

---

## 🗃️ Banco & Migrações

Os serviços usam **EF Core + SQLite**; na inicialização, o banco é **migrado automaticamente**.

Gerar manualmente:

```powershell
# Inventory
dotnet ef migrations add Init -p services/Inventory.Api -s services/Inventory.Api
dotnet ef database update      -p services/Inventory.Api -s services/Inventory.Api

# Sales
dotnet ef migrations add Init  -p services/Sales.Api     -s services/Sales.Api
dotnet ef database update      -p services/Sales.Api     -s services/Sales.Api
```

---

## 🛠️ Troubleshooting

**404 via Gateway**

* Garanta `[Route("products")]` e `[Route("orders")]` nos controllers.
* YARP está com `PathRemovePrefix: inventory/` e `sales/`.

**500 ao criar pedido**

* Geralmente RabbitMQ inacessível.
* Cheque portas (5673), variáveis `RabbitMQ__*` e se o container está **Up**.

**SQLite `no such table`**

* Apague o `.db` e/ou rode as migrações.
* Verifique permissão de escrita.

---

## 🧭 Roadmap curto

* Health Checks & CORS no Gateway
* Testes unitários (ex.: criação de produto e pedido)
* Observabilidade (Serilog + request logging)
* (Opcional) **AI.Api** para recomendações / precificação dinâmica (ML.NET/ONNX)
* Docker Compose para orquestrar tudo

---

## CI / Automação

Adicione este workflow em `.github/workflows/ci.yml` para build & testes:

```yaml
name: CI
on:
  push:
  pull_request:

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - name: Restore
        run: dotnet restore EcommerceMicro.sln
      - name: Build
        run: dotnet build EcommerceMicro.sln --configuration Release --no-restore
      # - name: Test
      #   run: dotnet test EcommerceMicro.sln --no-build --verbosity normal
```

---

## Contribuição

Contribuições são bem-vindas!
Abra uma *issue* com contexto e passos para reproduzir; *PRs* com commits pequenos e mensagens claras facilitam a revisão.

---

## 📜 Licença

Uso educacional no contexto do Bootcamp DIO — Desafio Técnico Avanade.

