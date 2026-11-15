param(
    [string]$Server = "(localdb)\mssqllocaldb",
    [string]$Database = "CleanAppDB",
    [string]$Tables = "" # Belirli tabloları çekmek için: "Products,Orders"
)

$ConnectionString = "Server=$Server;Database=$Database;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"

$ScaffoldArgs = @(
    "ef", "dbcontext", "scaffold", $ConnectionString, "Microsoft.EntityFrameworkCore.SqlServer",
    "--project", "src/GSB_project.Infrastructure",
    "--startup-project", "src/GSB_project.WebApi",
    "--context", "ApplicationDbContext",
    "--context-dir", "Persistence",
    "--output-dir", "../GSB_project.Domain/Entities",
    "--namespace", "GSB_project.Domain.Entities",
    "--context-namespace", "GSB_project.Infrastructure.Persistence",
    "--no-onconfiguring",
    "--use-database-names",
    "--force"
)

if ($Tables -ne "") {
    foreach ($table in $Tables.Split(",")) {
        $ScaffoldArgs += "--table"
        $ScaffoldArgs += $table.Trim()
    }
}

Write-Host "MSSQL veritabanindan Scaffold baslatiliyor ($Server -> $Database)..." -ForegroundColor Cyan
dotnet @ScaffoldArgs
Write-Host "Tamamlandi! Entity'ler Domain/Entities altina, ApplicationDbContext ise Infrastructure/Persistence altina uretildi." -ForegroundColor Green