# this file is written by ai because i am to lazy to do it thank u claude <3
# btw there is an image of an drowing archtichere for the system i did it you need to check it i got tired while doing it file name called arch.drawio
# you don't need to reed all of this to run the app just run tst servers and run main app open the dashboard and boom you can watch trafic and do all kind of things
# also there is a file called trafic you can run it to genrate fake trafic i liked the idea a lot

# ⚡ Custom Reverse Proxy & Real-Time Dashboard

A lightweight, high-performance **Reverse Proxy** built with .NET 8 using **Clean Architecture** principles, featuring dynamic load balancing, health checking, traffic-based caching, rate limiting, and a real-time **React (SignalR) Dashboard**.

> 💡 **Architecture Diagram:** Check out the architecture design diagram included in the repository root directory to see the complete flow between components.

---

## 🏗️ Architecture & Layer Dependencies

The solution adheres strictly to **Clean Architecture**. Dependencies must always point inward:

[ ReverseProxy.API ] ──► [ ReverseProxy.Infrastructure ]
│                                 │
▼                                 ▼
[ ReverseProxy.Application ] ──► [ ReverseProxy.Domain ]


* **`src/ReverseProxy.Domain`**: Core entities, value objects, domain logic, and interface contracts (Zero external dependencies).
* **`src/ReverseProxy.Application`**: Use cases, business rules, and interface orchestration.
* **`src/ReverseProxy.Infrastructure`**: Low-level implementations (Weighted Load Balancer, Health Checker, Binary Logger, Cache Store).
* **`src/ReverseProxy.API`**: Middleware pipeline, DI container, SignalR Hubs, and `appsettings.json`.
* **`frontend/`**: Real-time React + SignalR Dashboard built with Vite and Tailwind CSS.
* **`tests/`**: Unit/Integration tests and Mock Backend Test Servers.

---

## 🚀 Getting Started & How to Run

Follow these steps to run the complete ecosystem (Mock Servers -> Proxy API -> Dashboard -> Traffic Generator).

### 1. Start the Mock Backend Servers
The test servers simulate upstream backend nodes (`http://localhost:5001`, `5002`, `5003`). They are located in the `tests/` directory:

```bash
cd tests/TestServers # or navigate to your test servers project inside tests/
dotnet run
2. Run the Reverse Proxy API
Start the central Proxy engine (runs on http://localhost:5025 or configured port):

Bash
cd src/ReverseProxy.API
dotnet run
3. Start the Frontend Dashboard
Run the React dashboard to observe real-time metrics, node health, and traffic stats:

Bash
cd frontend
npm install
npm run dev
4. Generate Traffic
To simulate continuous incoming requests, test caching policies, and trigger rate-limiting bursts, execute the traffic generator script:

Bash
node traffic.js
```

## 🛠️ Implemented Features & Build Flow

* ** Reverse Proxy Routing: Forwards requests seamlessly to configured backend nodes while stripping hop-by-hop headers.

* ** Weighted Load Balancing: Distributes incoming traffic based on server weight metrics and active health status.

* ** Background Health Checking: Periodically pings nodes to automatically mark failed backends as Offline and restore them when recovered.

* ** Binary Logging System: Custom high-performance binary logger for efficient request history tracking.

* ** Fixed Window Rate Limiting: IP & route-based limit enforcement returning HTTP 429 Too Many Requests.

* ** Traffic-Based Caching: Smart caching middleware with whitelist routing and hit/miss header injection (X-Cache: HIT / MISS).

* ** Real-time SignalR Broadcast: Pushes
