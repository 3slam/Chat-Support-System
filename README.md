# Trips Chat Support — MVP

An all-in-one ASP.NET Core **MVC** application: a customer chat widget with a
pre-chat bot flow and a back-office agent inbox, wired together with **SignalR**
for real-time messaging and **EF Core + SQL Server** for storage.

## Stack
- ASP.NET Core MVC (.NET 9)
- EF Core 9 (SQL Server) — code-first migrations
- SignalR (real-time text both directions)
- Cookie authentication for the back-office
- Bootstrap 5 UI
- Docker / Docker Compose (app + SQL Server container)

## Features (mapped to the requirements)

**Customer-facing (`/`)**
- Open chat **with or without login** (anonymous "Guest" by default; optional email).
- Pre-chat **bot flow**: free text → 4 category buttons (**Flight / Hotel / Visa / Activity**)
  → category sub-options (**استفسار عام | لدي مشكلة مع الحجز**).
- Send/receive text in **real time** over SignalR.
- The widget remembers the conversation (localStorage) and **resumes** on reload.
- **"+ New chat"** button in the header starts a fresh conversation any time.

**Agent / back-office (`/Admin/Inbox`)**
- Inbox with **5 tabs**: `All | Flights | Hotel | Visa | Activity` (live-refreshing).
- **Auto-assignment**: the agent who picks up a chat is assigned it.
- Admins can **reassign** to any other agent.
- Admins can **close** the conversation.

## Run it with Docker (recommended)

No need for Visual Studio to support .NET 10 — Docker builds and runs everything,
including a SQL Server container.

```powershell
cd "C:\Users\mosama\Desktop\Chat Supprot"
docker compose up -d --build
```

This starts two containers: `chatsupport-db` (SQL Server 2022) and
`chatsupport-web`. On first start the app applies the EF migration and
**seeds 10 admin accounts**.

- Customer chat:  http://localhost:5080/
- Agent login:    http://localhost:5080/Account/Login

> The host port is **5080** (8080 is reserved by Windows/WinNAT). The container
> listens on 8080 internally; the mapping is `5080:8080` in `docker-compose.yml`.

Stop / reset:
```powershell
docker compose down          # stop containers (keeps the DB volume)
docker compose down -v       # stop AND wipe the database volume
```

## Run it without Docker (local SQL Server / LocalDB)

```powershell
dotnet run
```
Uses the connection string in `appsettings.json` (LocalDB). App serves on
http://localhost:5119/. To point at a different SQL Server, edit
`ConnectionStrings:Default`.

### Seeded accounts
| Email                          | Password       |
|--------------------------------|----------------|
| `admin1@trips.sa` … `admin10@trips.sa` | `Password123!` |

Open the customer page in one browser and the agent inbox (logged in) in another
to watch messages flow in real time.

## Database
Connection string is in `appsettings.json` (`ConnectionStrings:Default`),
pointing at `(localdb)\MSSQLLocalDB`, database `ChatSupportDb`. To use the full
SQL Server instance instead, change it to e.g.
`Server=.;Database=ChatSupportDb;Trusted_Connection=True;TrustServerCertificate=true`.

Reset the database:
```powershell
dotnet ef database drop -f
dotnet run   # re-creates and re-seeds
```

## Project layout
```
Controllers/
  HomeController.cs        customer chat page
  ChatApiController.cs     bot-flow + lifecycle JSON API
  AccountController.cs     agent login / logout
  AdminController.cs       inbox, conversation, pickup, reassign, close
Hubs/ChatHub.cs            SignalR real-time transport + inbox notifications
Models/                    Admin, Conversation, Message, enums
Data/                      AppDbContext, DbSeeder
Services/PasswordHasher.cs salted SHA-256 (swap for Identity in production)
Views/Home/Index           customer widget
Views/Admin/Inbox          5-tab inbox
Views/Admin/Conversation   agent workspace
wwwroot/js/                 customer-chat, admin-inbox, admin-conversation
```

## Notes / next steps for production
- Replace the SHA-256 hasher with ASP.NET Core Identity (PBKDF2/bcrypt) + lockout.
- Add agent vs admin role separation if only some users may reassign/close.
- Add paging/search to the inbox and unread counters per tab.
