# .NET 10 Clean Architecture Solution Template (MSSQL)

A production-ready, parametric **.NET 10** solution template built on **Clean Architecture** principles and pre-configured for **Microsoft SQL Server (MSSQL)**.

---

## Architecture & Project Structure

This template enforces the **Dependency Rule**—inner layers contain core business rules with zero external dependencies, while outer layers implement database and API concerns.

```text
├── src/
│   ├── Domain/          # Enterprise entities, value objects, enums, and domain exceptions (Zero dependencies)
│   ├── Application/     # Business logic, CQRS (MediatR), FluentValidation, and DbContext interfaces
│   ├── Infrastructure/  # EF Core ApplicationDbContext, MSSQL configuration, and external services
│   └── WebApi/          # REST API controllers, Dependency Injection setup, and Scalar OpenAPI UI
└── .template.config/    # Parametric dotnet new & Visual Studio 2026 template configuration
```

### Included Packages (.NET 10 Compatible)
* **CQRS & Validation:** `MediatR` (12.4.1), `FluentValidation` (11.11.0)
* **Database & ORM:** `Microsoft.EntityFrameworkCore.SqlServer` (10.0.0), `Microsoft.EntityFrameworkCore.Tools` (10.0.0)
* **API Documentation:** `Microsoft.AspNetCore.OpenApi` (10.0.0), `Scalar.AspNetCore` (2.1.0)

---

## Prerequisites

* [.NET 10 SDK](https://microsoft.com)
* [Visual Studio 2026](https://microsoft.com) (or VS Code / JetBrains Rider)
* **SQL Server** (LocalDB, SQL Express, SQL Server Developer, or Docker container)

---

## Template Installation

Clone this repository and install the template locally so it becomes available in both the **CLI** and **Visual Studio 2026**:

```bash
git clone https://github.com
cd YOUR_REPO_NAME
dotnet new install ./
```

*(To update the template after making changes, run `dotnet new install ./ --force` from the root directory.)*

---

## Creating a New Project

### Option 1: Using the .NET CLI
Generate a new solution using the `clean-net10` short name:

```bash
# Create with default settings (LocalDB + Scalar enabled)
dotnet new clean-net10 -n MyNewProject

# Create with custom SQL Server host and Scalar disabled
dotnet new clean-net10 -n MyNewProject --DbServer "localhost" --UseScalar false
```

#### Available Template Parameters

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `-n`, `--name` | `string` | `GSB_project` | Sets the solution name, folder names, namespaces, and database name. |
| `--DbServer` | `string` | `(localdb)\mssqllocaldb` | Configures the MSSQL server address in `appsettings.json`. |
| `--UseScalar` | `bool` | `true` | Includes or excludes Scalar OpenAPI interactive documentation. |

### Option 2: Using Visual Studio 2026
1. Open **Visual Studio 2026** and click **Create a new project**.
2. Search for **`.NET 10 Clean Architecture (MSSQL)`** and click **Next**.
3. Enter your **Project name** and location, then click **Next**.
4. Configure **DbServer** and **UseScalar** on the **Additional information** screen and click **Create**.

---

## Database Setup & Migrations (MSSQL)

1. Verify your connection string in `src/<ProjectName>.WebApi/appsettings.json`.
2. Open a terminal in the generated solution root and create your initial migration:

```bash
# Add a new EF Core migration
dotnet ef migrations add InitialCreate --project src/<ProjectName>.Infrastructure --startup-project src/<ProjectName>.WebApi

# Apply migrations and create the MSSQL database
dotnet ef database update --project src/<ProjectName>.Infrastructure --startup-project src/<ProjectName>.WebApi
```

---

## Running the Application

Start the WebApi project:

```bash
dotnet run --project src/<ProjectName>.WebApi
```

If `UseScalar` is enabled, navigate to `https://localhost:<PORT>/scalar/v1` in your browser during development to explore and test your API endpoints.


```bash
dotnet new clean-net10 -n StokYonetim --DbServer "localhost"
cd StokYonetim

# Veritabanındaki tüm tabloları çekmek için:
.\scaffold-db.ps1 -Server "localhost" -Database "MevcutVeritabaniAdi"

# Veya sadece istediğiniz tabloları çekmek için:
.\scaffold-db.ps1 -Server "localhost" -Database "MevcutVeritabaniAdi" -Tables "Urunler,Kategoriler"

```

## Otomatik testler

Gereksinimler, test teknikleri ve �al��t�rma komutlar�: [Test rehberi](docs/TESTING.md).

Performans, güvenlik geliştirmeleri ve ölçüm sonuçları: [Web kalite rehberi](docs/WEB_QUALITY.md).

Üretimde JWT kimlik doğrulaması için Authority/Audience yapılandırması zorunludur. Development ortamında `dotnet user-jwts` desteği vardır. Rol kuralları ve yerel k6 çalıştırması: [Kimlik doğrulama ve yük testi](docs/AUTHENTICATION.md).

