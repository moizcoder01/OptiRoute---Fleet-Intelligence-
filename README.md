<div align="center">

# ⬡ OptiRoute — Fleet Intelligence

**A full-stack desktop logistics and delivery management system**  
Built with C# .NET WinForms · SQL Server · OpenRouteService API · Leaflet.js

![C#](https://img.shields.io/badge/C%23-.NET%20WinForms-0052CC?style=flat&logo=csharp)
![SQL Server](https://img.shields.io/badge/Database-SQL%20Server-CC2927?style=flat&logo=microsoftsqlserver)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?style=flat&logo=windows)
![Status](https://img.shields.io/badge/Status-Academic%20Project-22C55E?style=flat)

</div>

---

## Overview

OptiRoute is a desktop application for managing a courier and delivery fleet end-to-end. It provides three independent role-based dashboards — Admin, Driver, and Customer — each with its own feature set. The system handles order placement, driver assignment, real-time GPS telemetry, route calculation, fuel tracking, vehicle maintenance monitoring, and financial reporting.

Developed as a second-semester group project to demonstrate proficiency in desktop application development, relational database design, repository pattern architecture, and real-world API integration.

---

## Features

### Admin Dashboard
- Overview stat cards — pending orders, available drivers, total delivered, total revenue
- Full order management with status filtering (Pending, Assigned, Picked, Delivered, Returned)
- Assign pending orders to available drivers with one click
- Driver and vehicle management — fuel levels, ratings, maintenance status
- Revenue reports — total revenue, prepaid vs COD breakdown, driver performance
- Profile management

### Driver Dashboard
- Personal stat cards — assigned, delivered, pending pickup, returned
- Vehicle info card with live fuel bar
- Star rating display with delivery count
- My Assignments page — Pick Order and Deliver buttons per assignment
- Active Delivery page — live telemetry sync, route info, COD collection prompt
- Delivery History with star ratings per order
- Profile management — personal info, vehicle details, change password, photo upload

### Customer Dashboard
- Order summary stat cards — total, in transit, delivered, urgent
- Place new orders with item details, weight, pickup/delivery points, priority, payment method
- Live fare estimate as weight and priority are entered
- My Orders page — full order list with status badges, cancel button, star rating after delivery
- Track Order page — step-by-step order progress timeline
- Profile management — personal info, photo upload, change password

---

## Tech Stack

| Layer | Technology |
|---|---|
| Language | C# (.NET 8, WinForms) |
| Database | Microsoft SQL Server |
| ORM / Data Access | Repository Pattern (raw ADO.NET with parameterized queries) |
| Password Security | BCrypt.Net |
| Route Calculation | OpenRouteService (ORS) REST API — driving-car profile |
| Geocoding | Nominatim (OpenStreetMap) — free, no API key required |
| Map Rendering | Leaflet.js via WebView2 |
| Fallback Routing | Haversine straight-line distance when ORS is unavailable |
---

## Database Design

The SQL Server database contains **6 tables**, **6 views**, and **4 triggers**.

### Tables
| Table | Purpose |
|---|---|
| Table_Users | All accounts — Admin, Customer, Driver |
| Table_Drivers | Driver-specific data — rating, license number |
| Table_Vehicles | Vehicle data — plate, type, fuel, distance, maintenance |
| Table_Orders | Order lifecycle — placement through delivery |
| Table_OrderHistory | Immutable archive of delivered orders |
| Table_Telemetry | GPS tick data — coordinates and fuel burned per tick |

### Views
| View | Purpose |
|---|---|
| vw_DriverDetails | Encapsulates the 3-table JOIN across Users, Drivers, Vehicles |
| vw_OrderFull | Encapsulates the Orders + Vehicles JOIN |
| vw_RevenueSummary | Pre-aggregates revenue data from all delivered orders |
| vw_DriverStats | Pre-aggregates order counts per driver by status |
| vw_TelemetrySummary | Pre-aggregates total fuel burned per order |
| vw_DriverAverageRating | Pre-aggregates average star rating per driver |

### Triggers
| Trigger | Fires On | Auto-Does |
|---|---|---|
| trg_OrderStatus_AfterUpdate | Table_Orders — OrderStatus change | Archives to history, manages vehicle availability, increments distance, flags maintenance |
| trg_Vehicle_MaintenanceAutoFlag | Table_Vehicles — distance column change | Auto-sets NeedsMaintenance when threshold exceeded |
| trg_Telemetry_AfterInsert | Table_Telemetry — every GPS insert | Auto-deducts fuel from vehicle |
| trg_Rating_AfterUpdate | Table_Orders — Rating column change | Auto-recalculates driver average rating |

---

## Setup & Installation

### Prerequisites
- Windows 10 or later
- Visual Studio 2022 (with .NET Desktop workload)
- SQL Server 2019 or later (Express edition works)
- SQL Server Management Studio (SSMS) — recommended

### Steps

**1. Clone the repository**
```bash
git clone https://github.com/moizcoder01/OptiRoute---Fleet-Intelligence-.git
cd OptiRoute---Fleet-Intelligence-
```

**2. Set up the database**

Open SSMS and run the full SQL schema file included in the project. This will create the OptiRoute database, all 6 tables, indexes, views, triggers, and seed the Admin account.

Default Admin credentials after seeding:
**3. Configure the connection string**

Open `Core/Data/DbConfig.cs` and update the connection string to match your SQL Server instance:
```csharp
public static string ConnectionString =
    "Server=YOUR_SERVER_NAME;Database=OptiRoute;Trusted_Connection=True;TrustServerCertificate=True;";
```

**4. Build and run**

Open `OptiRoute.slnx` in Visual Studio, restore NuGet packages, and press F5.

---

## NuGet Dependencies

| Package | Purpose |
|---|---|
| BCrypt.Net-Next | Password hashing and verification |
| Microsoft.Data.SqlClient | SQL Server connection |
| Microsoft.Web.WebView2 | Leaflet.js map rendering in WinForms |

---

## Architecture Notes

- **Zero SQL in UI forms** — all database calls go through repository classes in `Core/Data/`
- **Repository pattern** — each entity has its own repository; forms only call typed methods
- **Parameterized queries throughout** — no string concatenation in SQL, eliminating injection risk
- **Database-level automation** — triggers handle archiving, fuel deduction, maintenance flagging, and rating recalculation automatically
- **Graceful API fallback** — if ORS routing or Nominatim geocoding fail, the system falls back to Haversine distance estimation and flags the result as estimated in the UI

---

---

## License

This project was developed for academic purposes as part of a second-semester university group project.

---
